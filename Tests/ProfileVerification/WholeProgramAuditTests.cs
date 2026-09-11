using Naufal_Windows_Tech_s_Powertoys;

internal static class WholeProgramAuditTests
{
    internal static async Task RunAsync(Action<bool, string> assert)
    {
        foreach (int exit in new[] { -1, int.MinValue, 0, 1, 2, 3010, 1641 })
        foreach (bool timeout in new[] { false, true })
        {
            NativeCommandResult result = new(exit, "", "", timeout, TimeSpan.Zero);
            RepairCommandOutcome expected = timeout || exit < 0 ? RepairCommandOutcome.Failed : exit == 0 ? RepairCommandOutcome.Passed : RepairCommandOutcome.Warning;
            assert(RepairCommandClassification.Sfc(result) == expected, "audit SFC distinguishes transport failure " + exit + "/" + timeout);
            assert(RepairCommandClassification.DismSucceeded(result) == (!timeout && exit is 0 or 3010), "audit DISM timeout never passes " + exit + "/" + timeout);
        }
        string[] states = ["WAITING", "RUNNING", "PASS", "FAILED", "WARNING", "SKIPPED", "NOT VERIFIED", "unknown"];
        foreach (string first in states)
        foreach (string second in states)
        foreach (bool success in new[] { false, true })
        {
            var completion = MaintenanceCompletion.Evaluate(success, 0, [first, second]);
            bool expected = success && new[] { first, second }.All(s => s is "PASS" or "WARNING" or "SKIPPED");
            assert(completion.Success == expected, "audit overall never green with failed/unverified stage " + first + "/" + second);
            assert(completion.Stages.All(MaintenanceCompletion.IsTerminal), "audit every completed row is terminal");
            assert(completion.WarningCount == new[] { first, second }.Count(s => s == "WARNING"), "audit stage warning reflected in summary");
        }
        assert(!MaintenanceCompletion.Evaluate(true, 0, []).Success, "audit zero stages cannot certify repair");
        assert(MaintenanceCompletion.Evaluate(true, 5, ["PASS"]).WarningCount == 5, "audit backend warnings retained");
        MaintenanceProgressUpdate running = new(1, 2, "A", "Working", 12);
        assert(!MaintenanceCompletion.CanReport(["PASS", "RUNNING"], 2, running), "audit late progress cannot revive finished stage");
        assert(!MaintenanceCompletion.CanReport(["RUNNING", "RUNNING"], 2, running), "audit old running stage cannot regress current stage");
        assert(MaintenanceCompletion.CanReport(["RUNNING", "RUNNING"], 2, running with { Status = "FAILED" }), "audit late terminal evidence can settle prior stage");
        assert(!MaintenanceCompletion.CanReport(["WAITING", "WAITING"], 0, running with { StageIndex = 0 }), "audit zero index ignored");
        assert(!MaintenanceCompletion.CanReport(["WAITING", "WAITING"], 0, running with { StageCount = 3 }), "audit wrong stage count ignored");
        assert(!MaintenanceCompletion.CanReport(["WAITING", "WAITING"], 0, running with { Status = "unknown" }), "audit unknown phase ignored");

        assert(GpuDriverVersionVerification.Compare("NVIDIA", "31.0.15.6094", "560.94") == DriverVersionMatch.Matched, "audit NVIDIA device version conversion");
        assert(GpuDriverVersionVerification.Compare("NVIDIA", "560.94", "560.94") == DriverVersionMatch.Matched, "audit NVIDIA marketing version preserved");
        assert(GpuDriverVersionVerification.Compare("NVIDIA", "31.0.15.6094", "561.09") == DriverVersionMatch.Mismatch, "audit unchanged old NVIDIA driver not target success");
        assert(GpuDriverVersionVerification.Compare("Intel", "31.0.101.2141", "31.0.101.2141") == DriverVersionMatch.Matched, "audit Intel exact driver target");
        assert(GpuDriverVersionVerification.Compare("Intel", "31.0.101.2111", "31.0.101.2141") == DriverVersionMatch.Mismatch, "audit unchanged old Intel driver not target success");
        assert(GpuDriverVersionVerification.Compare("AMD", "31.0.21001.45002", "24.5.1") == DriverVersionMatch.Unavailable, "audit AMD package version is not display INF version");
        foreach (string invalid in new[] { "", "unknown", "31.0", "driver-31.0.15.6094", "0x56094" })
        {
            assert(GpuDriverVersionVerification.Compare("NVIDIA", invalid, "560.94") == DriverVersionMatch.Unavailable, "audit invalid NVIDIA read-back " + invalid);
            assert(GpuDriverVersionVerification.Compare("Intel", invalid, "31.0.101.2141") == DriverVersionMatch.Unavailable, "audit invalid Intel read-back " + invalid);
        }
        assert(GpuDriverVersionVerification.Compare("Intel", "31.0.101.2141", "101.2141") == DriverVersionMatch.Unavailable, "audit ambiguous target not guessed");
        await ReadProbeAsync(assert);

        // Wiring checks complement synthetic logic tests; not native UI tests.
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string main = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        assert(!main.Contains("await progressWindow.WaitUntilClosedAsync();"), "audit task lifetime ends with backend, not user closing results");
        assert(main.Contains("CatalogStateReader.ReadAsync(service, definition"), "audit shared read probe wired into catalogs");
        foreach (string catalog in new[] { "_gamingCatalog", "_essentialCatalog", "_advancedCatalog" })
            assert(main.Contains(catalog + " ??="), "audit catalog identity retained on reopen " + catalog);
        assert(File.ReadAllText(Path.Combine(root, "WindowsRepairService.cs")).Contains("RepairCommandClassification.Sfc(sfcResult)"), "audit SFC policy wired");
        assert(File.ReadAllText(Path.Combine(root, "GpuDriverService.cs")).Contains("targetMatch == DriverVersionMatch.Mismatch && !restartRequired"), "audit GPU mismatch blocks success unless explicitly deferred to restart");

        // A finished result window may remain open, while its resource is free.
        TaskActivityService tasks = new();
        using var current = await tasks.AcquireAsync("repair", "Repair", ["SystemMutation"]);
        var queued = tasks.AcquireAsync("next", "Next", ["SystemMutation"]);
        TaskCompletionSource resultWindowClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        assert(!queued.IsCompleted, "audit active backend retains resource");
        current!.Complete("COMPLETED", "Backend finished; result window stays open.");
        using var next = await queued.WaitAsync(TimeSpan.FromSeconds(2));
        assert(next is not null && !resultWindowClosed.Task.IsCompleted, "audit next backend runs while old result remains visible");
        next!.Complete("COMPLETED", "Finished.");
    }

