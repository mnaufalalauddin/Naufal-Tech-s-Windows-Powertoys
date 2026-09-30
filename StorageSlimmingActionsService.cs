using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed class StorageSlimmingActionsService : IToolActionService
{
    private readonly NativeCommandRunner _runner = new();

    private static readonly IReadOnlyList<ToolActionDefinition> Actions = new[]
    {
        new ToolActionDefinition(
            "ComponentStoreAnalyze", "Storage / Component Store", "Component Store Analysis",
            "Runs DISM AnalyzeComponentStore. This is read-only analysis and does not claim reclaimed space.",
            "Analyze", true, false,
            Impact: ToolActionImpact.StorageReduction,
            Evidence: ToolActionEvidence.MechanismUnmeasured,
            CanonicalActionId: "storage.component-store.analyze"),
        new ToolActionDefinition(
            "ComponentStoreCleanup", "Storage / Component Store", "Component Store Cleanup",
            "Runs the supported DISM StartComponentCleanup operation. Actual free-space change must be measured separately.",
            "Clean up", true, false,
            Confirmation: "Clean superseded component-store content with DISM? Installed servicing remains available according to DISM rules.",
            Risk: ToolActionRisk.Warning,
            Impact: ToolActionImpact.StorageReduction,
            Evidence: ToolActionEvidence.MechanismUnmeasured,
            CanonicalActionId: "storage.component-store.cleanup"),
        new ToolActionDefinition(
            "ComponentStoreResetBase", "Storage / Component Store / IRREVERSIBLE", "Component Store ResetBase",
            "Runs DISM StartComponentCleanup /ResetBase. Installed updates can no longer be uninstalled after the base is reset.",
            "Reset base", true, false,
            Confirmation: "Reset the component-store base? This is irreversible for currently installed update uninstallability.",
            Risk: ToolActionRisk.Danger,
            Warning: "Irreversible: installed updates cannot be uninstalled after ResetBase.",
            Impact: ToolActionImpact.StorageReduction,
            Evidence: ToolActionEvidence.MechanismUnmeasured,
            CanonicalActionId: "storage.component-store.resetbase")
    };

    public IReadOnlyList<ToolActionDefinition> GetActions() => Actions;

    public Task<ToolActionResult> RestoreAsync(ToolActionDefinition definition) =>
        Task.FromResult(new ToolActionResult(false,
            $"{definition.Name} has no automatic rollback. Component payload recovery follows Windows servicing/source rules."));

    public async Task<ToolActionResult> RunAsync(ToolActionDefinition definition)
    {
        if (!WindowsPrivilegeService.IsAdministrator())
            return new(false, "Administrator rights are required.");

        string[] args = definition.Id switch
        {
            "ComponentStoreAnalyze" => new[] { "/English", "/Online", "/Cleanup-Image", "/AnalyzeComponentStore" },
            "ComponentStoreCleanup" => new[] { "/English", "/Online", "/Cleanup-Image", "/StartComponentCleanup", "/NoRestart" },
            "ComponentStoreResetBase" => new[] { "/English", "/Online", "/Cleanup-Image", "/StartComponentCleanup", "/ResetBase", "/NoRestart" },
            _ => throw new InvalidOperationException($"Unknown storage action: {definition.Id}")
        };

        CatalogOperationProgress.Current?.Report(new("Running DISM component-store operation", null));
        NativeCommandResult result = await _runner.RunAsync(
            "dism.exe", args, TimeSpan.FromMinutes(definition.Id == "ComponentStoreAnalyze" ? 10 : 45),
            outputProgress: new CommandOutputProgress(CatalogOperationProgress.Current, 1, 1, definition.Name),
            outputEncoding: System.Text.Encoding.Unicode);

        if (result.TimedOut)
            return new(false, "DISM timed out. The operation is not reported as completed.");
        if (result.ExitCode != 0)
            return new(false, $"DISM exited with code {result.ExitCode}. {result.StandardError}".Trim());

        string note = definition.Id == "ComponentStoreAnalyze"
            ? "DISM analysis completed. Review DISM output; no cleanup was performed."
            : definition.Id == "ComponentStoreResetBase"
                ? "DISM ResetBase completed. No reclaimed-space figure is claimed; installed update uninstallability is now restricted by ResetBase semantics."
                : "DISM component-store cleanup completed. No reclaimed-space figure is claimed until free space is measured.";
        return new(true, note);
    }
}
