using Naufal_Windows_Tech_s_Powertoys;

internal static class CatalogBatchTests
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        var row = new ToolToggleDefinition("leaf", "TEST", "Leaf", "Synthetic", false, false);
        var leaf = new Leaf(row);
        var collision = new Alias("collision", leaf, row, true);
        var rejectedDefinition = await CatalogOperationRunner.ExecuteAsync(collision, collision.Row, CatalogOperation.RestoreSavedState);
        check(!rejectedDefinition.Success && leaf.Restores == 0 && leaf.Sets == 0, "Conflicting leaf definitions rejected before any composite write");
        var first = new Alias("first", leaf, row); var second = new Alias("second", leaf, row);
        using (CatalogExecutionBatch.Begin())
        {
            var a = await CatalogOperationRunner.ExecuteAsync(first, first.Row, CatalogOperation.RestoreSavedState);
            var b = await CatalogOperationRunner.ExecuteAsync(second, second.Row, CatalogOperation.RestoreSavedState);
            check(a.Success && b.Success && leaf.Restores == 1, "Shared leaf restore executes once across aliases; snapshot not consumed twice");
            check(leaf.Defaults == 0 && first.Writes == 0 && second.Writes == 0, "Canonical routing bypasses duplicate wrapper writes and false default restore");
            await CatalogOperationRunner.ExecuteAsync(leaf, row, CatalogOperation.SetOff);
            check(leaf.Sets == 1, "Explicit OFF is distinct from Restore within a batch");
        }
        using (CatalogExecutionBatch.Begin())
            await CatalogOperationRunner.ExecuteAsync(first, first.Row, CatalogOperation.RestoreSavedState);
        check(leaf.Restores == 2, "Fresh batch does not reuse a stale prior restore result");
        int calls = 0;
        var result = new ToolToggleOperationResult(false, false, "synthetic failure", new(false, true, "OFF"));
        using (CatalogExecutionBatch.Begin())
        {
            var completion = new TaskCompletionSource<ToolToggleOperationResult>();
            Task<ToolToggleOperationResult> Run() => CatalogExecutionBatch.ExecuteAsync(leaf, row, CatalogOperation.Apply, () => { calls++; return completion.Task; });
            Task<ToolToggleOperationResult>[] tasks = [Run(), Run(), Run()];
            check(calls == 1 && tasks.All(t => !t.IsCompleted), "Concurrent references share one in-flight canonical operation");
            completion.SetResult(result); await Task.WhenAll(tasks);
            check(tasks.All(t => t.Result == result), "Every reference sees the same failure; failure is not retried automatically");
            bool rejected = false;
            try { await CatalogExecutionBatch.ExecuteAsync(leaf, row with { Description = "different" }, CatalogOperation.Apply, () => Task.FromResult(result)); }
            catch (InvalidOperationException) { rejected = true; }
            check(rejected, "Conflicting definition rejected during execution as well as planning");
            using (CatalogExecutionBatch.Begin())
                await CatalogExecutionBatch.ExecuteAsync(leaf, row, CatalogOperation.Apply, () => { calls++; return Task.FromResult(result); });
            await Run();
            check(calls == 2, "Nested batch restores outer scope after disposal");
        }
        int throws = 0;
        using (CatalogExecutionBatch.Begin())
        {
            for (int i = 0; i < 2; i++)
                try { await CatalogExecutionBatch.ExecuteAsync(leaf, row, CatalogOperation.Apply, () => { throws++; throw new IOException("synthetic"); }); }
                catch (IOException) { }
        }
        check(throws == 1, "Exception cached within batch to prevent a repeated partial mutation");
        await CatalogExecutionBatch.ExecuteAsync(leaf, row, CatalogOperation.Apply, () => { calls++; return Task.FromResult(result); });
        await CatalogExecutionBatch.ExecuteAsync(leaf, row, CatalogOperation.Apply, () => { calls++; return Task.FromResult(result); });
        check(calls == 4, "Outside a batch independent calls remain independent");
    }

    private sealed class Leaf(ToolToggleDefinition row) : IToolToggleService
    {
        public int Sets, Restores, Defaults;
        public IReadOnlyList<ToolToggleDefinition> GetDefinitions() => [row];
        public Task<ToolToggleState> ReadStateAsync(ToolToggleDefinition definition) => Task.FromResult(new ToolToggleState(false, true, "OFF"));
        public Task<ToolToggleOperationResult> SetStateAsync(ToolToggleDefinition definition, bool targetOn)
        { Sets++; return Task.FromResult(new ToolToggleOperationResult(true, true, "Set", new(targetOn, true, "configured"))); }
        public Task<ToolToggleOperationResult> RestoreOriginalAsync(ToolToggleDefinition definition)
        { Restores++; return Task.FromResult(new ToolToggleOperationResult(true, true, "Exact restore", new(false, true, "OFF"))); }
        public Task<ToolToggleOperationResult> RestoreWindowsDefaultAsync(ToolToggleDefinition definition)
        { Defaults++; throw new Exception("Unexpected fallback"); }
    }
    private sealed class Alias(string id, Leaf leaf, ToolToggleDefinition child, bool conflicting = false) : IToolToggleService, ICatalogPlanSource
    {
        public ToolToggleDefinition Row = child with { Id = id }; public int Writes;
        public IReadOnlyList<CatalogPlanAction> GetPlanActions(ToolToggleDefinition definition) => conflicting
            ? [new(leaf, child), new(leaf, child with { Description = "incompatible" })] : [new(leaf, child)];
        public IReadOnlyList<ToolToggleDefinition> GetDefinitions() => [Row];
        public Task<ToolToggleState> ReadStateAsync(ToolToggleDefinition definition) => leaf.ReadStateAsync(child);
        public Task<ToolToggleOperationResult> SetStateAsync(ToolToggleDefinition definition, bool targetOn)
        { Writes++; throw new Exception("Wrapper must not perform independent writes"); }
    }
}
