using System;
using System.Linq;
using System.Threading.Tasks;
using Naufal_Windows_Tech_s_Powertoys;

internal static class MonitoringTests
{
    internal static async Task RunAsync(Action<bool, string> assert)
    {
        TaskActivityService service = new();
        assert(service.RunningSnapshot().Count == 0, "monitor starts empty");
        using var first = await service.AcquireAsync("monitor-first", "First", new[] { "resource" });
        var queuedRequest = service.AcquireAsync("monitor-second", "Second", new[] { "resource" });
        using var independent = await service.AcquireAsync("monitor-read", "Read", Array.Empty<string>());
        var before = service.RunningSnapshot();
        assert(before.Count == 2 && before.All(item => item.State == "RUNNING"), "monitor excludes queued work");
        assert(service.Snapshot().Any(item => item.State == "QUEUED"), "queued work remains internally managed");
        first!.Complete("COMPLETED", "done");
        using var second = await queuedRequest.WaitAsync(TimeSpan.FromSeconds(3));
        assert(service.RunningSnapshot().Any(item => item.Title == "Second"), "promoted task appears automatically");
        assert(service.RunningSnapshot().All(item => item.Title != "First"), "completed task disappears");
        assert(before.Any(item => item.Title == "First"), "monitor snapshot is detached from future mutations");
        second!.Complete("FAILED", "synthetic failure");
        assert(service.RunningSnapshot().All(item => item.Title != "Second"), "failed task disappears");
        independent!.Complete("WARNING", "synthetic warning");
        assert(service.RunningSnapshot().Count == 0, "terminal warnings are not running");
        using (var abandoned = await service.AcquireAsync("monitor-abandoned", "Abandoned", null))
            assert(service.RunningSnapshot().Count == 1, "running task appears immediately");
        assert(service.RunningSnapshot().Count == 0, "interrupted task disappears");
        assert(service.Snapshot().Count == 4, "monitor does not discard diagnostic history");

        LiveMetricHistory history = new();
        assert(history.Count == 0 && history.NetworkCeiling() == 1, "empty chart has bounded default axis");
        history.Add(double.NaN, 1);
        history.Add(-1, 1);
        assert(history.Count == 0, "invalid time is ignored");
        history.Add(0, 25, 2);
        history.Add(1, 50, 5);
        var segments = history.Project(600, 100, 100);
        assert(segments.Count == 1 && segments[0].Count == 2, "adjacent samples form one line");
        assert(segments[0][0].X == 590 && segments[0][1].X == 600, "chart uses real time with newest at right");
        assert(segments[0][0].Y == 75 && segments[0][1].Y == 50, "percentage chart maps exact values");
        assert(history.NetworkCeiling() == 50, "network ceiling covers both series");
        history.Add(1, 75, 5);
        assert(history.Count == 2 && history.Latest!.Value.Primary == 75, "duplicate timestamp replaces instead of growing");
        history.Add(0.5, 0);
        assert(history.Count == 2, "out of order sample does not reverse timeline");
        history.Add(2, null, 10);
        history.Add(3, 0, double.NaN);
        segments = history.Project(600, 100, 100);
        assert(segments.Count == 2 && segments[1].Single().Y == 100, "missing is a gap but genuine zero plots at baseline");
        assert(history.Project(600, 100, 100, true).Single().Count == 3, "secondary data has independent gaps");
        history.Add(20, 1000, -1);
        assert(history.Project(600, 100, 100).Count == 3, "suspend gap is not interpolated");
        assert(history.Project(600, 100, 100).Last().Single().Y == 0, "out of axis values remain inside plot");
        assert(history.Latest!.Value.Secondary is null, "negative rate is unavailable");
        foreach (double bad in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
        {
            assert(history.Project(bad, 100, 100).Count == 0, "invalid chart width handled");
            assert(history.Project(100, bad, 100).Count == 0, "invalid chart height handled");
            assert(history.Project(100, 100, bad).Count == 0, "invalid chart scale handled");
        }
        history.Add(100, double.PositiveInfinity, double.NaN);
        assert(history.Count == 1 && history.Project(600, 100, 100).Count == 0, "old readings expire and invalid readings do not plot");
        for (int i = 0; i < 10000; i++) history.Add(101 + i * 0.001, i, i * 2);
        assert(history.Count == LiveMetricHistory.MaximumSamples, "history stays bounded under fast sampling");
        assert(history.NetworkCeiling() >= 19998, "auto axis includes transmit peaks");
        for (int i = 0; i < 3600; i++) history.Add(200 + i, i % 101);
        assert(history.Count == 61, "one hour sampling retains only sixty seconds");
        foreach (double width in new[] { 20d, 160, 320, 2000 })
        foreach (double height in new[] { 20d, 86, 172, 500 })
            assert(history.Project(width, height, 100).SelectMany(segment => segment)
                .All(point => point.X >= 0 && point.X <= width && point.Y >= 0 && point.Y <= height &&
                    double.IsFinite(point.X) && double.IsFinite(point.Y)), "coordinates remain valid across UI sizes");
    }
}