    private static async Task ReadProbeAsync(Action<bool, string> assert)
    {
        PendingReadService service = new();
        var definition = service.GetDefinitions()[0];
        for (int retry = 0; retry < 3; retry++)
        {
            bool timedOut = false;
            try { await CatalogStateReader.ReadAsync(service, definition, TimeSpan.FromMilliseconds(50)); }
            catch (TimeoutException) { timedOut = true; }
            assert(timedOut, "audit blocked catalog read has bounded wait");
        }
        assert(service.Starts == 1, "audit repeated Analyze does not accumulate pending readers");
        var healthy = await CatalogStateReader.ReadAsync(service, definition with { Id = "healthy" }, TimeSpan.FromSeconds(2));
        assert(healthy.IsAvailable, "audit blocked row leaves independent rows usable");
        service.Pending.SetResult(new(false, true, "Read completed"));
        await service.ReadFinished.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var result = await CatalogStateReader.ReadAsync(service, definition, TimeSpan.FromSeconds(2));
        assert(result.IsAvailable, "audit probe recovers when Windows read finishes");
        // Wait for the tracked task to settle, then a new call must obtain fresh data.
        await Task.Delay(25);
        int before = service.Starts;
        result = await CatalogStateReader.ReadAsync(service, definition, TimeSpan.FromSeconds(2));
        assert(service.Starts == before + 1, "audit completed probe not cached forever");
        service.ThrowNext = true;
        await Task.Delay(25);
        bool failed = false;
        try { await CatalogStateReader.ReadAsync(service, definition, TimeSpan.FromSeconds(2)); }
        catch (InvalidOperationException) { failed = true; }
        assert(failed, "audit provider errors retained");
        service.ThrowNext = false;
        result = await CatalogStateReader.ReadAsync(service, definition, TimeSpan.FromSeconds(2));
        assert(result.IsAvailable, "audit retry possible after provider error");
    }

    private sealed class PendingReadService : IToolToggleService
    {
        internal int Starts;
        internal bool ThrowNext;
        internal readonly TaskCompletionSource<ToolToggleState> Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource ReadFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<ToolToggleDefinition> GetDefinitions() => [new("blocked", "test", "Test", "", false, false)];
        public async Task<ToolToggleState> ReadStateAsync(ToolToggleDefinition definition)
        {
            if (definition.Id == "healthy") return new(false, true, "healthy");
            Interlocked.Increment(ref Starts);
            if (ThrowNext) throw new InvalidOperationException("Synthetic read failure");
            var value = await Pending.Task;
            ReadFinished.TrySetResult();
            return value;
        }
        public Task<ToolToggleOperationResult> SetStateAsync(ToolToggleDefinition definition, bool targetOn) =>
            throw new InvalidOperationException("Mutation must never be called by this audit.");
    }
}
