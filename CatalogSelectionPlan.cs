using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

// Selection chooses the operation's scope, never its ON/OFF direction.
// Apply always targets the applied state; Restore replays the saved snapshot.
internal sealed class CatalogSelectionPlan
{
    public IReadOnlyList<ToolToggleDefinition> Selected { get; }
    public IReadOnlyList<ToolToggleDefinition> ToApply { get; }
    public IReadOnlyList<ToolToggleDefinition> ToRestore { get; }
    public int AlreadyAppliedCount => Selected.Count - ToApply.Count;
    public int AlreadyRestoredCount => Selected.Count - ToRestore.Count;

    private CatalogSelectionPlan(
        List<ToolToggleDefinition> selected,
        List<ToolToggleDefinition> toApply,
        List<ToolToggleDefinition> toRestore)
    {
        Selected = selected.AsReadOnly();
        ToApply = toApply.AsReadOnly();
        ToRestore = toRestore.AsReadOnly();
    }

    public static CatalogSelectionPlan Create(
        IReadOnlyList<ToolToggleDefinition> definitions,
        IReadOnlyDictionary<string, ToolToggleState> states,
        Func<ToolToggleDefinition, bool> isSelected)
    {
        List<ToolToggleDefinition> selected = new();
        List<ToolToggleDefinition> toApply = new();
        List<ToolToggleDefinition> toRestore = new();
        foreach (ToolToggleDefinition definition in definitions)
        {
            if (!isSelected(definition) ||
                !states.TryGetValue(definition.Id, out ToolToggleState state) ||
                !state.IsAvailable)
            {
                continue;
            }

            selected.Add(definition);
            if (!state.IsOn)
            {
                toApply.Add(definition);
            }
            if (state.IsOn || state.HasAppliedParts || definition.IsFeatureSwitch)
            {
                toRestore.Add(definition);
            }
        }
        return new CatalogSelectionPlan(selected, toApply, toRestore);
    }
}

internal enum CatalogOperation
{
    Apply,
    RestoreSavedState,
    RestoreWindowsDefaults,
    SetOff
}

internal static class CatalogOperationRunner
{
    public static async Task<ToolToggleOperationResult> ExecuteAsync(
        IToolToggleService service,
        ToolToggleDefinition definition,
        CatalogOperation operation,
        IProgress<CatalogProgressUpdate>? progress = null)
    {
        using var progressScope = CatalogOperationProgress.Begin(progress);
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        // Re-check after queue admission: hardware/packages may have disappeared
        // since Analyze. Never turn a failed WRITE into a neutral absence result.
        ToolToggleState before = await CatalogStateReader.ReadAsync(service, definition, TimeSpan.FromSeconds(30));
        // Task-returning backends can still perform registry, ACL, snapshot or
        // hardware work synchronously before their first await. Keep all of it
        // off the XAML dispatcher, including exact/default fallback Restore.
        ToolToggleOperationResult result = await Task.Run(() =>
            ExecuteWithStateAsync(service, definition, operation, before));
        return result with { BeforeState = before };
    }

    private static async Task<ToolToggleOperationResult> ExecuteWithStateAsync(
        IToolToggleService service, ToolToggleDefinition definition, CatalogOperation operation, ToolToggleState before)
    {
        if (before.IsConfirmedUnavailable)
            return new(false, false, before.Error, before, SkippedUnavailable: true);
        if (before.HasReadFailure)
            return new(false, false, before.Error, before);
        if (operation == CatalogOperation.RestoreSavedState)
        {
            ToolToggleOperationResult exact = await service.RestoreOriginalAsync(definition);
            // Only the backend can establish actual backup absence. An English
            // error containing "snapshot unavailable" may mean denial/corruption.
            if (exact.Success || exact.DefaultFallbackHandled || !exact.OriginalBackupMissing)
            {
                return exact;
            }

            ToolToggleOperationResult fallback =
                await service.RestoreWindowsDefaultAsync(definition);
            string explanation = fallback.Success
                ? "No saved pre-change state was available, so the documented Windows default was restored."
                : "No saved pre-change state was available, and the documented Windows-default fallback also failed.";
            return fallback with
            {
                Message = explanation + " " + fallback.Message
            };
        }

        return operation switch
        {
            CatalogOperation.Apply =>
                await service.SetStateAsync(definition, targetOn: true),
            CatalogOperation.SetOff =>
                await service.SetStateAsync(definition, targetOn: false),
            CatalogOperation.RestoreWindowsDefaults =>
                await service.RestoreWindowsDefaultAsync(definition),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }

}
