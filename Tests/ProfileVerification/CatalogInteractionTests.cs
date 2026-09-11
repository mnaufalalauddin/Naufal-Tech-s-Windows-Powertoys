using Naufal_Windows_Tech_s_Powertoys;

internal static class CatalogInteractionTests
{
    internal static async Task RunAsync(Action<bool, string> assert)
    {
        var definition = new ToolActionDefinition("fixture", "Test", "Fixture", "No OS writes", "Apply", false, true);
        var outer = new Recorder();
        using var outerScope = CatalogOperationProgress.Begin(outer);
        foreach (bool restore in new[] { false, true })
        {
            var reporter = new Recorder();
            bool workerContext = false;
            var service = new FakeService(async restoring =>
            {
                workerContext = SynchronizationContext.Current is null;
                assert(restoring == restore, "Action direction stays unchanged");
                assert(ReferenceEquals(CatalogOperationProgress.Current, reporter), "Progress present before first await");
                CatalogOperationProgress.Current?.Report(new("First Windows step", 25));
                await Task.Delay(1).ConfigureAwait(false);
                CatalogOperationProgress.Current?.Report(new("Second Windows step", null));
                return new(true, restoring ? "Restored" : "Applied");
            });
            var previousContext = SynchronizationContext.Current;
            Task<ToolActionResult> pending;
            try
            {
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                pending = CatalogActionRunner.ExecuteAsync(service, definition, restore, reporter);
            }
            finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
            var result = await pending;
            assert(result.Success && workerContext, "Apply and Restore synchronous backend portions stay off the UI context");
            assert(reporter.Values.Select(item => item.Percent).SequenceEqual(new double?[] { 25, null }), "Both progress stages reach the chosen reporter");
            assert(ReferenceEquals(CatalogOperationProgress.Current, outer), "Action does not leak its progress context to caller");
        }

        var throwing = new FakeService(_ => throw new InvalidOperationException("fixture failure"));
        foreach (bool restore in new[] { false, true })
        {
            bool failed = false;
            try { await CatalogActionRunner.ExecuteAsync(throwing, definition, restore, new Recorder()); }
            catch (InvalidOperationException error) { failed = error.Message == "fixture failure"; }
            assert(failed, "Backend exception remains a failure in both directions");
            assert(ReferenceEquals(CatalogOperationProgress.Current, outer), "Failure releases the action progress scope");
        }
        var unsupported = await CatalogActionRunner.ExecuteAsync(throwing, definition with { SupportsRestore = false }, true);
        assert(!unsupported.Success && !unsupported.SkippedUnavailable, "Non-restorable actions do not invoke Restore or become unavailable");

        // Simulate a dispatcher that restores its context on each continuation.
        // A plain SynchronizationContext posts to the pool without restoring it
        // and would accidentally let the old direct-backend call pass this test.
        foreach (CatalogOperation operation in Enum.GetValues<CatalogOperation>())
        {
            var reporter = new Recorder();
            var service = new FakeToggle();
            var toggle = new ToolToggleDefinition("fixture", "Test", "Fixture", "No OS writes", false, false);
            var previous = SynchronizationContext.Current;
            Task<ToolToggleOperationResult> pending;
            try
            {
                SynchronizationContext.SetSynchronizationContext(new TestDispatcherContext());
                pending = CatalogOperationRunner.ExecuteAsync(service, toggle, operation, reporter);
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
            var result = await pending;
            assert(service.WorkerContext, "Toggle backend stays off dispatcher: " + operation);
            assert(result.Success && result.Verified && result.BeforeState.HasValue, "Toggle result and preflight retained: " + operation);
            assert(reporter.Values.Single().Detail == "Backend step", "Bulk backend retains scoped progress: " + operation);
            assert(ReferenceEquals(CatalogOperationProgress.Current, outer), "Bulk worker does not leak scope: " + operation);
        }

        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int entered = 0;
        var parallel = new FakeService(async restoring =>
        {
            if (Interlocked.Increment(ref entered) == 2) ready.SetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            CatalogOperationProgress.Current?.Report(new(restoring ? "Restore fixture" : "Apply fixture", 50));
            return new(false, "Unavailable fixture", SkippedUnavailable: true);
        });
        var applyReporter = new Recorder();
        var restoreReporter = new Recorder();
        var results = await Task.WhenAll(
            CatalogActionRunner.ExecuteAsync(parallel, definition, false, applyReporter),
            CatalogActionRunner.ExecuteAsync(parallel, definition, true, restoreReporter));
        assert(applyReporter.Values.Single().Detail == "Apply fixture" && restoreReporter.Values.Single().Detail == "Restore fixture", "Concurrent catalogs cannot exchange progress reporters");
        assert(results.All(result => !result.Success && result.SkippedUnavailable), "Runner preserves actual result flags");

        var state = new CatalogProgressState(new[] { "a", "b", "c" });
        assert(!state.IsOverallIndeterminate && !state.HasFailures, "Waiting batch stays idle");
        state.Update("a", "RUNNING", 100, "A Windows step, not a complete tweak");
        assert(state.IsOverallIndeterminate && state.Settled == 0, "A Windows step at 100 does not fake overall completion");
        state.Update("a", "VERIFYING", null, "Reading back");
        assert(state.IsOverallIndeterminate, "Overall animates during first verification");
        state.Update("a", "COMPLETED", 100, "Verified");
        state.Update("b", "RUNNING", null, "Working");
        assert(!state.IsOverallIndeterminate && state.Settled == 1, "Overall switches to verified/processed item count");
        state.Update("b", "FAILED", null, "Write failed");
        state.Update("c", "RUNNING", 10, "Next item");
        assert(state.HasFailures && state.Settled == 2, "Failure remains visible while later tasks run");
        state.Update("c", "UNAVAILABLE", null, "Not installed");
        state.Finish(false, "Done");
        assert(!state.IsOverallIndeterminate && !state.CompletedWithoutErrors, "Failure never turns green at batch completion");
        var absent = new CatalogProgressState(new[] { "missing" });
        absent.Update("missing", "UNAVAILABLE", null, "Not installed");
        absent.Finish(true, "Skipped absent device");
        assert(!absent.HasFailures && absent.CompletedWithoutErrors && absent.VerifiedCount == 0, "Unavailable stays neutral, not verified or failed");
        var interrupted = new CatalogProgressState(new[] { "active" });
        interrupted.Update("active", "RUNNING", null, "Running");
        interrupted.Finish(false, "Stopped");
        assert(!interrupted.IsOverallIndeterminate && interrupted.HasFailures, "Interrupted work stops its progress animation");
    }

    private sealed class Recorder : IProgress<CatalogProgressUpdate>
    {
        public List<CatalogProgressUpdate> Values { get; } = new();
        public void Report(CatalogProgressUpdate value) => Values.Add(value);
    }

    private sealed class FakeService(Func<bool, Task<ToolActionResult>> run) : IToolActionService
    {
        public IReadOnlyList<ToolActionDefinition> GetActions() => Array.Empty<ToolActionDefinition>();
        public Task<ToolActionResult> RunAsync(ToolActionDefinition definition) => run(false);
        public Task<ToolActionResult> RestoreAsync(ToolActionDefinition definition) => run(true);
    }

    private sealed class TestDispatcherContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) =>
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var previous = Current;
                try { SetSynchronizationContext(this); callback(state); }
                finally { SetSynchronizationContext(previous); }
            });
    }

    private sealed class FakeToggle : IToolToggleService
    {
        public bool WorkerContext;
        public IReadOnlyList<ToolToggleDefinition> GetDefinitions() => Array.Empty<ToolToggleDefinition>();
        public async Task<ToolToggleState> ReadStateAsync(ToolToggleDefinition definition)
        {
            await Task.Delay(5).ConfigureAwait(false);
            return new(false, true, "Before fixture");
        }
        public Task<ToolToggleOperationResult> SetStateAsync(ToolToggleDefinition definition, bool targetOn)
        {
            WorkerContext = SynchronizationContext.Current is null;
            CatalogOperationProgress.Current?.Report(new("Backend step", null));
            return Task.FromResult(new ToolToggleOperationResult(true, true, "Verified fixture", new(targetOn, true, "After fixture")));
        }
    }
}
