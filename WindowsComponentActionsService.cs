using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed class WindowsComponentActionsService : IToolActionService
{
    private readonly NativeCommandRunner _runner = new();
    private static readonly IReadOnlyList<ToolActionDefinition> Actions = new[]
    {
        new ToolActionDefinition("ComponentCleanupAnalyze", "Windows Components", "Analyze Component Store",
            "Read-only DISM component-store analysis.", "Analyze", true, false,
            CanonicalActionId: "components.store.analyze", Impact: ToolActionImpact.MaintenanceRepair, Evidence: ToolActionEvidence.NotApplicable),
        new ToolActionDefinition("CapabilityList", "Windows Components", "List Windows Capabilities",
            "Exports exact capability identities and states before any manual capability change.", "Export list", true, false,
            CanonicalActionId: "components.capabilities.list", Impact: ToolActionImpact.MaintenanceRepair, Evidence: ToolActionEvidence.NotApplicable),
        new ToolActionDefinition("FeatureList", "Windows Components", "List Optional Features",
            "Exports exact optional-feature identities and states before any manual feature change.", "Export list", true, false,
            CanonicalActionId: "components.features.list", Impact: ToolActionImpact.MaintenanceRepair, Evidence: ToolActionEvidence.NotApplicable),
        new ToolActionDefinition("LanguageList", "Windows Components", "List Installed User Languages",
            "Exports the current user's language list and system locale. No language is removed.", "Export languages", false, false,
            CanonicalActionId: "components.languages.list", Impact: ToolActionImpact.MaintenanceRepair, Evidence: ToolActionEvidence.NotApplicable)
    };

    public IReadOnlyList<ToolActionDefinition> GetActions() => Actions;
    public Task<ToolActionResult> RestoreAsync(ToolActionDefinition definition) =>
        Task.FromResult(new ToolActionResult(false, "Read-only/export action; no restore is necessary."));

    public async Task<ToolActionResult> RunAsync(ToolActionDefinition definition)
    {
        if (definition.RequiresAdministrator && !WindowsPrivilegeService.IsAdministrator())
            return new(false, "Administrator rights are required.");
        (string exe, string[] args) = definition.Id switch
        {
            "ComponentCleanupAnalyze" => ("dism.exe", new[] { "/English", "/Online", "/Cleanup-Image", "/AnalyzeComponentStore" }),
            "CapabilityList" => ("dism.exe", new[] { "/English", "/Online", "/Get-Capabilities" }),
            "FeatureList" => ("dism.exe", new[] { "/English", "/Online", "/Get-Features", "/Format:Table" }),
            "LanguageList" => ("powershell.exe", new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "Get-WinUserLanguageList | Format-List *; Get-WinSystemLocale | Format-List *" }),
            _ => throw new InvalidOperationException(definition.Id)
        };
        NativeCommandResult result = await _runner.RunAsync(exe, args, TimeSpan.FromMinutes(10));
        if (result.ExitCode != 0 || result.TimedOut) return new(false, result.CombinedOutput);
        string dir = Path.Combine(AppDataPaths.LocalRoot, "Logs", "Components");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, definition.Id + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".txt");
        await File.WriteAllTextAsync(path, result.StandardOutput, new UTF8Encoding(false));
        return new(true, "Exported: " + path);
    }
}
