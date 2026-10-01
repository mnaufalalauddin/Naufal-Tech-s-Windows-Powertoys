using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed record CatalogEffect(string Target, string Desired)
{
    // Set only by backends using the same durable original and retirement protocol.
    internal string? SharedOriginalOwner { get; init; }
    internal static CatalogEffect Registry(string hive, string path, string name, string kind, object? value, bool remove = false)
    {
        string root = hive.Replace(":", "").Trim().ToUpperInvariant() switch
        {
            "HKLM" or "LOCALMACHINE" => "HKLM",
            "HKCU" or "CURRENTUSER" => "HKCU",
            "HKCR" or "CLASSESROOT" => "HKCR",
            "HKU" or "USERS" => "HKU",
            _ => hive.ToUpperInvariant()
        };
        return new($"Registry/{root}/{path.Replace('/', '\\').Trim('\\')}/{name}".ToUpperInvariant(),
            remove ? "<absent>" : kind + ":" + Convert.ToString(value, CultureInfo.InvariantCulture));
    }
}

internal interface ICatalogEffectSource
{
    // Configuration targets, not a claim that Windows implements every policy.
    IReadOnlyList<CatalogEffect> GetEffects(ToolToggleDefinition definition);
}

internal interface ICatalogPlanSource
{
    IReadOnlyList<CatalogPlanAction> GetPlanActions(ToolToggleDefinition definition);
}

internal sealed record CatalogPlanAction(IToolToggleService Service, ToolToggleDefinition Definition)
{
    internal string Key => Service.GetType().FullName + ":" + Definition.Id.ToUpperInvariant();
    internal IReadOnlyList<CatalogEffect> Effects => Service is ICatalogEffectSource source ? source.GetEffects(Definition) : Array.Empty<CatalogEffect>();
}

internal sealed class CatalogEffectPlan
{
    internal IReadOnlyList<CatalogPlanAction> Actions { get; }
    internal IReadOnlyList<string> Conflicts { get; }
    internal IReadOnlyList<string> SharedEffects { get; }
    internal int RepeatedReferences { get; }
    internal int UndeclaredActions => Actions.Count(a => a.Effects.Count == 0);
    private CatalogEffectPlan(List<CatalogPlanAction> actions, List<string> conflicts, List<string> shared, int duplicates)
        => (Actions, Conflicts, SharedEffects, RepeatedReferences) = (actions.AsReadOnly(), conflicts.AsReadOnly(), shared.AsReadOnly(), duplicates);

    internal static IReadOnlyList<CatalogPlanAction> Expand(IToolToggleService service, ToolToggleDefinition definition) =>
        service is ICatalogPlanSource source ? source.GetPlanActions(definition) : new[] { new CatalogPlanAction(service, definition) };

    internal static CatalogEffectPlan Create(IToolToggleService service, IEnumerable<ToolToggleDefinition> definitions)
    {
        Dictionary<string, CatalogPlanAction> unique = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, (CatalogEffect Effect, string Owner)> effects = new(StringComparer.OrdinalIgnoreCase);
        List<string> conflicts = [], shared = [];
        int duplicates = 0;
        foreach (var definition in definitions)
        foreach (var action in Expand(service, definition))
        {
            if (unique.TryGetValue(action.Key, out var previous))
            {
                if (previous.Definition != action.Definition) conflicts.Add($"Incompatible definitions for canonical action {action.Key}.");
                duplicates++;
                continue;
            }
            unique.Add(action.Key, action);
            foreach (var effect in action.Effects)
            {
                if (effects.TryGetValue(effect.Target, out var prior))
                {
                    if (prior.Effect.Desired != effect.Desired)
                        conflicts.Add($"{effect.Target}: {prior.Owner} requests {prior.Effect.Desired}; {action.Definition.Id} requests {effect.Desired}.");
                    else if (!string.Equals(prior.Owner, action.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        string overlap = $"{effect.Target}: {prior.Owner} and {action.Definition.Id} both request {effect.Desired}.";
                        shared.Add(overlap);
                        if (string.IsNullOrWhiteSpace(effect.SharedOriginalOwner) ||
                            !string.Equals(effect.SharedOriginalOwner, prior.Effect.SharedOriginalOwner, StringComparison.Ordinal))
                            conflicts.Add("Shared target has independent restore owners; select one owner: " + overlap);
                    }
                }
                else effects.Add(effect.Target, (effect, action.Key));
            }
        }
        // Shared ownership is not inferred from equal desired values. The narrow
        // migrated aliases opt in; all other independent restore owners stay blocked.
        return new(unique.Values.ToList(), conflicts, shared, duplicates);
    }
}
