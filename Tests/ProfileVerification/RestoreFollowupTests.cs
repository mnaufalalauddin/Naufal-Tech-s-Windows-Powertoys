using Microsoft.Win32;
using Naufal_Windows_Tech_s_Powertoys;
using System.Text.Json;
using System.Security.AccessControl;

internal static class RestoreFollowupTests
{
    internal static void Run(Action<bool, string> assert)
    {
        void Throws<T>(Action action, string label) where T : Exception
        {
            try { action(); } catch (T) { assert(true, label); return; }
            throw new Exception("Expected " + typeof(T).Name + ": " + label);
        }

        Dictionary<string, string> Row(string type, string store = "ActiveStore") =>
            new() { ["Type"] = type, ["PolicyStore"] = store };
        for (int type = 0; type <= 7; type++)
        {
            int parsed = TeredoConfiguration.ReadActiveType(new[] { Row(type.ToString()) });
            var state = TeredoConfiguration.ToState(parsed);
            assert(state.IsAvailable && state.IsOn == (type == 4), "Teredo enum " + type);
            assert(TeredoConfiguration.MatchesTarget(state, false) == (type == 0), "only Default is restored " + type);
            assert(TeredoConfiguration.MatchesTarget(state, true) == (type == 4), "only Disabled is applied " + type);
        }
        assert(TeredoConfiguration.ReadActiveType(new[] { Row("0"), Row("4", "PersistentStore") }) == 0,
            "effective policy read independent of other stores");
        foreach (var rows in new[] { Array.Empty<Dictionary<string, string>>(), new[] { Row("0"), Row("0") },
            new[] { Row("disabled") }, new[] { Row("8") }, new[] { Row("0", "PersistentStore") },
            new[] { new Dictionary<string, string> { ["PolicyStore"] = "ActiveStore" } } })
            Throws<InvalidDataException>(() => TeredoConfiguration.ReadActiveType(rows), "unreadable configuration is not verified");
        assert(!TeredoConfiguration.MatchesTarget(new(false, false, "Configured type: Default"), false), "Teredo read failure remains failure");

        string Json(string service, string start = "2", string delayed = "null", string running = "true") => $$"""
            { "__KentangSvc_{{service}}": { "RequestedName":"{{service}}", "Name":"{{service}}",
              "Start":{"Exists":true,"Value":{{start}},"Type":"DWord"},
              "DelayedAutoStart":{"Exists":{{(delayed == "null" ? "false" : "true")}},"Value":{{delayed}},"Type":"DWord"},
              "WasRunning":{{running}} } }
            """;
        PreviousServiceSnapshot? Parse(string json, string service)
        {
            using var document = JsonDocument.Parse(json);
            return PreviousServiceSnapshot.Parse(document.RootElement, service);
        }
        foreach (string service in new[] { "Ndu", "jhi_service", "TrkWks" })
        {
            var saved = Parse(Json(service), service)!;
            var plan = ServiceRestoreSnapshot.CreatePlan(saved.Start, saved.Delayed, saved.WasRunning, null);
            assert(plan.TargetStart == 2 && plan.ShouldStart && plan.VerifyRuntime && !plan.ShouldStop,
                service + " original Automatic AND Running must be restored");
            assert(!plan.UsesDocumentedDefault && saved.Delayed is null, "no guessed vendor/driver default");
            assert(PreviousServiceSnapshot.IsConsumed(saved, saved.Fingerprint), "same old snapshot cannot be replayed after success");
            assert(PreviousServiceSnapshot.PrepareForRestore(saved, saved.Fingerprint).AlreadyRestored, "repeat restore is verification-only");
            assert(!PreviousServiceSnapshot.PrepareForRestore(saved, null).AlreadyRestored, "unconsumed restore permits mutation");
            assert(!PreviousServiceSnapshot.IsConsumed(saved, null), "unconsumed snapshot usable");
            assert(Parse(Json(service), service.ToUpperInvariant())!.Fingerprint == saved.Fingerprint, "case-stable receipt");
            var changed = Parse(Json(service, "3", "0", "false"), service)!;
            assert(!PreviousServiceSnapshot.IsConsumed(changed, saved.Fingerprint), "changed source snapshot is distinct");
            plan = ServiceRestoreSnapshot.CreatePlan(changed.Start, changed.Delayed, changed.WasRunning, null);
            assert(plan.TargetStart == 3 && plan.ShouldStop && plan.VerifyRuntime, "exact Manual/Stopped also retained");
        }
        string valid = Json("Ndu");
        assert(Parse("{}", "Ndu") is null && Parse(valid, "missing") is null, "missing entry is not fabricated");
        foreach (string invalid in new[] { Json("Ndu", "0"), Json("Ndu", "1"), Json("Ndu", "9"), Json("Ndu", "\"2\""),
            Json("Ndu", "2", "2"), Json("Ndu", "2", "null", "null"),
            valid.Replace("\"Name\":\"Ndu\"", "\"Name\":\"Other\""),
            valid.Replace("\"WasRunning\":true", "\"WasRunning\":true,\"WasRunning\":false"),
            valid.Replace("\"Type\":\"DWord\"", "\"Type\":\"String\""),
            valid.Replace("\"Start\":", "\"MissingStart\":"),
            valid[..^1] + "," + valid[1..], "[]" })
            Throws<InvalidDataException>(() => Parse(invalid, "Ndu"), "invalid old snapshot blocked before writes");
        var original = Parse(valid, "Ndu")!;
        var unrelatedChange = Parse(valid[..^1] + ",\"unrelated\":42}", "Ndu")!;
        assert(unrelatedChange.Fingerprint == original.Fingerprint, "unrelated JSON changes cannot resurrect consumed snapshot");

        const string dacl = "D:AI(A;;FA;;;SY)(A;;FR;;;BU)";
        const string full = "O:SYG:SYD:AI(A;;FA;;;SY)(A;;FR;;;BU)S:AI(AU;SA;FA;;;WD)";
        assert(StoreDaclSnapshot.Matches(full, dacl), "legacy full descriptor vs actual DACL equivalent");
        assert(StoreDaclSnapshot.Matches(full, "O:BAG:BAD:(A;;FA;;;SY)(A;;FR;;;BU)"), "unmodified owner/group/audit/auto-inherit metadata ignored");
        assert(StoreDaclSnapshot.AccessSddl(full) == dacl, "only saved DACL is restored");
        foreach (string actual in new[] { "D:PAI(A;;FA;;;SY)(A;;FR;;;BU)", "D:AI(A;;FA;;;SY)(A;;FA;;;BU)",
            "D:AI(A;;FA;;;SY)(A;;FR;;;WD)", "D:AI(A;;FR;;;BU)(A;;FA;;;SY)",
            "D:AI(D;;FA;;;WD)(A;;FA;;;SY)(A;;FR;;;BU)", "D:AI(A;CI;FA;;;SY)(A;;FR;;;BU)", "D:" })
            assert(!StoreDaclSnapshot.Matches(full, actual), "real permission difference remains failure: " + actual);
        Throws<InvalidDataException>(() => StoreDaclSnapshot.AccessSddl("O:SYG:SY"), "no DACL snapshot blocked");
        Throws<InvalidDataException>(() => StoreDaclSnapshot.AccessSddl("D:NO_ACCESS_CONTROL"), "null DACL snapshot blocked");

        object? value = null;
        RegistryValueKind? kind = null;
        int writes = 0;
        void DeniedWrite() { writes++; throw new UnauthorizedAccessException(); }
        assert(!RegistryRestoreWrite.Apply(() => (value, kind), null, null, DeniedWrite) && writes == 0,
            "absent DelayedAutoStart does not open protected key for write");
        value = "2"; kind = RegistryValueKind.DWord;
        assert(!RegistryRestoreWrite.Apply(() => (value, kind), "2", RegistryValueKind.DWord, DeniedWrite) && writes == 0,
            "already correct DWord does not require write access");
        Throws<UnauthorizedAccessException>(() => RegistryRestoreWrite.Apply(() => (value, kind), "3", RegistryValueKind.DWord, DeniedWrite),
            "necessary write access denial is still an error");
        Throws<InvalidOperationException>(() => RegistryRestoreWrite.Apply(() => (value, kind), "3", RegistryValueKind.DWord, () => { }),
            "write without readback change is still an error");
        assert(RegistryRestoreWrite.Apply(() => (value, kind), "3", RegistryValueKind.DWord, () => value = "3"), "real write verified");
        kind = RegistryValueKind.String;
        assert(RegistryRestoreWrite.Apply(() => (value, kind), "3", RegistryValueKind.DWord, () => kind = RegistryValueKind.DWord), "kind mismatch also repaired");
        assert(RegistryRestoreWrite.Apply(() => (value, kind), null, null, () => { value = null; kind = null; }), "existing delayed field removed and verified");
        Throws<UnauthorizedAccessException>(() => RegistryRestoreWrite.Apply(() => throw new UnauthorizedAccessException(), null, null, () => { }),
            "unreadable registry is not absent");

        var automatic = ServiceRestoreSnapshot.CreateWindowsDefaultPlan(2, null);
        assert(!ServiceRestoreSnapshot.NeedsStartupConfiguration(2, null, automatic), "already Automatic does not reconfigure protected key");
        assert(!ServiceRestoreSnapshot.NeedsStartupConfiguration(2, 0, automatic), "non-delayed Automatic does not need redundant sc config");
        assert(ServiceRestoreSnapshot.NeedsStartupConfiguration(2, 1, automatic), "delayed Automatic is really different");
        assert(ServiceRestoreSnapshot.NeedsStartupConfiguration(4, null, automatic), "Disabled still must be configured");
        assert(ServiceRestoreSnapshot.NeedsStartupConfiguration(null, null, automatic), "unreadable Start is not verified");
        assert(automatic.ShouldStart && automatic.VerifyRuntime, "skipping config never skips the required service start/verification");

        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string services = File.ReadAllText(Path.Combine(root, "DebloatServiceGroupsService.cs"));
        int smart = services.IndexOf("private async Task<bool> RestoreSmartAsync", StringComparison.Ordinal);
        int buildPlan = services.IndexOf("private static IReadOnlyList<ServiceRestoreWorkItem> BuildRestorePlan", smart, StringComparison.Ordinal);
        string restore = services[smart..buildPlan];
        assert(restore.IndexOf("VerifyRestorePlanAsync", StringComparison.Ordinal) < restore.IndexOf("MarkRestored", StringComparison.Ordinal), "consume legacy entry only after verification");
        assert(restore.IndexOf("PreviousSnapshot?.AlreadyRestored == true", StringComparison.Ordinal) < restore.IndexOf("ConfigureServiceStartupAsync", StringComparison.Ordinal), "consumed snapshot cannot mutate on repeated Restore");
        assert(services.Contains("PreviousServiceSnapshot.ReadForRestore(serviceName)") && services.Contains("previous.WasRunning, null"), "service backend wired to strict original snapshot");
        assert(services.Contains("RegistryRestoreWrite.Apply") && services.Contains("NeedsStartupConfiguration") && services.Contains("WaitForServiceStateAsync(serviceName, expected)"), "no-op writes retain real Running verification");
        string network = File.ReadAllText(Path.Combine(root, "DebloatNetworkStorageService.cs"));
        assert(network.Contains("MSFT_NetTeredoConfiguration") && network.Contains("TeredoConfiguration.MatchesTarget(after, targetOn)") &&
            network.Contains("TeredoConfiguration.MatchesTarget(after, disabled: false)"), "both Teredo restore routes use configured policy");
        string essential = File.ReadAllText(Path.Combine(root, "EssentialActionsService.cs"));
        int beginAcl = essential.IndexOf("private static Task<ToolActionResult> ApplyStoreSearchBlockAsync", StringComparison.Ordinal);
        int endAcl = essential.IndexOf("private static string GetStoreDatabasePath", beginAcl, StringComparison.Ordinal);
        string aclMethods = essential[beginAcl..endAcl];
        assert(!aclMethods.Contains("AccessControlSections.All") && aclMethods.Contains("StoreDaclSnapshot.Matches"), "Store restore changes/verifies DACL only");
        assert(aclMethods.IndexOf("backup.SetValue(\"Sddl\"", StringComparison.Ordinal) < aclMethods.IndexOf("backup.SetValue(\"Captured\"", StringComparison.Ordinal), "Store snapshot commits last");
    }

