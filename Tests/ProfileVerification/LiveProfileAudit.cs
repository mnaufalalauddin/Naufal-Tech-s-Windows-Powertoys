using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Text.Json;
using Naufal_Windows_Tech_s_Powertoys;

// Explicit opt-in ONLY. Normal regression runs never call this class.
internal static class LiveProfileAudit
{
    internal static async Task RunAsync(string[] args)
    {
        string folder = Path.GetFullPath(args[Array.IndexOf(args, "--report-directory") + 1]);
        Directory.CreateDirectory(folder);
        string log = Path.Combine(folder, "result.txt");
        void Log(string value) => File.AppendAllText(log, value + Environment.NewLine);
        string Json(object? value) => JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
        object Invoke(Type type, object? instance, string name, params object?[] values) =>
            type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!.Invoke(instance, values)!;
        async Task<object> Capture(PerformanceProfileExtendedService service, string guid)
        {
            Task task = (Task)Invoke(typeof(PerformanceProfileExtendedService), service, "CaptureSnapshotAsync", guid);
            await task;
            return task.GetType().GetProperty("Result")!.GetValue(task)!;
        }
        try
        {
            if (!PerformanceProfileService.IsAdministrator()) throw new InvalidOperationException("Administrator token required. No settings changed.");
            string sid = WindowsIdentity.GetCurrent().User!.Value;
            if (sid != args[Array.IndexOf(args, "--expected-sid") + 1]) throw new InvalidOperationException("Different Windows account: refusing to test.");
            if (Process.GetProcessesByName("Naufal Windows Powertoys").Length != 0 || Process.GetProcessesByName("Naufal Windows Utility").Length != 0)
                throw new InvalidOperationException("Close Powertoys before the live test. No settings changed.");
            Log("Elevated, same-account live profile audit: " + DateTimeOffset.Now);
            var live = new GamingLiveStatusService();
            var before = await live.ReadSnapshotAsync();
            Log("Before: " + before.PerformanceProfile?.DisplayText);
            if (before.Bcd.ExitCode != 0) throw new InvalidOperationException("BCD preflight failed: " + before.Bcd.StandardError + before.Bcd.StandardOutput);
            Log("BCD read: PASS (elevated). " + before.Bcd.StandardOutput);
            var export = await new NativeCommandRunner().RunAsync("bcdedit.exe", ["/export", Path.Combine(folder, "bcd-original")], TimeSpan.FromSeconds(30));
            if (export.ExitCode != 0) throw new InvalidOperationException("BCD backup failed; no settings changed: " + export.CombinedOutput);
            var extended = new PerformanceProfileExtendedService();
            object mmcss = Invoke(typeof(PerformanceProfileService), null, "CaptureMmcssSnapshot");
            var known = PerformanceProfileVerificationService.ReadKnownPowerGuids();
            string[] targets = PerformanceProfileVerification.Profiles.Select(profile =>
                PerformanceProfileVerificationService.ResolveTarget(profile, before.PowerCfgList, known))
                .Append(before.PowerPlanGuid).Where(guid => guid is not null).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var snapshots = new Dictionary<string, object>();
            foreach (string target in targets) snapshots[target] = await Capture(extended, target);
            File.WriteAllText(Path.Combine(folder, "original-state.json"), Json(new { before.PowerPlanGuid, Mmcss = mmcss, Extended = snapshots }));
            string baseline = Json(new { Mmcss = mmcss, Extended = snapshots });
            string historyPath = PerformanceProfileTransaction.StatePath;
            byte[]? history = File.Exists(historyPath) ? File.ReadAllBytes(historyPath) : null;
            if (history is not null) File.WriteAllBytes(Path.Combine(folder, "transaction-original.json"), history);
            try
            {
                foreach (PerformanceProfileKind profile in Enum.GetValues<PerformanceProfileKind>())
                {
                    bool reached = false;
                    var service = new PerformanceProfileService
                    {
                        VerifiedAuditCheckpoint = verification =>
                        {
                            Log($"APPLY {verification.Profile}: {verification.Matched}/{verification.Total}; Verified={verification.Verified}");
                            reached = verification.Verified;
                            // Controlled failure after real writes AND real 23-check verification.
                            // Exercises the production extended + outer rollback boundaries.
                            throw new InvalidOperationException("Intentional live-audit rollback checkpoint.");
                        }
                    };
                    var result = await service.ApplyAsync(profile);
                    Log(result.Message);
                    var restored = new Dictionary<string, object>();
                    foreach (string target in targets) restored[target] = await Capture(extended, target);
                    object restoredMmcss = Invoke(typeof(PerformanceProfileService), null, "CaptureMmcssSnapshot");
                    var after = await live.ReadSnapshotAsync();
                    bool exact = baseline == Json(new { Mmcss = restoredMmcss, Extended = restored }) && before.PowerPlanGuid == after.PowerPlanGuid;
                    Log($"ROLLBACK {profile}: exact baseline={exact}; actual={after.PerformanceProfile?.DisplayText}");
                    if (!exact || !result.RolledBack || !reached)
                        throw new InvalidOperationException("Live profile round-trip failed. Further profiles were not started.");
                }
                Log("PASS: all three profiles reached 23/23 and each intentional failure restored the exact captured baseline.");
            }
            finally
            {
                // Keep diagnostic results in this audit directory; do not replace the user's history.
                if (history is not null) File.WriteAllBytes(historyPath, history);
                else if (File.Exists(historyPath)) File.Delete(historyPath);
            }
        }
        catch (Exception exception)
        {
            Log("FAIL: " + exception);
            Environment.ExitCode = 1;
        }
    }
}
