using System;
using System.Collections.Generic;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

internal enum ToolActionImpact
{
    Unknown,
    RuntimeOptimization,
    StorageReduction,
    ConditionalWorkloadDependent,
    PreferenceCosmetic,
    AdvancedSecurityMitigation,
    MaintenanceRepair
}

internal enum ToolActionEvidence
{
    MechanismUnmeasured,
    Measured,
    Experimental,
    NotApplicable
}

internal sealed class CanonicalCatalogPlan
{
    public IReadOnlyList<ToolToggleDefinition> Ordered { get; }
    public int DuplicateCount { get; }

    private CanonicalCatalogPlan(List<ToolToggleDefinition> ordered, int duplicateCount)
    {
        Ordered = ordered.AsReadOnly();
        DuplicateCount = duplicateCount;
    }

    internal static string Key(ToolToggleDefinition definition) =>
        string.IsNullOrWhiteSpace(definition.CanonicalActionId)
            ? definition.Id
            : definition.CanonicalActionId.Trim();

    public static CanonicalCatalogPlan Create(
        IReadOnlyList<ToolToggleDefinition> catalog,
        Func<ToolToggleDefinition, bool> isSelected)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(isSelected);

        Dictionary<string, ToolToggleDefinition> all = new(StringComparer.OrdinalIgnoreCase);
        foreach (ToolToggleDefinition definition in catalog)
        {
            string key = Key(definition);
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("A catalog action has no stable ID.");

            if (all.TryGetValue(key, out ToolToggleDefinition existing) &&
                !Equivalent(existing, definition))
            {
                throw new InvalidOperationException(
                    $"Canonical action '{key}' has incompatible duplicate definitions.");
            }
            all.TryAdd(key, definition);
        }

        Dictionary<string, ToolToggleDefinition> chosen = new(StringComparer.OrdinalIgnoreCase);
        int duplicateCount = 0;
        foreach (ToolToggleDefinition definition in catalog.Where(isSelected))
        {
            string key = Key(definition);
            if (!chosen.TryAdd(key, all[key])) duplicateCount++;
        }

        // Dependencies are canonical action references. They are added once,
        // recursively, before their dependants.
        Queue<ToolToggleDefinition> pending = new(chosen.Values);
        while (pending.Count > 0)
        {
            ToolToggleDefinition definition = pending.Dequeue();
            foreach (string dependency in definition.DependsOn ?? Array.Empty<string>())
            {
                if (!all.TryGetValue(dependency, out ToolToggleDefinition required))
                    throw new InvalidOperationException(
                        $"Action '{Key(definition)}' requires missing action '{dependency}'.");
                if (chosen.TryAdd(Key(required), required)) pending.Enqueue(required);
            }
        }

        foreach (ToolToggleDefinition definition in chosen.Values)
        {
            foreach (string conflict in definition.ConflictsWith ?? Array.Empty<string>())
            {
                if (chosen.ContainsKey(conflict))
                    throw new InvalidOperationException(
                        $"Action '{Key(definition)}' conflicts with '{conflict}'.");
            }
        }

        List<ToolToggleDefinition> ordered = new();
        HashSet<string> visiting = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);

        void Visit(ToolToggleDefinition definition)
        {
            string key = Key(definition);
            if (visited.Contains(key)) return;
            if (!visiting.Add(key))
                throw new InvalidOperationException(
                    $"Dependency cycle detected at canonical action '{key}'.");

            foreach (string dependency in definition.DependsOn ?? Array.Empty<string>())
                Visit(chosen[dependency]);

            visiting.Remove(key);
            visited.Add(key);
            ordered.Add(definition);
        }

        foreach (ToolToggleDefinition definition in chosen.Values) Visit(definition);
        return new CanonicalCatalogPlan(ordered, duplicateCount);
    }

    private static bool Equivalent(ToolToggleDefinition left, ToolToggleDefinition right)
    {
        return string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase) &&
               left.RequiresAdministrator == right.RequiresAdministrator &&
               left.RestartRecommended == right.RestartRecommended &&
               left.IsFeatureSwitch == right.IsFeatureSwitch &&
               left.Impact == right.Impact;
    }
}