    internal static void ProbeReadOnly()
    {
        int type = TeredoConfiguration.ReadActiveType(NativeHardwareData.Query(
            @"ROOT\StandardCimv2", "MSFT_NetTeredoConfiguration", "Type", "PolicyStore"));
        Console.WriteLine("Teredo native configured type: " + TeredoConfiguration.TypeName(type));
        foreach (string service in new[] { "Ndu", "jhi_service" })
        {
            var saved = PreviousServiceSnapshot.ReadForRestore(service);
            Console.WriteLine(saved is null ? service + ": no previous snapshot" :
                $"{service}: saved Start={saved.Start}, Delayed={saved.Delayed?.ToString() ?? "absent"}, WasRunning={saved.WasRunning}, AlreadyRestored={saved.AlreadyRestored}");
        }
        using RegistryKey? backup = Registry.CurrentUser.OpenSubKey(@"Software\Naufal Windows Tech\Powertoys\Backups\EssentialActions\StoreSearch");
        if (backup?.GetValue("Sddl") is string sddl && backup.GetValue("Path") is string path)
        {
            string actual = FileSystemAclExtensions.GetAccessControl(new FileInfo(path), AccessControlSections.Access)
                .GetSecurityDescriptorSddlForm(AccessControlSections.Access);
            Console.WriteLine("Store saved/current DACL matches: " + StoreDaclSnapshot.Matches(sddl, actual));
        }
        else Console.WriteLine("Store: no saved ACL snapshot");
        Console.WriteLine("Read-only production helper probe complete. No state changed.");
    }
}
