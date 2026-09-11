using Naufal_Windows_Tech_s_Powertoys;
using System.Text;

internal static class ReauditSeptember9Tests
{
    internal static async Task RunAsync(Action<bool, string> assert)
    {
        Catalog(assert);
        Office(assert);
        await AdmissionAsync(assert);

        int callbacks = 0;
        var output = new Callback<string>(_ => { Interlocked.Increment(ref callbacks); throw new InvalidOperationException("synthetic observer fault"); });
        var result = await new NativeCommandRunner().RunAsync(Environment.ProcessPath!, ["--audit-child-flood"],
            TimeSpan.FromSeconds(8), outputProgress: output, outputEncoding: Encoding.UTF8);
        assert(!result.TimedOut && result.ExitCode == 0, "R09 failing observer does not stall or fail the actual command");
        assert(result.StandardOutput.EndsWith("OUT-END") && result.StandardOutput.Length >= 96 * 4096, "R09 stdout fully drained after observer failure");
        assert(result.StandardError.Contains("ERR-END") && result.StandardError.Contains("synthetic observer fault"), "R09 stderr drained and observer fault disclosed");
        assert(callbacks == 1, "R09 broken observer disabled after first exception across both streams");
        var failedCommand = await new NativeCommandRunner().RunAsync(Environment.ProcessPath!, ["--audit-child-flood", "--audit-exit-7"],
            TimeSpan.FromSeconds(8), outputProgress: output, outputEncoding: Encoding.UTF8);
        assert(!failedCommand.TimedOut && failedCommand.ExitCode == 7 && failedCommand.StandardOutput.EndsWith("OUT-END"),
            "R09 actual child failure is preserved despite observer failure");

        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string main = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        int admissionStart = main.IndexOf("private async Task<TaskActivityService.TaskActivityLease?> AcquireManagedTaskAsync", StringComparison.Ordinal);
        int admitted = main.IndexOf("if (lease is null)", admissionStart, StringComparison.Ordinal);
        string waiting = main[admissionStart..admitted];
        assert(waiting.Contains("TaskAdmission.WaitAsync") && !waiting.Contains("await ShowMessageDialogAsync"), "R09 queue message does not gate task admission");
        assert(main.Contains("setupLease = await AcquireManagedTaskAsync(") && main.Contains("\"FirstRun:Prerequisites\""), "R09 first-run work uses global task admission");
        int wizardStart = main.IndexOf("private async Task ShowFirstRunPrerequisiteWizardAsync", StringComparison.Ordinal);
        int wizardEnd = main.IndexOf("private static Border CreateFirstRunPrerequisiteRow", wizardStart, StringComparison.Ordinal);
        string wizard = main[wizardStart..wizardEnd];
        assert(wizard.Contains("\"SystemMutation\", \"AppxDeployment\", \"WindowsServicing\""), "R09 first-run conflicts with repair/package mutations");
        assert(wizard.Contains("setupLease?.Dispose()") && wizard.Contains("setupProgress?.Complete(false"), "R09 first-run failure settles progress and task lease");
        assert(wizard.IndexOf("if (_isClosed) return;", StringComparison.Ordinal) < wizard.IndexOf("TextBlock heading", StringComparison.Ordinal), "R09 no late wizard created after dashboard closes");
        string firstRun = File.ReadAllText(Path.Combine(root, "FirstRunPrerequisiteService.cs"));
        foreach (int stage in new[] { 2, 4, 5, 6, 7, 8 })
            assert(firstRun.Contains($"Start({stage},"), "R09 slow wizard stage reports running before completion " + stage);
        assert(firstRun.Contains("HttpCompletionOption.ResponseHeadersRead, downloadTimeout.Token") &&
            firstRun.Contains("ReadAsStreamAsync(downloadTimeout.Token)") && firstRun.Contains("null, downloadTimeout.Token"), "R09 wizard download cancellation covers headers and body");
        assert(firstRun.Contains("Guid.NewGuid()") && firstRun.Contains("AtomicDownloadFile.SaveAsync") && firstRun.Contains("!preserveBundle && File.Exists(bundlePath)"), "R09 isolated complete download and unconfirmed deployment file retention");
        string licensing = File.ReadAllText(Path.Combine(root, "LicensingInformationService.cs"));
        assert(!licensing.Contains(@"C:\Program Files") && licensing.Contains("OfficeScriptDiscovery.Candidates"), "R09 licensing discovery uses installed locations, not fixed C drive");
        assert(licensing.Contains("new NativeCommandRunner().RunAsync") && !licensing.Contains("ReadToEndAsync"), "R09 activation queries share bounded stream lifecycle");
        assert(licensing.Contains("\"/dstatus\"") && !licensing.Contains("\"/act\"") && !licensing.Contains("\"/ato\""), "R09 activation remains information-only");
        foreach (string file in new[] { "PerformanceProfileService.cs", "GamingStatusService.cs", "GamingLiveStatusService.cs", "SecurityInformationService.cs" })
        {
            string source = File.ReadAllText(Path.Combine(root, file));
            assert(source.Contains("new NativeCommandRunner().RunAsync") && !source.Contains("ReadToEndAsync"), "R09 bounded stream lifecycle shared by " + file);
        }
    }

