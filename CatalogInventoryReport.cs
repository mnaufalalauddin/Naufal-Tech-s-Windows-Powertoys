using System;
using System.Collections.Generic;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class CatalogInventoryReport
{
    internal static IReadOnlyList<SystemReportEntry> Build(IEnumerable<(string Name, IToolToggleService Service)> catalogs)
    {
        List<SystemReportEntry> rows = [new("STATIC ACTION INVENTORY", "No detection or system changes performed; presence/support is determined when analyzing each catalog.", true)];
        List<(string Owner, CatalogEffect Effect)> targets = [];
        int options = 0, declared = 0, undeclared = 0;
        foreach (var catalog in catalogs)
        foreach (var definition in catalog.Service.GetDefinitions())
        {
            options++;
            var leaves = CatalogEffectPlan.Expand(catalog.Service, definition);
            rows.Add(new(catalog.Name + " / " + definition.Name, definition.Id, true));
            rows.Add(new("Stable ID / UI owner", definition.Id + " / " + catalog.Name, false));
            rows.Add(new("Category / risk", definition.Category + " / " + definition.SelectionTier, false));
            rows.Add(new("Effects / lost functions", definition.Description, false));
            rows.Add(new("Warning", string.IsNullOrWhiteSpace(definition.Warning) ? "See description and Apply preview." : definition.Warning, false));
            rows.Add(new("Elevation / restart", $"Administrator={definition.RequiresAdministrator}; restart-sensitive={definition.RestartRecommended}", false));
            rows.Add(new("Evidence", "No savings measured for this action. Configuration verification is not proof of resource improvement or post-reboot effectiveness.", false));
            foreach (var leaf in leaves)
            {
                rows.Add(new("Canonical backend", leaf.Key, false));
                var effects = leaf.Effects;
                if (effects.Count == 0)
                {
                    undeclared++;
                    rows.Add(new("Target coverage", "Not yet declared. Read this backend's implementation; no full effect-deduplication claim.", false));
                }
                else
                {
                    declared++;
                    foreach (var effect in effects)
                    {
                        rows.Add(new(effect.Target, effect.Desired, false));
                        if (effect.SharedOriginalOwner is not null)
                            rows.Add(new("Shared original snapshot", effect.SharedOriginalOwner +
                                "; legacy originals must agree; actual snapshot validity is checked before changes.", false));
                        targets.Add((leaf.Key, effect));
                    }
                }
            }
            rows.Add(new("Recovery", "Owned by the backend above: original snapshot where implemented, or documented fallback. Missing backup is not permission to invent defaults.", false));
        }
        rows.Insert(1, new("Inventory coverage", $"{options} visible options; {declared} leaf references with target declarations; {undeclared} without. Composite references can repeat. Some declared effects are partial.", false));
        rows.Add(new("SHARED TARGET AUDIT", "Overlaps are not necessarily bugs: scope, restore owner and alternative requested values must be reviewed. This list is not an Apply plan.", true));
        foreach (var group in targets.GroupBy(t => t.Effect.Target, StringComparer.OrdinalIgnoreCase))
        {
            var owners = group.Distinct().ToArray();
            if (owners.Select(o => o.Owner).Distinct(StringComparer.OrdinalIgnoreCase).Count() < 2) continue;
            rows.Add(new(group.Key, string.Join(" | ", owners.Select(o => o.Owner + " => " + o.Effect.Desired)), false));
        }
        return rows.AsReadOnly();
    }
}
