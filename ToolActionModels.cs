using System.Collections.Generic;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal enum ToolActionRisk
    {
        Default,
        Primary,
        Success,
        Warning,
        Danger
    }

    internal readonly record struct ToolActionDefinition(
        string Id,
        string Category,
        string Name,
        string Description,
        string RunLabel,
        bool RequiresAdministrator,
        bool SupportsRestore,
        string Confirmation = "",
        string RestoreConfirmation = "",
        string RestoreLabel = "Restore",
        ToolActionRisk Risk = ToolActionRisk.Default,
        string Warning = "")
    {
        public string Name { get; init; } = CatalogDisplayNames.Simplify(Name);
    }

    internal readonly record struct ToolActionResult(
        bool Success,
        string Message,
        bool SkippedUnavailable = false);

    internal interface IToolActionService
    {
        IReadOnlyList<ToolActionDefinition> GetActions();

        Task<ToolActionResult> RunAsync(ToolActionDefinition definition);

        Task<ToolActionResult> RestoreAsync(ToolActionDefinition definition);
    }
}
