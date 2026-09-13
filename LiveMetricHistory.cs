using System;
using System.Collections.Generic;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

internal readonly record struct LiveMetricSample(double Seconds, double? Primary, double? Secondary);
internal readonly record struct LiveGraphPoint(double X, double Y);

// UI-independent bounded history. Missing readings and timer/suspend gaps must
// never be represented as zero utilization or joined with an invented line.
internal sealed class LiveMetricHistory
{
    internal const double WindowSeconds = 60;
    internal const int MaximumSamples = 121;
    private const double MaximumContinuousGap = 2.5;
    private readonly List<LiveMetricSample> _samples = new();
    internal int Count => _samples.Count;
    internal LiveMetricSample? Latest => _samples.Count == 0 ? null : _samples[^1];

    internal static double? ValidValue(double? value) =>
        value is double number && double.IsFinite(number) && number >= 0 ? number : null;

    internal void Add(double seconds, double? primary, double? secondary = null)
    {
        if (!double.IsFinite(seconds) || seconds < 0) return;
        if (_samples.Count > 0 && seconds < _samples[^1].Seconds) return;
        var sample = new LiveMetricSample(seconds, ValidValue(primary), ValidValue(secondary));
        if (_samples.Count > 0 && seconds == _samples[^1].Seconds) _samples[^1] = sample;
        else _samples.Add(sample);
        _samples.RemoveAll(item => item.Seconds < seconds - WindowSeconds);
        if (_samples.Count > MaximumSamples) _samples.RemoveRange(0, _samples.Count - MaximumSamples);
    }

    internal double NetworkCeiling()
    {
        double maximum = _samples.SelectMany(item => new[] { item.Primary ?? 0, item.Secondary ?? 0 })
            .DefaultIfEmpty(0).Max();
        if (maximum <= 1) return 1;
        double unit = Math.Pow(10, Math.Floor(Math.Log10(maximum)));
        foreach (double factor in new[] { 1d, 2d, 5d, 10d })
        {
            double ceiling = factor * unit;
            if (double.IsFinite(ceiling) && ceiling >= maximum) return ceiling;
        }
        return maximum;
    }

    internal IReadOnlyList<IReadOnlyList<LiveGraphPoint>> Project(
        double width, double height, double ceiling, bool secondary = false)
    {
        List<IReadOnlyList<LiveGraphPoint>> segments = new();
        if (_samples.Count == 0 || !double.IsFinite(width) || !double.IsFinite(height) ||
            !double.IsFinite(ceiling) || width <= 0 || height <= 0 || ceiling <= 0) return segments;
        double end = _samples[^1].Seconds;
        List<LiveGraphPoint>? segment = null;
        double? previousTime = null;
        foreach (LiveMetricSample sample in _samples)
        {
            double? value = secondary ? sample.Secondary : sample.Primary;
            if (value is null) { segment = null; previousTime = null; continue; }
            if (segment is null || (previousTime.HasValue && sample.Seconds - previousTime.Value > MaximumContinuousGap))
            {
                segment = new();
                segments.Add(segment);
            }
            segment.Add(new LiveGraphPoint(
                Math.Clamp(1 - (end - sample.Seconds) / WindowSeconds, 0, 1) * width,
                (1 - Math.Clamp(value.Value / ceiling, 0, 1)) * height));
            previousTime = sample.Seconds;
        }
        return segments;
    }
}