    private static void Catalog(Action<bool, string> assert)
    {
        assert(!new CatalogProgressState([]).AllVerified, "R09 empty catalog is not verified");
        foreach (double percent in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -2, 0, 42.5, 100, 400 })
        {
            var state = new CatalogProgressState(["a"]);
            assert(!state.Update("unknown", "RUNNING", percent, "invalid id"), "R09 unknown progress id ignored");
            assert(state.Update("a", "RUNNING", percent, "work"), "R09 real progress accepted");
            assert(state.Items["a"].Percent == (double.IsFinite(percent) ? Math.Clamp(percent, 0, 100) : null), "R09 percent finite or indeterminate");
            assert(state.Update("a", "VERIFYING", null, "verify"), "R09 verification accepted");
            assert(!state.Update("a", "RUNNING", percent, "late worker") && state.Items["a"].Phase == "VERIFYING", "R09 late running callback cannot regress verification");
            assert(!state.Update("a", "unrecognized", percent, "unknown"), "R09 unknown phase ignored");
            assert(state.Update("a", "COMPLETED", 100, "verified"), "R09 verified row completes");
            foreach (string late in new[] { "RUNNING", "VERIFYING", "FAILED", "COMPLETED", "SKIPPED" })
                assert(!state.Update("a", late, percent, "late") && state.AllVerified, "R09 terminal immutable: " + late);
        }
    }

    private static void Office(Action<bool, string> assert)
    {
        var paths = OfficeScriptDiscovery.Candidates([@"D:\Apps", @"E:\Apps32", @"d:\apps", "", "relative", @"\\server\share"],
            [@"F:\Custom Office\Office16", @"G:\C2R\Microsoft Office", @"F:\Custom Office\Office16\", "C:relative", " "]);
        assert(paths.Contains(@"D:\Apps\Microsoft Office\root\Office16\ospp.vbs"), "R09 non-C 64-bit installation");
        assert(paths.Contains(@"E:\Apps32\Microsoft Office\Office16\ospp.vbs"), "R09 non-C 32-bit installation");
        assert(paths.Contains(@"D:\Apps\Microsoft Office\Office15\ospp.vbs"), "R09 legacy Office layout");
        assert(paths.Contains(@"F:\Custom Office\Office16\ospp.vbs"), "R09 registered direct InstallRoot");
        assert(paths.Contains(@"G:\C2R\Microsoft Office\root\Office16\ospp.vbs"), "R09 registered ClickToRun layout");
        assert(paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() == paths.Count, "R09 duplicate/case/trailing separator candidates merged");
        assert(paths.All(p => Path.IsPathFullyQualified(p) && !p.StartsWith(@"\\") && p.EndsWith("ospp.vbs")), "R09 only fully qualified local script candidates");
        assert(OfficeScriptDiscovery.Candidates(["", "relative", "C:relative", @"\\server\share"], []).Count == 0, "R09 no working-directory/network fallback");
        assert(OfficeScriptDiscovery.Candidates([], []).Count == 0, "R09 absent metadata produces no guessed C path");
    }

    private static async Task AdmissionAsync(Action<bool, string> assert)
    {
        TaskActivityService tasks = new();
        using var current = await tasks.AcquireAsync("repair", "Repair", ["SystemMutation"]);
        var queued = tasks.AcquireAsync("first-run", "First-Run Setup", ["SystemMutation", "AppxDeployment", "WindowsServicing"]);
        bool shown = false, dismissed = false;
        var messageClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = TaskAdmission.WaitAsync(queued, () => shown = true, () => dismissed = true);
        assert(shown && !waiting.IsCompleted, "R09 wizard waits behind repair without starting");
        current!.Complete("COMPLETED", "done");
        using var setup = await waiting.WaitAsync(TimeSpan.FromSeconds(2));
        assert(setup is not null && dismissed && !messageClosed.Task.IsCompleted, "R09 queued work starts without waiting for user closing informational message");
        var following = tasks.AcquireAsync("catalog", "Catalog", ["SystemMutation"]);
        assert(!following.IsCompleted, "R09 first-run holds mutation resource");
        setup!.Complete("FAILED", "synthetic setup error");
        using var next = await following.WaitAsync(TimeSpan.FromSeconds(2));
        assert(next is not null, "R09 first-run failure releases queued catalog");
        next!.Complete("COMPLETED", "done");
        assert(!tasks.HasActiveTask, "R09 queue fully drained");
        foreach (bool failShow in new[] { false, true })
        foreach (bool failDismiss in new[] { false, true })
        {
            var request = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var admitted = TaskAdmission.WaitAsync(request.Task,
                () => { if (failShow) throw new InvalidOperationException("show"); },
                () => { if (failDismiss) throw new InvalidOperationException("dismiss"); });
            request.SetResult(123);
            assert(await admitted.WaitAsync(TimeSpan.FromSeconds(2)) == 123, "R09 notification errors cannot strand granted resource");
        }
        int notices = 0;
        assert(await TaskAdmission.WaitAsync(Task.FromResult(7), () => notices++, () => notices++) == 7 && notices == 0,
            "R09 immediately granted task does not flash queue notice");
        var fault = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failed = TaskAdmission.WaitAsync(fault.Task, () => { }, () => notices++);
        fault.SetException(new InvalidOperationException("request failed"));
        bool preserved = false;
        try { await failed; } catch (InvalidOperationException exception) { preserved = exception.Message == "request failed"; }
        assert(preserved && notices == 1, "R09 original admission failure preserved and notice dismissed");
    }

    private sealed class Callback<T>(Action<T> callback) : IProgress<T>
    { public void Report(T value) => callback(value); }
}
