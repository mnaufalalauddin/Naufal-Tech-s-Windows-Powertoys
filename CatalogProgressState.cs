using System;
using System.Collections.Generic;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

internal readonly record struct CatalogItemProgress(string Phase, double? Percent, string Detail)
{
    public bool IsTerminal => Phase is "COMPLETED" or "FAILED" or "SKIPPED" or "NOT VERIFIED" or "UNAVAILABLE";
}

internal sealed class CatalogProgressState(IEnumerable<string> ids)
{
    private readonly Dictionary<string, CatalogItemProgress> _items = ids.ToDictionary(
        id => id, _ => new CatalogItemProgress("WAITING", null, "Waiting to start."), StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, CatalogItemProgress> Items => _items;
    public bool Finished { get; private set; }
    public int Settled => _items.Values.Count(item => item.IsTerminal);
    public bool AllVerified => _items.Count > 0 && _items.Values.All(item => item.Phase == "COMPLETED");
    public int VerifiedCount => _items.Values.Count(item => item.Phase == "COMPLETED");
    public int UnavailableCount => _items.Values.Count(item => item.Phase == "UNAVAILABLE");
    public bool HasFailures => _items.Values.Any(item => item.Phase is "FAILED" or "NOT VERIFIED");
    // A Windows command percentage is a step, not the completion percentage of
    // a multi-step tweak. Animate until the first settled item instead of
    // fabricating an aggregate percentage (or displaying an idle empty track).
    public bool IsOverallIndeterminate => !Finished && Settled == 0 &&
        _items.Values.Any(item => item.Phase is "RUNNING" or "VERIFYING");
    public bool CompletedWithoutErrors => _items.Count > 0 &&
        _items.Values.All(item => item.Phase is "COMPLETED" or "UNAVAILABLE");

    public bool Update(string id, string phase, double? percent, string detail)
    {
        if (Finished || !_items.TryGetValue(id, out var current) || current.IsTerminal ||
            phase is not ("RUNNING" or "VERIFYING" or "COMPLETED" or "FAILED" or "SKIPPED" or "NOT VERIFIED" or "UNAVAILABLE") ||
            current.Phase == "VERIFYING" && phase == "RUNNING") return false;
        _items[id] = new(phase, percent is double value && double.IsFinite(value) ? Math.Clamp(value, 0, 100) : null, detail);
        return true;
    }

    public void Finish(bool success, string detail)
    {
        if (Finished) return;
        foreach (string id in _items.Keys.ToArray())
        {
            var item = _items[id];
            if (item.IsTerminal) continue;
            _items[id] = new(success ? "NOT VERIFIED" : item.Phase == "WAITING" ? "SKIPPED" : "FAILED", null,
                success ? "No verified completion was reported for this item." : item.Phase == "WAITING" ? "Not started because the batch stopped." : detail);
        }
        Finished = true;
    }
}
