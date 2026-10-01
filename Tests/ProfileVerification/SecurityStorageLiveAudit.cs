using Microsoft.Win32;
using Naufal_Windows_Tech_s_Powertoys;
using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;

// Explicit guest-only acceptance test. The ordinary regression entry point never calls RunAsync.
// No automatic reboot, firmware change, policy override, payload removal or host servicing.
internal static class SecurityStorageLiveAudit
{
    internal const string Feature = "Printing-PrintToPDFServices-Features";
    internal sealed record Identity(Guid Vm, string Sid, string Machine, int Build, string Boot);
    internal sealed record Manifest(int Version, Identity Original, string Phase,
        SecurityMitigationSnapshot Security, string StorageOriginal, string StorageOutcome,
        bool SecurityChanged, bool? AppliedEffective, string LastBoot, string Note);

    // Compatibility for evidence written by the first harness, which waited even
    // when security was skipped. This route only reads state and completes evidence.
    internal static bool CanFinalizeWithoutBoot(Manifest m) =>
        m.Phase == "AwaitAppliedBoot" && !m.SecurityChanged && m.AppliedEffective is null &&
        (m.StorageOutcome == "NotExercised" ||
            (m.StorageOutcome == "PostBootRoundTripVerified" && m.StorageOriginal == "Enabled"));

    internal static string ResumeBlock(Manifest m, Identity now)
    {
        if (m.Version != 1 || m.Original.Vm == Guid.Empty || m.Original.Build < 19041 ||
            string.IsNullOrWhiteSpace(m.Original.Sid) || string.IsNullOrWhiteSpace(m.Original.Machine) ||
            string.IsNullOrWhiteSpace(m.Original.Boot) || string.IsNullOrWhiteSpace(m.LastBoot)) return "Invalid manifest identity.";
        if (m.Original.Vm != now.Vm || m.Original.Sid != now.Sid || m.Original.Machine != now.Machine || m.Original.Build != now.Build)
            return "VM, account, machine or Windows build changed. No mutation permitted.";
        if (m.Phase is not ("AwaitAppliedBoot" or "AwaitRestoredBoot" or "AwaitStorageDisabledBoot" or "AwaitStorageRestoredBoot"))
            return "This phase cannot be resumed automatically: " + m.Phase + ". Review evidence or revert the disposable VM checkpoint.";
        if (string.IsNullOrWhiteSpace(now.Boot) || (now.Boot == m.LastBoot && !CanFinalizeWithoutBoot(m)))
            return "A different guest boot is required. Restart manually inside the disposable guest, then Resume this same report directory.";
        if (m.SecurityChanged && (m.Security.MachineIdentity != m.Original.Machine || m.Security.HvciEnabled is not (null or 0)))
            return "Invalid original HVCI snapshot. No mutation permitted.";
        if (m.Phase.StartsWith("AwaitStorage", StringComparison.Ordinal) &&
            (m.StorageOriginal != "Enabled" || m.SecurityChanged)) return "Invalid storage phase baseline.";
        return "";
    }

    internal static bool AcceptedStorageChange(string outcome, string actual, bool enabled) =>
        (outcome is "Verified" or "RebootRequired") &&
        (actual == (enabled ? "Enabled" : "Disabled") || actual == (enabled ? "Enable Pending" : "Disable Pending"));

    internal static bool RuntimeMatches(SecurityMitigationSnapshot expected, SecurityMitigationSnapshot actual) =>
        expected.EvidenceError.Length == 0 && actual.EvidenceError.Length == 0 &&
        expected.VbsStatus.HasValue && actual.VbsStatus == expected.VbsStatus &&
        expected.Configured is not null && actual.Configured is not null &&
        expected.Running is not null && actual.Running is not null &&
        expected.Configured.Order().SequenceEqual(actual.Configured.Order()) &&
        expected.Running.Order().SequenceEqual(actual.Running.Order());

