using System;
using System.Collections.Generic;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

// Explicit, reviewed dependency rules only; do not merge similar names blindly.
internal static class CatalogPlanSafety
{
    internal static IReadOnlyList<string> ApplyConflicts(IEnumerable<ToolToggleDefinition> selected)
    {
        var ids = selected.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<string> conflicts = [];
        if (ids.Contains("Hibernation") && ids.Contains("FastStartupEnable"))
            conflicts.Add("Disable Hibernation conflicts with enabling Fast Startup, which requires hibernation. Select only one of these actions; nothing has been applied.");
        if (ids.Any(LegacyMitigationPolicy.IsBlocked)) conflicts.Add(LegacyMitigationPolicy.ApplyBlocked);
        return conflicts;
    }
}
