using Naufal_Windows_Tech_s_Powertoys;
using System.Text;
using System.Text.Json;

internal static class AuditRegressionTests
{
    internal static async Task RunAsync(Action<bool, string> assert)
    {
        // F01: process success is not read-back success; percentages are not conversion states.
        var encrypted = new BitLockerVolumeInfo("C:", "FullyEncrypted", 100, "On", "Unlocked");
        assert(!BitLockerVerification.ProtectionSucceeded(0, null, "Off"), "F01 null read-back");
        assert(!BitLockerVerification.DecryptionAccepted(0, null), "F01 missing decryption read-back");
        foreach (string status in new[] { "Unknown", "", "On", "Off" })
        {
            var volume = encrypted with { ProtectionStatus = status };
            assert(BitLockerVerification.ProtectionSucceeded(0, volume, "Off") == (status == "Off"), "F01 suspend " + status);
            assert(BitLockerVerification.ProtectionSucceeded(0, volume, "On") == (status == "On"), "F01 resume " + status);
            assert(!BitLockerVerification.ProtectionSucceeded(1, volume, "On"), "F01 command failure " + status);
        }
        assert(!(encrypted with { EncryptionPercentage = 0 }).IsFullyDecrypted, "F01 zero percent is not decrypted");
        assert(!BitLockerVerification.DecryptionAccepted(0, encrypted with { VolumeStatus = "DecryptionPaused" }), "F01 paused not active");
        assert(BitLockerVerification.DecryptionAccepted(0, encrypted with { VolumeStatus = "DecryptionInProgress" }), "F01 asynchronous decryption accepted");
        assert(BitLockerVerification.DecryptionAccepted(0, encrypted with { VolumeStatus = "FullyDecrypted" }), "F01 decryption completed");
        assert(NativeHardwareData.ConversionName("3") == "DecryptionInProgress" && NativeHardwareData.ConversionName("99") == "Unknown", "F01 numeric provider conversion");
        assert(NativeHardwareData.ProtectionName("2") == "Unknown", "F01 unknown protection enum");

        // F02: a real operation must be invoked, not a success log substituted for it.
        int resets = 0;
        string? resetPackage = null;
        Task Reset(string package) { resets++; resetPackage = package; return Task.CompletedTask; }
        assert(await StoreResetStage.RunAsync("Store.current-user", Reset) == "" && resets == 1 && resetPackage == "Store.current-user", "F02 invokes reset");
        assert((await StoreResetStage.RunAsync(null, Reset)).Length > 0 && resets == 1, "F02 absent current-user package is warning");
        assert((await StoreResetStage.RunAsync("Store", _ => Task.FromException(new InvalidOperationException("package in use")))).Contains("package in use"), "F02 in-use reset warning");

        // F03: parsers and actual harmless child-process pipe behavior.
        assert(CommandOutputProgress.ParsePercent("25,5%\r") == 25.5, "F03 comma percent and CR");
        assert(CommandOutputProgress.ParsePercent("100%") == 100, "F03 final percent");
        assert(CommandOutputProgress.ParsePercent("101%") is null, "F03 reject invalid percent");
        assert(CommandOutputProgress.ParsePercent("progress pending") is null, "F03 indeterminate progress");
        var chunks = new Capture<MaintenanceProgressUpdate>();
        var progress = new CommandOutputProgress(chunks, 1, 1, "fake");
        progress.Report("2"); progress.Flush(); progress.Report("5%"); progress.Flush();
        assert(chunks.Values.Last().StagePercent == 25, "F03 percent crossing chunk boundary");
        progress.Report("\r10%"); progress.Flush();
        assert(chunks.Values.Last().StagePercent == 25, "F03 no backward progress");
        var runner = new NativeCommandRunner();
        var firstOutput = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pipe = new Capture<string>(_ => firstOutput.TrySetResult());
        Task<NativeCommandResult> running = runner.RunAsync(Environment.ProcessPath!, new[] { "--audit-child-stream" }, TimeSpan.FromSeconds(8), outputProgress: pipe, outputEncoding: Encoding.Unicode);
        await firstOutput.Task.WaitAsync(TimeSpan.FromSeconds(5));
        assert(!running.IsCompleted, "F03 receives stdout before process exit");
        var command = await running;
        assert(command.ExitCode == 0 && command.StandardOutput.Contains("100%") && command.StandardOutput.Contains("測試"), "F03 UTF16 output preserved");
        assert(command.StandardError.Contains("simulated warning"), "F03 stderr is drained");
        var timeout = await runner.RunAsync(Environment.ProcessPath!, new[] { "--audit-child-wait" }, TimeSpan.FromMilliseconds(350));
        assert(timeout.TimedOut && timeout.ExitCode == -1 && timeout.StandardError.Contains("timed out"), "F03 bounded timeout");
        using (var cancellation = new CancellationTokenSource(350))
        {
            bool cancelled = false;
            try { await runner.RunAsync(Environment.ProcessPath!, new[] { "--audit-child-wait" }, TimeSpan.FromSeconds(8), cancellation.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            assert(cancelled, "F03 explicit cancellation propagated");
        }

        // F04: warning belongs to its stage; skipped and failed steps never become PASS.
        var updates = new Capture<MaintenanceProgressUpdate>();
        int warnings = 0;
        var tracker = new MaintenanceStageTracker(updates, () => warnings);
        MaintenanceProgress.StartStage(tracker, 1, 3, "reset");
        warnings++;
        MaintenanceProgress.StartStage(tracker, 2, 3, "shell");
        tracker.Skip("already restored");
        MaintenanceProgress.StartStage(tracker, 3, 3, "verify");
        tracker.Complete(true);
        assert(updates.Values.Last(x => x.StageIndex == 1).Status == "WARNING", "F04 stage warning retained");
        assert(updates.Values.Last(x => x.StageIndex == 2).Status == "SKIPPED", "F04 skipped retained");
        assert(updates.Values.Last(x => x.StageIndex == 3).Status == "PASS", "F04 independently verified final stage");
        updates.Values.Clear();
        tracker = new MaintenanceStageTracker(updates, () => 0);
        MaintenanceProgress.StartStage(tracker, 1, 3, "preflight");
        tracker.Complete(false);
        assert(updates.Values.Last(x => x.StageIndex == 1).Status == "FAILED", "F04 failed stage");
        assert(updates.Values.Count(x => x.Status == "SKIPPED") == 2 && !updates.Values.Any(x => x.Status == "PASS"), "F04 unreached stages skipped");
        int beforeLate = updates.Values.Count;
        tracker.Complete(false);
        tracker.Report(new(1, 3, "late", "late chunk"));
        assert(updates.Values.Count == beforeLate, "F04 ignore late and repeated completion");
        assert(!updates.Values.Any(x => x.Status == "PASS"), "F04 repeated completion cannot turn failure into pass");

        // F05: actual embedded supplemental language tables and stable native aliases.
        var catalog = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        SupplementalUiCatalog.Merge(catalog);
        assert(catalog.Count == 23, "F05 23 supplemental language tables");
        string[] keys = { UiTextKeys.RepairSelected, UiTextKeys.EnableSelected, UiTextKeys.DownloadInstall,
            UiTextKeys.AnalyzeReload, UiTextKeys.SelectSafe, UiTextKeys.ApplySelected,
            UiTextKeys.RuntimeRepairConfirmation, UiTextKeys.RuntimeEnableConfirmation, UiTextKeys.RuntimeInstallConfirmation };
        foreach (var language in catalog)
            foreach (string key in keys)
                assert(language.Value.TryGetValue(key, out string? translation) && !string.IsNullOrWhiteSpace(translation), "F05 " + language.Key + ": " + key);
        assert(catalog["id"][UiTextKeys.SelectSafe] == "Pilih yang aman", "F05 native Indonesian toolbar");
        assert(catalog["de"].ContainsKey("repair selected"), "F05 original uppercase/native titlecase aliases");
        SupplementalUiCatalog.Merge(catalog);
        assert(catalog.Count == 23, "F05 repeat merge idempotent");

        // F06: authoritative identity, never arbitrary matching of same-model NICs.
        Dictionary<string, string> Nic(string id, string location, string name) => new()
            { ["PNPDeviceID"] = id, ["LocationInformationString"] = location, ["InterfaceDescription"] = name, ["Name"] = name };
        var nicA = Nic("PCI-A", "slot 1", "NIC");
        var nicB = Nic("PCI-B", "slot 2", "NIC");
        var nics = new[] { nicA, nicB };
        var exact = MsiNdisIdentity.Match(nics, "pci-a", "", "NIC");
        assert(ReferenceEquals(exact.Row, nicA) && exact.Source == "exact PNPDeviceID", "F06 precise identity precedes duplicate names");
        assert(ReferenceEquals(MsiNdisIdentity.Match(nics, "unknown", "slot 2", "NIC").Row, nicB), "F06 exact location fallback");
        assert(MsiNdisIdentity.Match(nics, "", "", "NIC").Row is null, "F06 ambiguous NIC remains unknown");
        assert(MsiNdisIdentity.Match(nics, "", "", "").Row is null, "F06 empty identity never matches");
        assert(MsiNdisIdentity.Match(Array.Empty<Dictionary<string, string>>(), "a", "b", "c").Row is null, "F06 provider absent");

        // F07: media is not bus, and missing health is not Healthy.
        foreach (var pair in new[] { ("0", "Healthy"), ("1", "Warning"), ("2", "Unhealthy"), ("5", "Unknown"), ("", "Unknown") })
            assert(NativeHardwareData.DiskHealth(pair.Item1) == pair.Item2, "F07 health " + pair.Item1);
        foreach (var pair in new[] { ("3", "HDD"), ("4", "SSD"), ("5", "SCM"), ("0", "Unspecified"), ("NVMe", "Unknown"), ("", "Unknown") })
            assert(NativeHardwareData.DiskMedia(pair.Item1) == pair.Item2, "F07 media " + pair.Item1);

        // F08: full 16-state Ready/Missing/Optional/busy action matrix.
        foreach (bool busy in new[] { false, true })
        foreach (bool installable in new[] { false, true })
        foreach (bool repairable in new[] { false, true })
        foreach (bool enableable in new[] { false, true })
        {
            var actions = RuntimeActionAvailability.Resolve(busy, installable, repairable, enableable);
            assert(actions.Install == (!busy && installable && !repairable) &&
                actions.Repair == (!busy && repairable) && actions.Enable == (!busy && enableable),
                $"F08 gating {busy}/{installable}/{repairable}/{enableable}");
        }

        // F09: atomic durable transaction data; use test-only temp directory, never ProgramData.
        string directory = Path.Combine(Path.GetTempPath(), "NaufalPowertoys-Audit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "transaction.json");
        try
        {
            string guid = Guid.NewGuid().ToString();
            var known = new Dictionary<string, string> { ["Optimized Gaming"] = guid, ["Invalid"] = guid, ["Balanced"] = "not-a-guid" };
            var checks = new ProfileVerificationResult("Optimized Gaming", Enumerable.Range(1, 23).Select(i => new ProfileVerificationCheck("Check " + i, "expected", "actual", true)).ToArray());
            string Save(bool ok, bool rollback, bool? rolledBack) =>
                PerformanceProfileTransaction.Save(path, "Optimized Gaming", ok, 12.3456, ok, guid, known, rollback, rolledBack, checks, ok ? null : "simulated failure");
            assert(Save(true, false, null) == "", "F09 transaction save");
            using (var json = JsonDocument.Parse(File.ReadAllText(path)))
            {
                var root = json.RootElement;
                assert(root.GetProperty("SchemaVersion").GetInt32() == 2 && root.GetProperty("Checks").GetArrayLength() == 23, "F09 schema and 23 checks");
                assert(root.GetProperty("Success").GetBoolean() && root.GetProperty("RestartRequired").GetBoolean(), "F09 success and restart");
                assert(root.GetProperty("RollbackSucceeded").ValueKind == JsonValueKind.Null, "F09 no rollback != successful rollback");
                assert(root.GetProperty("KnownPowerGuids").EnumerateObject().Count() == 1, "F09 validate persisted GUID keys");
            }
            assert(PerformanceProfileVerificationService.ReadKnownPowerGuids(path)["Optimized Gaming"] == guid, "F09 reload saved state");
            assert(Save(false, true, false) == "", "F09 failed transaction saved");
            using (var json = JsonDocument.Parse(File.ReadAllText(path)))
                assert(!json.RootElement.GetProperty("RollbackSucceeded").GetBoolean() && !json.RootElement.GetProperty("Success").GetBoolean(), "F09 partial rollback isn't success");
            assert(PerformanceProfileTransaction.Format(checks).Contains("23/23"), "F09 detail report");
            // Older acuan schema: aliases are migrated without trusting malformed or failed targets.
            File.WriteAllText(path, "{\"Profile\":\"Gaming\",\"TargetPowerGuid\":\"" + guid + "\"}");
            assert(PerformanceProfileVerificationService.ReadKnownPowerGuids(path)["Competitive Gaming"] == guid, "F09 legacy transaction alias");
            File.WriteAllText(path, "{\"Profile\":\"Gaming\",\"Success\":false,\"TargetPowerGuid\":\"" + guid + "\"}");
            assert(PerformanceProfileVerificationService.ReadKnownPowerGuids(path).Count == 0, "F09 failed target isn't historical success");
            File.WriteAllText(path, "{bad json");
            assert(PerformanceProfileVerificationService.ReadKnownPowerGuids(path).Count == 0, "F09 corrupt history ignored");
            assert(Save(true, false, null) == "", "F09 recovery from corrupt history");
            string previous = File.ReadAllText(path);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                assert(Save(false, true, false).StartsWith("Transaction record could not be saved:"), "F09 denied replacement emits warning");
            assert(File.ReadAllText(path) == previous, "F09 failed atomic replace preserves previous record");
            assert(Directory.GetFiles(directory, "*.tmp").Length == 0, "F09 temp files removed");
        }
        finally
        {
            // Delete only files created by this test in its unique directory.
            if (File.Exists(path)) File.Delete(path);
            Directory.Delete(directory, recursive: false);
        }
    }

    internal static async Task<bool> RunChildAsync(string[] args)
    {
        if (args.Contains("--audit-child-flood"))
        {
            Console.OutputEncoding = Encoding.UTF8;
            // Deliberately exceed both redirected pipe capacities. No file,
            // registry, Windows provider, repair or installer is involved.
            for (int i = 0; i < 96; i++)
            {
                await Console.Out.WriteAsync(new string('O', 4096));
                await Console.Out.FlushAsync();
                await Console.Error.WriteAsync(new string('E', 4096));
                await Console.Error.FlushAsync();
            }
            Console.Write("OUT-END");
            Console.Error.Write("ERR-END");
            if (args.Contains("--audit-exit-7")) Environment.ExitCode = 7;
            return true;
        }
        if (args.Contains("--audit-provider-probe"))
        {
            try
            {
                var disks = await Task.Run(() => NativeHardwareData.Query(@"ROOT\Microsoft\Windows\Storage", "MSFT_PhysicalDisk", "Size", "MediaType", "HealthStatus"));
                Console.WriteLine("Physical disks: " + disks.Count);
                foreach (var disk in disks) Console.WriteLine("  " + disk["Size"] + " bytes; " + NativeHardwareData.DiskMedia(disk["MediaType"]) + "; " + NativeHardwareData.DiskHealth(disk["HealthStatus"]));
            }
            catch (Exception ex) { Console.WriteLine("Disk query unavailable: " + ex.Message); }
            try
            {
                var volumes = await Task.Run(NativeHardwareData.ReadBitLockerVolumes);
                Console.WriteLine("BitLocker volumes: " + volumes.Count);
                foreach (var volume in volumes) Console.WriteLine("  " + volume.MountPoint + ": " + volume.VolumeStatus + "; " + volume.ProtectionStatus + "; " + volume.LockStatus);
            }
            catch (Exception ex) { Console.WriteLine("BitLocker query unavailable: " + ex.Message); }
            try
            {
                var nics = await Task.Run(() => NativeHardwareData.Query(@"ROOT\StandardCimv2", "MSFT_NetAdapterHardwareInfoSettingData", "MaxInterruptMessages", "NumMsixTableEntries"));
                Console.WriteLine("NDIS hardware rows: " + nics.Count);
                foreach (var nic in nics) Console.WriteLine("  max=" + nic["MaxInterruptMessages"] + "; MSI-X entries=" + nic["NumMsixTableEntries"]);
            }
            catch (Exception ex) { Console.WriteLine("NDIS query unavailable: " + ex.Message); }
            Console.WriteLine("READ-ONLY PROBE COMPLETE: no setters, repairs, or registry writes.");
            return true;
        }
        if (args.Contains("--audit-child-wait")) { await Task.Delay(10000); return true; }
        if (!args.Contains("--audit-child-stream")) return false;
        Console.OutputEncoding = Encoding.Unicode;
        Console.Write("25,5%\r測試"); await Console.Out.FlushAsync();
        await Task.Delay(1000);
        Console.Error.Write("simulated warning"); await Console.Error.FlushAsync();
        Console.Write("100%"); await Console.Out.FlushAsync();
        return true;
    }

    private sealed class Capture<T>(Action<T>? callback = null) : IProgress<T>
    {
        internal List<T> Values { get; } = new();
        public void Report(T value) { lock (Values) Values.Add(value); callback?.Invoke(value); }
    }
}
