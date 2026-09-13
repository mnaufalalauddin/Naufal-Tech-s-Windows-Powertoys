using System.Collections.Generic;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal enum ToolToggleTier
    {
        Unspecified,
        Safe,
        Advanced,
        VeryAggressive
    }

    internal readonly record struct ToolToggleDefinition(
        string Id,
        string Category,
        string Name,
        string Description,
        bool RequiresAdministrator,
        bool RestartRecommended,
        ToolToggleTier SelectionTier = ToolToggleTier.Unspecified,
        string Warning = "",
        bool IsFeatureSwitch = false)
    {
        public string Name { get; init; } = CatalogDisplayNames.Simplify(Name);
    }

    internal readonly record struct ToolToggleState(
        bool IsOn,
        bool IsAvailable,
        string ActualValue,
        string Error = "",
        bool HasAppliedParts = false,
        bool UnavailableOnThisPc = false)
    {
        // Only an explicit, successful absence probe may set this flag. An
        // unreadable registry, timeout or missing backup is NOT unavailability.
        public bool IsConfirmedUnavailable => !IsAvailable && UnavailableOnThisPc;
        public bool HasReadFailure => !IsAvailable && !IsConfirmedUnavailable;
        public static ToolToggleState Unavailable(string reason) =>
            new(false, false, "Unavailable", reason, UnavailableOnThisPc: true);
    }

    internal readonly record struct ToolToggleOperationResult(
        bool Success,
        bool Verified,
        string Message,
        ToolToggleState State,
        bool DefaultFallbackHandled = false,
        bool SkippedUnavailable = false,
        bool OriginalBackupMissing = false,
        ToolToggleState? BeforeState = null);

    internal interface IToolToggleService
    {
        IReadOnlyList<ToolToggleDefinition> GetDefinitions();

        Task<ToolToggleState> ReadStateAsync(ToolToggleDefinition definition);

        Task<ToolToggleOperationResult> SetStateAsync(
            ToolToggleDefinition definition,
            bool targetOn);

        // Restore replays the first-apply snapshot. Windows Default is a
        // separate operation in the reference application and must never be
        // represented by merely forcing the row toggle to OFF.
        Task<ToolToggleOperationResult> RestoreOriginalAsync(
            ToolToggleDefinition definition) =>
            SetStateAsync(definition, targetOn: false);

        Task<ToolToggleOperationResult> RestoreWindowsDefaultAsync(
            ToolToggleDefinition definition) =>
            RestoreOriginalAsync(definition);
    }
}
