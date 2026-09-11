using Naufal_Windows_Tech_s_Powertoys;

internal static class CatalogAvailabilityTests
{
    internal static async Task RunAsync(Action<bool, string> assert)
    {
        ToolToggleState absent = ToolToggleState.Unavailable("No matching hardware is installed.");
        ToolToggleState ready = new(false, true, "OFF");
        ToolToggleState applied = new(true, true, "ON");
        ToolToggleState denied = new(false, false, "Unable to read", "Access denied");
        assert(absent.IsConfirmedUnavailable && !absent.HasReadFailure && !absent.IsOn, "Availability explicit absence is not an applied tweak or failure");
        assert(denied.HasReadFailure && !denied.IsConfirmedUnavailable, "Availability access denial is not absence");
        assert(new ToolToggleState(false, false, "Unavailable", "No snapshot exists").HasReadFailure, "Availability legacy wording cannot classify missing backup as absence");
        assert(!(ready with { UnavailableOnThisPc = true }).IsConfirmedUnavailable, "Availability contradictory flag cannot override available state");
        foreach (var first in new[] { absent, ready, applied, denied })
        foreach (var second in new[] { absent, ready, applied, denied })
        {
            var aggregate = CatalogAvailability.Aggregate([first, second]);
            bool hasFailure = first.HasReadFailure || second.HasReadFailure;
            bool allAbsent = first.IsConfirmedUnavailable && second.IsConfirmedUnavailable;
            assert(aggregate.IsConfirmedUnavailable == allAbsent, "Availability composite only all-confirmed-absent is unavailable");
            assert(aggregate.HasReadFailure == hasFailure, "Availability composite never hides child read errors");
            assert(aggregate.IsAvailable == (!hasFailure && !allAbsent), "Availability composite applicability matrix");
            assert(aggregate.IsOn == (!hasFailure && !allAbsent && new[] { first, second }.Where(s => s.IsAvailable).All(s => s.IsOn)), "Availability composite verified state excludes absent siblings, not failed ones");
        }
        assert(CatalogAvailability.Aggregate([]).HasReadFailure, "Availability empty definition is not assumed absent");
        foreach (int count in new[] { 0, 1, 8, 41 })
        {
            var progress = new CatalogProgressState(Enumerable.Range(0, 41).Select(i => i.ToString()));
            for (int i = 0; i < 41; i++) progress.Update(i.ToString(), i < count ? "UNAVAILABLE" : "COMPLETED", i < count ? null : 100, "fixture");
            progress.Finish(true, "Finished checking");
            assert(progress.Settled == 41 && progress.CompletedWithoutErrors, "Availability absence settles checks without failing whole catalog");
            assert(progress.AllVerified == (count == 0) && progress.VerifiedCount == 41 - count && progress.UnavailableCount == count, "Availability unavailable never counted verified");
            assert(!progress.Update("0", "FAILED", 0, "late"), "Availability terminal unaffected by late events");
            assert(CatalogAvailability.Summary(41-count, 41, count).StartsWith($"{41-count} out of 41 have been verified, but {count} tweaks"), "Availability badge counts exact");
        }
        foreach (string bad in new[] { "FAILED", "NOT VERIFIED", "SKIPPED", "WAITING" })
        {
            var progress = new CatalogProgressState(["absent", "other"]);
            progress.Update("absent", "UNAVAILABLE", null, "missing hardware");
            if (bad != "WAITING") progress.Update("other", bad, null, "failed");
            progress.Finish(true, "done");
            assert(!progress.CompletedWithoutErrors && progress.UnavailableCount == 1, "Availability mixed actual failure stays unsuccessful: " + bad);
        }
        assert(!new CatalogProgressState([]).CompletedWithoutErrors, "Availability empty progress is not successful");
        var definition = new ToolToggleDefinition("fixture", "test", "Fixture", "", false, false);
        foreach (CatalogOperation operation in Enum.GetValues<CatalogOperation>())
        {
            foreach (var state in new[] { absent, denied })
            {
                var service = new FakeService(state);
                var result = await CatalogOperationRunner.ExecuteAsync(service, definition, operation);
                assert(service.Writes == 0 && !result.Success && !result.Verified, "Availability preflight avoids unavailable/unsafe writes: " + operation);
                assert(result.SkippedUnavailable == state.IsConfirmedUnavailable, "Availability only proven absence skips neutrally: " + operation);
            }
            var failingWrite = new FakeService(ready) { After = absent };
            var failure = await CatalogOperationRunner.ExecuteAsync(failingWrite, definition, operation);
            assert(failingWrite.Writes == 1 && !failure.Success && !failure.SkippedUnavailable, "Availability failed write is not masked by absent post-state: " + operation);
        }
        foreach (Exception error in new Exception[] { new UnauthorizedAccessException("denied"), new TimeoutException("timed out"), new IOException("read failure") })
        {
            var service = new FakeService(ready) { ReadError = error };
            bool surfaced = false;
            try { await CatalogOperationRunner.ExecuteAsync(service, definition, CatalogOperation.Apply); }
            catch (Exception caught) { surfaced = ReferenceEquals(error, caught); }
            assert(surfaced && service.Writes == 0, "Availability thrown probe failure is never neutral: " + error.GetType().Name);
        }
        string missingPath = Path.Combine(Path.GetTempPath(), "naufal-absent-" + Guid.NewGuid().ToString("N"), "file.dll");
        assert(!CatalogAvailability.FileIsPresent(missingPath), "Availability missing file fixture");
        assert(CatalogAvailability.FileIsPresent(Environment.ProcessPath!), "Availability existing file fixture");
        bool malformedTarget = false;
        try { CatalogAvailability.FileIsPresent(Path.GetTempPath()); }
        catch (IOException) { malformedTarget = true; }
        assert(malformedTarget, "Availability a directory instead of expected file is a real error");
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string main = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        string view = File.ReadAllText(Path.Combine(root, "CatalogProgressWindow.cs"));
        assert(main.Contains("progressWindow.UnavailableItem(definition.Id, state.Error)") && main.Contains("state.HasReadFailure"), "Availability Analyze uses explicit states");
        assert(main.Contains("availabilityBadge.Update(available, definitions.Count") && main.Contains("progressWindow.CompleteItem(definition.Id, result)"), "Availability counts and operation outcomes wired");
        assert(view.Contains("_progress.CompletedWithoutErrors") && view.Contains("_availabilityBadge.Update(_progress.VerifiedCount, _total, _progress.UnavailableCount)"), "Availability progress badge counts actual verified rows");
        string badge = File.ReadAllText(Path.Combine(root, "CatalogAvailabilityBadge.cs"));
        assert(badge.Contains("107, 114, 128") && badge.Contains("TextWrapping.Wrap") && !badge.Contains("Height ="), "Availability neutral gray badge wraps without fixed height");
    }

    private sealed class FakeService : IToolToggleService
    {
        private readonly ToolToggleState _before;
        internal FakeService(ToolToggleState before) { _before = before; After = before; }
        internal int Writes;
        internal ToolToggleState After;
        internal Exception? ReadError;
        public IReadOnlyList<ToolToggleDefinition> GetDefinitions() => [];
        public Task<ToolToggleState> ReadStateAsync(ToolToggleDefinition definition) => ReadError is null ? Task.FromResult(_before) : Task.FromException<ToolToggleState>(ReadError);
        public Task<ToolToggleOperationResult> SetStateAsync(ToolToggleDefinition definition, bool targetOn)
        {
            Writes++;
            return Task.FromResult(new ToolToggleOperationResult(false, false, "Synthetic write failure", After));
        }
    }
}
