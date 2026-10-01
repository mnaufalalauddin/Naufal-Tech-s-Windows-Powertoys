using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed record ResourceMetric(string Id, string Label, string Unit, double Divisor = 1);
internal sealed record ResourceSample(DateTimeOffset Time, double ElapsedSeconds, IReadOnlyDictionary<string, double?> Values);
internal sealed record ResourceMeasurement(string Windows, string Volume, string Workload, string Actions,
    string RebootStatus, DateTimeOffset Started, TimeSpan Uptime, TimeSpan Interval, IReadOnlyList<ResourceSample> Samples);

// Pure aggregation: never infer a failed counter as zero or infer causation from a delta.
internal static class ResourceMeasurements
{
    internal static readonly ResourceMetric[] Metrics =
    [
        new("ram", "Physical RAM used (total minus available)", "GiB", 1073741824),
        new("ramTotal", "Physical RAM total", "GiB", 1073741824),
        new("commit", "System committed memory", "GiB", 1073741824),
        new("commitLimit", "System commit limit", "GiB", 1073741824),
        new("cpu", "CPU busy (all processors)", "%"),
        new("diskRead", "Physical disks read throughput (_Total)", "MB/s", 1000000),
        new("diskWrite", "Physical disks write throughput (_Total)", "MB/s", 1000000),
        new("free", "Windows volume free space (all users)", "GiB", 1073741824),
        new("processes", "System processes", "count"),
        new("threads", "System threads", "count"),
        new("handles", "System handles", "count"),
        new("ownPrivate", "Utility private committed memory", "MiB", 1048576),
        new("ownWorking", "Utility working set (includes shared pages)", "MiB", 1048576),
        new("ownCpu", "Utility CPU / total processor capacity", "%"),
        new("ownThreads", "Utility threads", "count"),
        new("ownHandles", "Utility handles", "count")
    ];

    internal static (double Median, double Min, double Max, int Count)? Summarize(ResourceMeasurement run, string id)
    {
        double[] values = run.Samples.Select(s => s.Values.TryGetValue(id, out var value) ? value : null)
            .Where(v => v.HasValue && double.IsFinite(v.Value) && v.Value >= 0)
            .Select(v => v!.Value).OrderBy(v => v).ToArray();
        if (values.Length == 0) return null;
        int middle = values.Length / 2;
        return (values.Length % 2 == 1 ? values[middle] : values[middle - 1] / 2 + values[middle] / 2,
            values[0], values[^1], values.Length);
    }

    internal static async Task<ResourceMeasurement> CaptureAsync(IResourceProbe probe, string workload, string actions,
        string rebootStatus, CancellationToken cancellationToken, IProgress<int>? progress = null,
        int count = 10, TimeSpan? interval = null)
    {
        TimeSpan delay = interval ?? TimeSpan.FromSeconds(1);
        if (count < 3 || count > 60 || delay < TimeSpan.FromMilliseconds(100) || delay > TimeSpan.FromSeconds(10))
            throw new ArgumentOutOfRangeException(nameof(count));
        DateTimeOffset start = DateTimeOffset.UtcNow;
        TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        cancellationToken.ThrowIfCancellationRequested();
        probe.Read(); // Prime rate counters; excluded from summary.
        List<ResourceSample> samples = [];
        for (int i = 0; i < count; i++)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            samples.Add(probe.Read());
            progress?.Report(i + 1);
        }
        return new(probe.Windows, probe.Volume, workload, actions, rebootStatus, start, uptime, delay, samples.AsReadOnly());
    }

    internal static string Report(ResourceMeasurement baseline, ResourceMeasurement? after = null)
    {
        StringBuilder text = new("RESOURCE MEASUREMENT — READ-ONLY\n\n");
        text.AppendLine("Observed samples, not a benchmark or proof that a tweak caused the change.");
        text.AppendLine("Keep workload, power source, boot age and background activity comparable. No boost score.");
        text.AppendLine("RAM is system total minus available, not summed process working sets. Utility metrics are a subset, not added to system totals.");
        text.AppendLine("Disk rates cover exposed PhysicalDisk instances; missing/disabled providers remain Unknown.");
        text.AppendLine("Reboot status and action labels below are user annotations, not effective-state verification.\n");
        AppendRun("BASELINE", baseline);
        if (after is not null)
        {
            AppendRun("AFTER", after);
            bool same = baseline.Windows == after.Windows && baseline.Volume == after.Volume &&
                baseline.Workload == after.Workload && baseline.Interval == after.Interval && baseline.Samples.Count == after.Samples.Count;
            text.AppendLine(same ? "Context labels match; background conditions may still differ." : "WARNING: context differs; runs are not directly comparable.");
            text.AppendLine("\nMEDIAN DELTAS (after minus baseline; counts alone do not measure saved work)");
            foreach (var metric in Metrics)
            {
                var first = Summarize(baseline, metric.Id);
                var second = Summarize(after, metric.Id);
                if (first is null || second is null || first.Value.Count < 3 || second.Value.Count < 3)
                {
                    text.AppendLine($"{metric.Label}: Unknown — fewer than 3 valid samples in either run.");
                    continue;
                }
                double delta = (second.Value.Median - first.Value.Median) / metric.Divisor;
                string direction = delta > 0 ? "increased" : delta < 0 ? "decreased" : "unchanged";
                text.AppendLine($"{metric.Label}: {delta.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture)} {metric.Unit} ({direction})");
            }
        }
        return text.ToString();

        void AppendRun(string name, ResourceMeasurement run)
        {
            text.AppendLine($"{name} | {run.Started:O}\nWindows: {run.Windows}\nVolume: {run.Volume}\nUptime at start: {run.Uptime}\nWorkload: {run.Workload}\nActions/profile: {run.Actions}\nReboot: {run.RebootStatus}");
            text.AppendLine($"Requested interval: {F(run.Interval.TotalSeconds)}s; samples: {run.Samples.Count}. Values: median [min .. max], valid/total.");
            foreach (var metric in Metrics)
            {
                var summary = Summarize(run, metric.Id);
                text.AppendLine(summary is { } s
                    ? $"{metric.Label}: {F(s.Median / metric.Divisor)} [{F(s.Min / metric.Divisor)} .. {F(s.Max / metric.Divisor)}] {metric.Unit}; {s.Count}/{run.Samples.Count}"
                    : $"{metric.Label}: Unknown; 0/{run.Samples.Count}");
            }
            text.AppendLine("Raw samples (timestamp | actual interval seconds | id=value in base units; ? = unavailable):");
            foreach (var sample in run.Samples)
                text.AppendLine($"{sample.Time:O} | {F(sample.ElapsedSeconds)} | " + string.Join("; ", Metrics.Select(m =>
                    m.Id + "=" + (sample.Values.TryGetValue(m.Id, out var v) && v.HasValue && double.IsFinite(v.Value) ? F(v.Value) : "?"))));
            text.AppendLine();
        }
        static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}

internal interface IResourceProbe : IDisposable
{
    string Windows { get; }
    string Volume { get; }
    ResourceSample Read();
}