    internal static async Task RunAsync(string[] args)
    {
        Guid vm = GuestAuditIdentity.Require(args); // First: no file, process or registry writes before guest identity.
        string Argument(string name)
        {
            int at = Array.IndexOf(args, name);
            if (at < 0 || at + 1 >= args.Length) throw new ArgumentException("Missing " + name);
            return args[at + 1];
        }
        if (!WindowsPrivilegeService.IsAdministrator()) throw new InvalidOperationException("Guest administrator token required.");
        string sid = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("User SID unavailable.");
        if (sid != Argument("--expected-sid")) throw new InvalidOperationException("Different Windows account; no changes made.");
        if (Process.GetProcessesByName("Naufal Windows Utility").Length != 0 || Process.GetProcessesByName("Naufal Windows Powertoys").Length != 0)
            throw new InvalidOperationException("Close the utility before guest acceptance testing.");
        string stage = Argument("--audit-stage");
        if (stage is not ("Prepare" or "Resume")) throw new ArgumentException("Use --audit-stage Prepare or Resume.");
        string folder = OfflineImagePolicy.LocalPath(Argument("--report-directory"));
        OfflineImagePolicy.NoReparseAncestors(folder);
        string local = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)) + Path.DirectorySeparatorChar;
        if (!folder.StartsWith(local, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Use a guest-local report directory under this account's LocalAppData.");
        var platform = new SecurityMitigationNative();
        var security = platform.Read();
        Identity now = ReadIdentity(vm, sid, security);
        Directory.CreateDirectory(folder);
        // Exclusive run lock prevents two guest audit processes racing on the same manifest.
        using FileStream gate = new(Path.Combine(folder, "audit.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        string manifestPath = Path.Combine(folder, "security-storage-manifest.json");
        string logPath = Path.Combine(folder, "security-storage-result.txt");
        string backupPath = Path.Combine(folder, "hvci-original.json");
        void Log(string value) { Console.WriteLine(value); using FileStream f = new(logPath, FileMode.Append, FileAccess.Write, FileShare.Read); byte[] b = System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToString("O") + " " + value + Environment.NewLine); f.Write(b); f.Flush(true); }
        void Save(Manifest value) => AtomicJson(manifestPath, value);
        void Evidence(string label, SecurityMitigationSnapshot value) => AtomicJson(Path.Combine(folder, label + "-security.json"), value);
        var runner = new StorageNativeRunner();
        var storage = new StorageServicing(runner, Path.Combine(folder, "servicing"));
        var store = new SecurityMitigationFileStore(backupPath);

        void Complete(Manifest saved, SecurityMitigationSnapshot actual, string featureState)
        {
            if (!saved.SecurityChanged && File.Exists(backupPath))
                throw new InvalidDataException("Unexpected security backup for a no-change stage. Preserve evidence for review.");
            bool exactConfiguration = actual.HvciEnabled == saved.Security.HvciEnabled;
            bool sameRuntime = RuntimeMatches(saved.Security, actual);
            bool sameLsa = saved.Security.Lsa is { ReadError.Length: 0, EffectiveProtectionLevel: not null } && actual.Lsa is { ReadError.Length: 0, EffectiveProtectionLevel: not null } &&
                saved.Security.Lsa.RunAsPpl == actual.Lsa.RunAsPpl && saved.Security.Lsa.EffectiveProtectionLevel == actual.Lsa.EffectiveProtectionLevel;
            string summary = $"HVCI configuration baseline={exactConfiguration}; DeviceGuard effective baseline={sameRuntime}; LSA read-only baseline={sameLsa}; HVCI changed={saved.SecurityChanged}; HVCI enabled after reboot={saved.AppliedEffective?.ToString() ?? "NotExercised"}; Storage={saved.StorageOutcome}, current baseline={featureState == saved.StorageOriginal}. No coverage claimed for other security/storage controls or WIM deployment.";
            bool verified = exactConfiguration && sameRuntime && featureState == saved.StorageOriginal && (!saved.SecurityChanged || saved.AppliedEffective == true);
            saved = saved with { Phase = !verified ? "CompletedWithUnverifiedEvidence" : saved.SecurityChanged ? "CompletedScopedEvidence" : "CompletedWithSkippedControls", LastBoot = now.Boot, Note = summary }; Save(saved);
            Log(summary);
            if (!saved.SecurityChanged) Log("HVCI Apply/rollback NOT EXERCISED. No additional reboot is needed for this audit. LSA was read-only.");
            if (!verified) throw new InvalidOperationException("Configured/runtime evidence did not fully verify. See retained manifest; do not count this as a global pass.");
        }

        async Task BeginSecurity(Manifest m)
        {
            var fresh = platform.Read();
            if (fresh.HvciEnabled != m.Security.HvciEnabled || fresh.MachineIdentity != m.Security.MachineIdentity || !RuntimeMatches(m.Security, fresh))
                throw new InvalidOperationException("Security baseline changed before security stage; no security writes permitted.");
            string block = SecurityMitigationPolicy.BlockReason(fresh, true);
            if (block.Length == 0 && m.Security.HvciEnabled != 1)
            {
                store.Save(new(m.Security.MachineIdentity, m.Security.HvciEnabled, "GuestAuditOriginal", DateTimeOffset.UtcNow));
                m = m with { Phase = "SecurityEnableStarted", SecurityChanged = true, Note = "Original HVCI value/absence retained before enable attempt." }; Save(m);
                var applied = SecurityMitigationPolicy.Execute(SecurityMitigationAction.EnableMemoryIntegrity, platform, store);
                Log("HVCI ENABLE: " + applied.Outcome + "; " + applied.Message);
                if (!applied.Verified || !applied.Changed || platform.ReadMemoryIntegrity() != 1)
                {
                    m = m with { Phase = "SecurityNeedsReview", Note = "Enable unverified. Original retained; do not start a new baseline." }; Save(m);
                    throw new InvalidOperationException(m.Note);
                }
            }
            else
            {
                Log("HVCI NOT EXERCISED: " + (block.Length == 0 ? "Already enabled; no weakening toggle is performed for coverage." : block));
                string pending = await PendingReason(runner);
                if (pending.Length != 0) throw new InvalidOperationException(pending);
                Complete(m, fresh, await ReadFeature(runner));
                return;
            }
            m = m with { Phase = "AwaitAppliedBoot", LastBoot = now.Boot, Note = "Restart the disposable guest manually, then Resume with this same report directory." }; Save(m);
            Log(m.Note);
        }

        if (stage == "Prepare")
        {
            if (File.Exists(manifestPath) || File.Exists(backupPath) || File.Exists(logPath))
                throw new InvalidOperationException("Existing acceptance evidence found. Use Resume or inspect/revert the checkpoint; never overwrite a failed phase.");
            var initialStorage = await ReadFeature(runner);
            Manifest m = new(1, now, "Prepared", security, initialStorage, "NotExercised", false, null, now.Boot, "Baseline durable; no mutation yet.");
            Save(m); Evidence("baseline", security);
            Log("Scoped guest acceptance: only Print to PDF and eligible HVCI enable/restore. LSA is read-only. No reboot is requested automatically.");
            Log(SecurityMitigationPolicy.Report(security));
            try
            {
                string pending = await PendingReason(runner);
                if (initialStorage.Contains("Pending", StringComparison.OrdinalIgnoreCase)) pending = "Print to PDF has a pending servicing state; no acceptance mutation permitted.";
                if (pending.Length != 0)
                {
                    m = m with { Phase = "Blocked", Note = pending }; Save(m);
                    throw new InvalidOperationException(pending);
                }
                if (initialStorage == "Enabled")
                {
                    m = m with { Phase = "StorageDisableStarted", Note = "Original Enabled. Recovery: restore exact Enabled; payload is retained." }; Save(m);
                    var disabled = await storage.ChangeAsync(new(StorageItemKind.Feature, Feature, "Enabled"), false);
                    Log("STORAGE DISABLE: " + disabled.Outcome + "; " + disabled.LogPath);
                    if (!AcceptedStorageChange(disabled.Outcome, await ReadFeature(runner), false))
                    {
                        m = m with { Phase = "StorageNeedsReview", StorageOutcome = disabled.Outcome, Note = "Stop: inspect servicing log before rollback. Pending changes require manual review/reboot in clone." }; Save(m);
                        throw new InvalidOperationException(m.Note);
                    }
                    m = m with { Phase = "AwaitStorageDisabledBoot", StorageOutcome = "DisableAwaitingBoot", LastBoot = now.Boot,
                        Note = "Print to PDF disable accepted, not yet post-boot verified. Restart the clone manually, then Resume this directory." }; Save(m);
                    Log(m.Note);
                    return;
                }
                else Log("STORAGE NOT EXERCISED: baseline is " + initialStorage + "; only an initially Enabled Print to PDF feature is eligible.");

                await BeginSecurity(m);
            }
            catch (Exception ex) { Log("STOP: " + ex.Message + ". Manifest and recovery evidence retained; no automatic retry or reboot."); throw; }
            return;
        }

        if (!File.Exists(manifestPath)) throw new InvalidOperationException("No prepared manifest found.");
        Manifest saved = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath)) ?? throw new InvalidDataException("Invalid manifest.");
        string blocked = ResumeBlock(saved, now);
        if (blocked.Length != 0) throw new InvalidOperationException(blocked);
        Evidence(saved.Phase, security);
        Log("RESUME: " + saved.Phase + "; boot " + now.Boot);
        Log(SecurityMitigationPolicy.Report(security));
        string featureState = await ReadFeature(runner);
        Log("POST-BOOT STORAGE: " + featureState + "; original " + saved.StorageOriginal + "; roundtrip " + saved.StorageOutcome);
        string pendingAfterBoot = await PendingReason(runner);
        string expectedFeature = saved.Phase == "AwaitStorageDisabledBoot" ? "Disabled" : saved.StorageOriginal;
        if (pendingAfterBoot.Length != 0 || featureState != expectedFeature)
        {
            saved = saved with { Phase = "PostBootNeedsReview", Note = pendingAfterBoot.Length != 0 ? pendingAfterBoot : "Storage no longer matches its exact baseline." }; Save(saved);
            throw new InvalidOperationException(saved.Note);
        }
        if (saved.Phase == "AwaitStorageDisabledBoot")
        {
            saved = saved with { Phase = "StorageRestoreStarted", LastBoot = now.Boot, StorageOutcome = "DisabledAfterBootVerified" }; Save(saved);
            var restored = await storage.ChangeAsync(new(StorageItemKind.Feature, Feature, "Disabled"), true);
            Log("STORAGE RESTORE: " + restored.Outcome + "; " + restored.LogPath);
            if (!AcceptedStorageChange(restored.Outcome, await ReadFeature(runner), true))
            {
                saved = saved with { Phase = "StorageNeedsReview", Note = "Restore unverified. Inspect evidence/revert clone; no retry." }; Save(saved);
                throw new InvalidOperationException(saved.Note);
            }
            saved = saved with { Phase = "AwaitStorageRestoredBoot", StorageOutcome = "RestoreAwaitingBoot",
                Note = "Restore accepted. Restart clone manually and Resume to verify Enabled after boot." }; Save(saved);
            Log(saved.Note);
            return;
        }
        if (saved.Phase == "AwaitStorageRestoredBoot")
        {
            saved = saved with { Phase = "StorageRestored", StorageOutcome = "PostBootRoundTripVerified", LastBoot = now.Boot }; Save(saved);
            Log("Print to PDF: Disabled and exact Enabled baseline each verified on a separate boot. This does not test printing a document.");
            await BeginSecurity(saved);
            return;
        }
        if (saved.Phase == "AwaitAppliedBoot" && saved.SecurityChanged)
        {
            var original = store.Load();
            if (original is null || original.MachineIdentity != saved.Original.Machine || original.OriginalValue != saved.Security.HvciEnabled)
                throw new InvalidDataException("Private HVCI backup and manifest disagree. No automatic restore.");
            bool effective = security.EvidenceError.Length == 0 && security.HvciEnabled == 1 && security.VbsStatus == 2 && security.Running?.Contains(2) == true;
            Log("POST-BOOT HVCI ENABLE effective=" + effective + ". Registry alone is not effective-protection evidence.");
            saved = saved with { Phase = "SecurityRestoreStarted", AppliedEffective = effective, LastBoot = now.Boot, Note = "Restoring exact original HVCI value/absence through unchanged production gates." }; Save(saved);
            var restored = SecurityMitigationPolicy.Execute(SecurityMitigationAction.RestoreMemoryIntegrity, platform, store);
            Log("HVCI RESTORE: " + restored.Outcome + "; " + restored.Message);
            if (!restored.Verified || platform.ReadMemoryIntegrity() != saved.Security.HvciEnabled)
            {
                saved = saved with { Phase = "SecurityNeedsReview", Note = "HVCI restore blocked/unverified. Inspect original snapshot and policy, or revert clone checkpoint; no policy bypass." }; Save(saved);
                throw new InvalidOperationException(saved.Note);
            }
            saved = saved with { Phase = "AwaitRestoredBoot", Note = "Exact configuration restored; restart the guest manually a second time, then Resume to compare effective baseline." }; Save(saved);
            Log(saved.Note);
            return;
        }
        Complete(saved, security, featureState);
    }

    private static Identity ReadIdentity(Guid vm, string sid, SecurityMitigationSnapshot security)
    {
        var rows = NativeHardwareData.Query(@"ROOT\CIMV2", "Win32_OperatingSystem", "LastBootUpTime");
        string? boot = rows.Count == 1 ? rows[0].GetValueOrDefault("LastBootUpTime") : null;
        if (string.IsNullOrWhiteSpace(boot) || security.WindowsBuild < 19041 || string.IsNullOrWhiteSpace(security.MachineIdentity))
            throw new InvalidOperationException("Exact boot/build/machine identity unavailable; no mutation allowed.");
        return new(vm, sid, security.MachineIdentity, security.WindowsBuild, boot);
    }

    private static async Task<string> ReadFeature(IStorageCommandRunner runner)
    {
        var read = await runner.RunAsync(StorageServicing.InfoCommand(new(StorageItemKind.Feature, Feature, "")));
        if (read.ExitCode != 0 || read.TimedOut || StorageServicing.ReadState(read.StandardOutput) is not { Length: > 0 } state)
            throw new InvalidOperationException("Print to PDF state cannot be verified; no mutation allowed. " + read.CombinedOutput);
        return state;
    }

    private static async Task<string> PendingReason(IStorageCommandRunner runner)
    {
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        foreach (string path in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\PackagesPending", @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired" })
        { using var key = hive.OpenSubKey(path); if (key is not null) return "Pending Windows servicing/reboot registry evidence: " + path; }
        using var session = hive.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
        if (session?.GetValue("PendingFileRenameOperations") is not null) return "Pending file operations require reboot/review before this test.";
        var packages = await runner.RunAsync(StorageServicing.Dism("/Get-Packages"));
        if (packages.ExitCode != 0 || packages.TimedOut) return "Package pending-state inventory unavailable; no servicing test allowed.";
        if (packages.StandardOutput.Split('\n').Any(line => line.TrimStart().StartsWith("State", StringComparison.OrdinalIgnoreCase) && line.Contains("Pending", StringComparison.OrdinalIgnoreCase)))
            return "Pending package servicing detected; finish it and reboot before a fresh audit.";
        return "";
    }

    private static void AtomicJson<T>(string path, T value)
    {
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (FileStream stream = new(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, value, new JsonSerializerOptions { WriteIndented = true }); stream.Flush(true); }
        File.Move(temp, path, true);
    }
}
