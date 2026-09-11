using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed class CommandOutputProgress : IProgress<string>
{
    private readonly IProgress<MaintenanceProgressUpdate>? _target;
    private readonly int _index, _count;
    private readonly string _name;
    private readonly StringBuilder _pending = new();
    private readonly object _gate = new();
    private long _last;
    private double? _percent;
    private string _tail = "";
    private static readonly Regex Percentage = new(@"(?<!\d)(\d{1,3}(?:[.,]\d+)?)\s*%", RegexOptions.CultureInvariant);
    internal CommandOutputProgress(IProgress<MaintenanceProgressUpdate>? target, int index, int count, string name)
    { _target = target; _index = index; _count = count; _name = name; }
    public void Report(string chunk)
    {
        lock (_gate)
        {
            _pending.Append(chunk.Replace("\0", ""));
            if (Environment.TickCount64 - _last >= 120 || _pending.Length >= 4096) FlushCore();
        }
    }
    public void Flush() { lock (_gate) FlushCore(); }
    private void FlushCore()
    {
        if (_pending.Length == 0) return;
        string text = _pending.ToString();
        string combined = _tail + text;
        double? value = ParsePercent(combined);
        _tail = combined.Length > 64 ? combined[^64..] : combined;
        if (value.HasValue) _percent = Math.Max(_percent ?? 0, value.Value);
        _target?.Report(new(_index, _count, _name, text.Trim(), _percent));
        _pending.Clear();
        _last = Environment.TickCount64;
    }
    internal static double? ParsePercent(string text)
    {
        MatchCollection matches = Percentage.Matches(text.Replace("\0", ""));
        if (matches.Count == 0) return null;
        return double.TryParse(matches[^1].Groups[1].Value.Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out double value) && value is >= 0 and <= 100 ? value : null;
    }
}
