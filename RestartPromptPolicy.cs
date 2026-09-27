using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class RestartPromptPolicy
{
    // Explorer/app refresh and immediately effective options need no OS reboot.
    internal static bool RequiresWindowsRestart(string id, bool recommended, string actual = "") =>
        actual.Contains("RUNNING UNTIL STOP/REBOOT", StringComparison.OrdinalIgnoreCase) ||
        id == "ModernStandbyOverride" ||
        (recommended && id is not ("IconCache" or "EndTask" or "ClassicContext" or
            "PhotoViewer" or "Widgets" or "ExplorerHomeGallery" or "Hibernation"));

    internal static bool ForToggle(ToolToggleDefinition definition, ToolToggleOperationResult result) =>
        result.Success && result.Verified && !result.SkippedUnavailable && result.State.IsAvailable &&
        RequiresWindowsRestart(definition.Id, definition.RestartRecommended, result.State.ActualValue);

    internal static bool ForAction(ToolActionDefinition definition, ToolActionResult result) =>
        result.Success && !result.SkippedUnavailable &&
        RequiresWindowsRestart(definition.Id, definition.RestartRecommended);
}

// UI-thread-owned. Injected callbacks allow testing without shutdown or OS changes.
internal sealed class RestartPromptCoordinator
{
    private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);
    internal bool IsPromptActive { get; private set; }
    internal bool RestartRequested { get; private set; }
    internal int PendingCount => _pending.Count;

    internal void Add(string name)
    {
        if (!string.IsNullOrWhiteSpace(name)) _pending.Add(name);
    }

    internal async Task OfferAsync(Func<bool> isBusy,
        Func<string[], Task<bool>> confirm, Func<Task> restart)
    {
        if (IsPromptActive || RestartRequested || _pending.Count == 0 || isBusy()) return;
        IsPromptActive = true;
        string[] names = _pending.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        _pending.Clear();
        try
        {
            // Later acknowledges this batch; subsequent ticks do not nag.
            if (!await confirm(names)) return;
            // Work may start in another window while confirmation is open.
            if (isBusy())
            {
                foreach (string name in names) Add(name);
                return;
            }
            await restart();
            RestartRequested = true;
        }
        finally { IsPromptActive = false; }
    }
}
