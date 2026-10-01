using Naufal_Windows_Tech_s_Powertoys;

internal static class ResourceMeasurementTests
{
    internal static async Task RunAsync(Action<bool, string> assert)
    {
        ResourceMeasurement Run(params double?[] values) => new("fixture Windows", "C:\\", "idle", "test", "Unknown", DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(20), TimeSpan.FromSeconds(1), values.Select(v => new ResourceSample(DateTimeOffset.UtcNow, 1,
                new Dictionary<string, double?> { ["cpu"] = v })).ToArray());
        var mixed = Run(1, null, 3, double.NaN, double.PositiveInfinity, -1, 2);
        var summary = ResourceMeasurements.Summarize(mixed, "cpu");
        assert(summary is { Median: 2, Min: 1, Max: 3, Count: 3 }, "Analyzer excludes invalid and absent data, not zero-filled");
        assert(ResourceMeasurements.Summarize(Run(0, 0, 0), "cpu") is { Median: 0, Count: 3 }, "Real zero samples preserved");
        assert(ResourceMeasurements.Summarize(Run(1, 2, 3, 4), "cpu")?.Median == 2.5, "Even median");
        assert(ResourceMeasurements.Summarize(mixed, "commit") is null, "Missing memory is Unknown");
        string report = ResourceMeasurements.Report(Run(2, 2, 2), Run(1, 2, 3));
        assert(report.Contains("unchanged") && report.Contains("Unknown — fewer than 3"), "Neutral changes and insufficient evidence distinguished");
        assert(ResourceMeasurements.Report(Run(1, 1, 1), Run(2, 2, 2)).Contains("increased"), "Negative performance direction not hidden");
        assert(ResourceMeasurements.Report(Run(3, 3, 3), Run(1, 1, 1)).Contains("decreased"), "Decreases reported without boost score");
        assert(ResourceMeasurements.Report(mixed, mixed with { Workload = "game" }).Contains("WARNING: context differs"), "Workload mismatch warned");
        assert(ResourceMeasurements.Report(mixed).Contains("Raw samples") && ResourceMeasurements.Report(mixed).Contains("not effective-state verification"), "Export includes samples and honest reboot scope");
        using var probe = new Probe();
        var captured = await ResourceMeasurements.CaptureAsync(probe, "idle", "none", "Unknown", CancellationToken.None, count: 3, interval: TimeSpan.FromMilliseconds(100));
        assert(probe.Reads == 4 && captured.Samples.Count == 3, "Priming read excluded from statistics");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        bool stopped = false;
        try { await ResourceMeasurements.CaptureAsync(probe, "", "", "", cancelled.Token); }
        catch (OperationCanceledException) { stopped = true; }
        assert(stopped, "Closing analyzer cancels sampling");
        bool rejected = false;
        try { await ResourceMeasurements.CaptureAsync(probe, "", "", "", CancellationToken.None, count: 1); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        assert(rejected, "One-snapshot benchmark rejected");
    }

    private sealed class Probe : IResourceProbe
    {
        public int Reads;
        public string Windows => "fixture";
        public string Volume => "C:\\";
        public ResourceSample Read() { Reads++; return new(DateTimeOffset.UtcNow, .1, new Dictionary<string, double?> { ["cpu"] = Reads }); }
        public void Dispose() { }
    }
}
