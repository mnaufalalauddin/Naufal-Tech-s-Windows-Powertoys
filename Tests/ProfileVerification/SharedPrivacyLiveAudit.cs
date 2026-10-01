using Microsoft.Win32;
using System.Diagnostics;
using System.Text.Json;
using Naufal_Windows_Tech_s_Powertoys;

// Never called by normal regressions. Only an explicitly identified disposable VirtualBox guest.
internal static class SharedPrivacyLiveAudit
{
    internal static void Run(string[] args)
    {
        string Argument(string name)
        {
            int index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing " + name);
            return args[index + 1];
        }
        Guid expected = GuestAuditIdentity.Require(args);
        if (Process.GetProcessesByName("Naufal Windows Utility").Length != 0)
            throw new InvalidOperationException("Close the application before the VM audit.");
        string folder = Path.GetFullPath(Argument("--report-directory"));
        Directory.CreateDirectory(folder);
        string log = Path.Combine(folder, "shared-privacy-result.txt");
        if (File.Exists(log)) throw new InvalidOperationException("Use a fresh report directory.");
        void Log(string text) => File.AppendAllText(log, text + Environment.NewLine);
        // A clone can legitimately contain the user's production snapshots.
        // Exercise the same native implementation in fresh test-owned storage;
        // never rename, clear, import or consume those production backups.
        var store = SharedPrivacySnapshotStore.CreateIsolatedAudit(Guid.NewGuid());
        string root = store.AuditBackupRoot;
        using (var existingRoot = Registry.CurrentUser.OpenSubKey(root))
            if (existingRoot is not null) throw new InvalidOperationException("Audit root already exists. No settings changed.");
        string[] ownedPaths = [root + @"\Essential\Telemetry", root + @"\Debloat\AdvertisingId",
            root + @"\Debloat\TailoredExperiences", root + @"\SharedPrivacy\AdvertisingId", root + @"\SharedPrivacy\TailoredExperiences"];
        foreach (string path in ownedPaths)
        {
            using var existing = Registry.CurrentUser.OpenSubKey(path);
            if (existing is not null) throw new InvalidOperationException("An existing snapshot is present. Audit blocked without modifying it: " + path);
        }
        var targets = SharedPrivacySnapshot.Targets.Select(t => new RestoreRegistryTarget(RegistryHive.CurrentUser, t.Path, t.Name)).ToArray();
        var baseline = targets.Select(t =>
        {
            var value = RegistryRestorePlan.ReadNative(t);
            return new RestoreRegistryValue(t, value.Value, value.Kind);
        }).ToArray();
        int changedTargets = baseline.Count(b => b.Kind != RegistryValueKind.DWord || !Equals(b.Value, 0));
        // The report is durable before native writes; the VM baseline checkpoint is a separate recovery layer.
        File.WriteAllText(Path.Combine(folder, "shared-privacy-original.json"), JsonSerializer.Serialize(baseline, new JsonSerializerOptions { WriteIndented = true }));
        Log("VM-only shared snapshot audit: " + expected + "; " + DateTimeOffset.Now);
        Log("Isolated test backup root: HKCU\\" + root);
        Log("Production backups are not used or modified. Existing production-backup migration is NOT tested by this isolated run.");
        bool restored = false;
        try
        {
            store.Prepare("Telemetry", SharedSnapshotPreparation.Capture);
            RegistryRestorePlan.Execute(targets.Select(t => new RestoreRegistryValue(t, 0, RegistryValueKind.DWord)).ToArray());
            Log($"APPLY shared privacy primitives: both DWORDs read back as 0; {changedTargets}/{targets.Length} differed from baseline. Already-applied targets are not mutation-coverage evidence.");
            foreach (var target in SharedPrivacySnapshot.Targets)
            {
                store.Prepare(target.Id, SharedSnapshotPreparation.Capture);
                using var backup = Registry.CurrentUser.OpenSubKey(root + @"\Debloat\" + target.Id)!;
                var original = baseline.Single(b => b.Target.Path == target.Path && b.Target.Name == target.Name);
                var saved = SharedRegistryOriginal.Read(k => backup.GetValue(k), target.DebloatTag);
                using var essential = Registry.CurrentUser.OpenSubKey(root + @"\Essential\Telemetry")!;
                if (saved != SharedRegistryOriginal.Read(k => essential.GetValue(k), target.EssentialTag))
                    throw new InvalidOperationException("Cross-catalog originals differ.");
                if (original.Value is null && saved!.Exists) throw new InvalidOperationException("Original absence was lost.");
            }
            Log("CROSS-CATALOG capture: both legacy mirrors retained the same first original, not the applied state.");
            foreach (var target in SharedPrivacySnapshot.Targets)
            {
                store.Prepare(target.Id, SharedSnapshotPreparation.Restore);
                using var backup = Registry.CurrentUser.OpenSubKey(root + @"\Debloat\" + target.Id)!;
                RegistryRestorePlan.RestoreTagged(k => backup.GetValue(k), target.DebloatTag,
                    new(RegistryHive.CurrentUser, target.Path, target.Name), (value, kind) => kind switch
                    {
                        RegistryValueKind.DWord => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
                        RegistryValueKind.QWord => long.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
                        RegistryValueKind.Binary => Convert.FromBase64String(value),
                        _ => value
                    });
            }
            foreach (var original in baseline)
            {
                var readback = RegistryRestorePlan.ReadNative(original.Target);
                if (!RegistryRestorePlan.Matches(readback.Value, original.Value) || readback.Kind != original.Kind)
                    throw new InvalidOperationException("Saved-snapshot rollback differs from the independent baseline.");
            }
            restored = true;
            Log("ROLLBACK: exact value / kind / absence verified against captured baseline.");
            // These exact backup leaves did not exist before this explicitly authorized guest test.
            using (var essentialRoot = Registry.CurrentUser.OpenSubKey(root + @"\Essential", true))
                essentialRoot?.DeleteSubKeyTree("Telemetry", false);
            store.RetireUnused("Telemetry");
            foreach (var target in SharedPrivacySnapshot.Targets)
            {
                using var retained = Registry.CurrentUser.OpenSubKey(root + @"\SharedPrivacy\" + target.Id);
                if (retained is null) throw new InvalidOperationException("Original retired while a second owner still existed.");
            }
            foreach (var target in SharedPrivacySnapshot.Targets)
            {
                using (var debloatRoot = Registry.CurrentUser.OpenSubKey(root + @"\Debloat", true))
                    debloatRoot?.DeleteSubKeyTree(target.Id, false);
                store.RetireUnused(target.Id);
                using var retired = Registry.CurrentUser.OpenSubKey(root + @"\SharedPrivacy\" + target.Id);
                if (retired is not null) throw new InvalidOperationException("Unused canonical snapshot was not retired.");
            }
            Log("PASS: real HKCU primitive Apply/readback/rollback and shared snapshot lifetime verified using isolated backup storage. This does not test existing production-backup migration, the full Telemetry bundle or services.");
        }
        catch (Exception ex)
        {
            Log("FAIL: " + ex);
            Environment.ExitCode = 1;
        }
        finally
        {
            if (!restored)
            {
                try { RegistryRestorePlan.Execute(baseline); Log("Recovery: exact captured registry baseline verified. Diagnostic snapshots retained."); }
                catch (Exception ex) { Log("RECOVERY FAILED: " + ex + " Use the isolated VM baseline checkpoint."); Environment.ExitCode = 1; }
            }
        }
    }
}
