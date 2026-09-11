using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    /// <summary>
    /// Presents the final feature-oriented 41-row de-bloat catalog. The source
    /// utility builds 49 atomic backends, then merges recommendation, privacy,
    /// taskbar, and Explorer children into four user-facing controls.
    /// </summary>
    internal sealed class DebloatCatalogService : IToolToggleService
    {
        private readonly IReadOnlyDictionary<string, CatalogItem> _items;

        public DebloatCatalogService(
            DebloatService lowRisk,
            EssentialTweaksService essential,
            EssentialActionsService essentialActions,
            WindowsAiService windowsAi,
            DebloatRegistryLabService registryLab,
            DebloatNetworkStorageService networkStorage,
            XboxComponentsService xboxComponents,
            DebloatServiceGroupsService serviceGroups)
        {
            Dictionary<string, ChildItem> atomic = new(StringComparer.OrdinalIgnoreCase);

            AddSelected(atomic, essential,
                "Widgets", "ConsumerFeatures", "ExplorerHomeGallery",
                "Telemetry", "Location", "DeliveryOptimization");
            AddSelected(atomic, windowsAi, "WindowsAI");
            AddSelected(atomic, xboxComponents, "XboxComponents");
            AddSelected(atomic, lowRisk,
                "WindowsTips", "StartRecommendations", "SearchHighlights",
                "AdvertisingId", "TailoredExperiences", "SettingsSuggestedContent",
                "EdgeBackground", "OneDriveAutoStartup");
            AddSelected(atomic, registryLab,
                "NvidiaOverlay", "TaskbarChatOff", "TaskViewButtonOff",
                "NoNetCrawling", "StickyKeysHotkeysOff",
                "ModernStandbyOverride", "TpmCpuBypass");
            AddSelected(atomic, networkStorage,
                "Teredo", "StorageSense", "ReservedStorage");
            AddSelected(
                atomic,
                new StoreSearchToggleService(essentialActions),
                "StoreSearch");
            AddSelected(
                atomic,
                serviceGroups,
                serviceGroups.GetDefinitions().Select(definition => definition.Id).ToArray());

            List<CatalogItem> items = new()
            {
                Direct(atomic, "Widgets", "Components", ToolToggleTier.Safe),
                Direct(atomic, "WindowsAI", "Components", ToolToggleTier.Advanced),
                Direct(atomic, "XboxComponents", "Components", ToolToggleTier.Advanced),
                Direct(atomic, "NvidiaOverlay", "Startup", ToolToggleTier.Safe),
                Direct(atomic, "StickyKeysHotkeysOff", "System / HIGH RISK", ToolToggleTier.Advanced),
                Direct(atomic, "ModernStandbyOverride", "System / HIGH RISK", ToolToggleTier.VeryAggressive),
                Direct(atomic, "TpmCpuBypass", "System / Legacy", ToolToggleTier.VeryAggressive),
                Direct(atomic, "Teredo", "Network / Experimental", ToolToggleTier.Advanced),
                Direct(atomic, "StorageSense", "Storage / Experimental", ToolToggleTier.Advanced),
                Direct(atomic, "ReservedStorage", "Storage / HIGH RISK", ToolToggleTier.Advanced),
                Direct(atomic, "Telemetry", "Privacy / HIGH IMPACT", ToolToggleTier.VeryAggressive),
                Direct(atomic, "Location", "Privacy / Advanced", ToolToggleTier.Advanced),
                Direct(atomic, "DeliveryOptimization", "Privacy / Safe", ToolToggleTier.Safe),
                Direct(atomic, "EdgeBackground", "Startup / Safe", ToolToggleTier.Safe),
                Direct(atomic, "OneDriveAutoStartup", "Startup / Safe", ToolToggleTier.Safe)
            };

            foreach (ToolToggleDefinition group in serviceGroups.GetDefinitions())
            {
                ToolToggleTier tier = group.Id.Equals("BitsService", StringComparison.OrdinalIgnoreCase)
                    ? ToolToggleTier.Advanced
                    : group.Category.Contains("Safe", StringComparison.OrdinalIgnoreCase)
                        ? ToolToggleTier.Safe
                        : group.Category.Contains("HIGH", StringComparison.OrdinalIgnoreCase)
                            ? ToolToggleTier.VeryAggressive
                            : ToolToggleTier.Advanced;
                items.Add(Direct(atomic, group.Id, group.Category, tier));
            }

            items.Add(Composite(
                atomic,
                new ToolToggleDefinition(
                    "WindowsRecommendations",
                    "Components / Safe",
                    "Windows Recommendations / Suggested Content",
                    "Disables Windows consumer promotions, tips, Start recommendations, Search Highlights, suggested Settings content and Microsoft Store recommended-search promotions as one consolidated cleanup.",
                    true,
                    true,
                    ToolToggleTier.Safe),
                "ConsumerFeatures", "WindowsTips", "StartRecommendations",
                "SearchHighlights", "SettingsSuggestedContent", "StoreSearch"));
            items.Add(Composite(
                atomic,
                new ToolToggleDefinition(
                    "PersonalizedExperiences",
                    "Privacy / Safe",
                    "Advertising ID / Personalized Experiences",
                    "Disables the Windows Advertising ID and diagnostic-data-based tailored experiences used for personalized recommendations.",
                    false,
                    false,
                    ToolToggleTier.Safe),
                "AdvertisingId", "TailoredExperiences"));
            items.Add(Composite(
                atomic,
                new ToolToggleDefinition(
                    "TaskbarClutter",
                    "Components / Safe",
                    "Taskbar Optional Buttons / Clutter",
                    "Hides the optional Task View and Chat/Teams taskbar buttons without uninstalling their Windows features or applications.",
                    false,
                    false,
                    ToolToggleTier.Safe),
                "TaskbarChatOff", "TaskViewButtonOff"));
            items.Add(Composite(
                atomic,
                new ToolToggleDefinition(
                    "ExplorerCleanup",
                    "Components / Safe",
                    "File Explorer Cleanup / Background Discovery",
                    "Removes File Explorer Home/Gallery surface clutter and disables automatic network-folder crawling performed by Explorer.",
                    false,
                    true,
                    ToolToggleTier.Safe),
                "ExplorerHomeGallery", "NoNetCrawling"));

            if (items.Count != 41)
            {
                throw new InvalidOperationException(
                    $"The final de-bloat catalog must contain 41 rows, but contains {items.Count}.");
            }

            _items = items.ToDictionary(
                item => item.Definition.Id,
                StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<ToolToggleDefinition> GetDefinitions() =>
            _items.Values.Select(item => item.Definition).ToArray();

        public async Task<ToolToggleState> ReadStateAsync(ToolToggleDefinition definition)
        {
            CatalogItem item = Resolve(definition);
            List<ToolToggleState> states = new();
            foreach (ChildItem child in item.Children)
            {
                states.Add(await child.Service.ReadStateAsync(child.Definition));
            }
            return AggregateStates(states);
        }

        public async Task<ToolToggleOperationResult> SetStateAsync(
            ToolToggleDefinition definition,
            bool targetOn)
        {
            CatalogItem item = Resolve(definition);
            if (definition.RequiresAdministrator && !WindowsPrivilegeService.IsAdministrator())
            {
                ToolToggleState denied = await ReadStateAsync(definition);
                return new ToolToggleOperationResult(
                    false,
                    false,
                    "Administrator rights are required.",
                    denied);
            }

            List<string> failures = new();
            int applicable = 0;
            foreach (ChildItem child in item.Children)
            {
                ToolToggleState before = await child.Service.ReadStateAsync(child.Definition);
                if (!before.IsAvailable)
                {
                    if (before.HasReadFailure) failures.Add($"{child.Definition.Name}: {before.Error}");
                    continue;
                }

                applicable++;
                ToolToggleOperationResult result = await child.Service.SetStateAsync(
                    child.Definition,
                    targetOn);
                if (!result.Success || !result.Verified)
                {
                    failures.Add($"{child.Definition.Name}: {result.Message}");
                }
            }

            List<ToolToggleState> afterStates = new();
            foreach (ChildItem child in item.Children)
            {
                afterStates.Add(await child.Service.ReadStateAsync(child.Definition));
            }

            ToolToggleState aggregate = AggregateStates(afterStates);
            ToolToggleState[] availableAfter = afterStates
                .Where(state => state.IsAvailable)
                .ToArray();
            bool verified = applicable > 0 &&
                            failures.Count == 0 &&
                            aggregate.IsAvailable && availableAfter.Length > 0 &&
                            availableAfter.All(state => state.IsOn == targetOn);
            string message = verified
                ? $"{definition.Name} is now {(targetOn ? "APPLIED" : "RESTORED")} and all applicable child settings were verified."
                : failures.Count > 0
                    ? string.Join(Environment.NewLine, failures)
                    : applicable == 0
                        ? "No applicable target is installed on this PC."
                        : $"Verification did not match the requested state. Actual: {aggregate.ActualValue}";
            return new ToolToggleOperationResult(
                verified,
                verified,
                message,
                aggregate);
        }

        public Task<ToolToggleOperationResult> RestoreOriginalAsync(
            ToolToggleDefinition definition) =>
            RestoreChildrenAsync(definition, windowsDefault: false);

        public Task<ToolToggleOperationResult> RestoreWindowsDefaultAsync(
            ToolToggleDefinition definition) =>
            RestoreChildrenAsync(definition, windowsDefault: true);

        private async Task<ToolToggleOperationResult> RestoreChildrenAsync(
            ToolToggleDefinition definition,
            bool windowsDefault)
        {
            CatalogItem item = Resolve(definition);
            if (definition.RequiresAdministrator &&
                !WindowsPrivilegeService.IsAdministrator())
            {
                ToolToggleState denied = await ReadStateAsync(definition);
                return new ToolToggleOperationResult(
                    false,
                    false,
                    "Administrator rights are required.",
                    denied);
            }

            List<string> failures = new();
            int applicable = 0;
            foreach (ChildItem child in item.Children)
            {
                ToolToggleState before = await child.Service.ReadStateAsync(child.Definition);
                if (!before.IsAvailable)
                {
                    if (before.HasReadFailure) failures.Add($"{child.Definition.Name}: {before.Error}");
                    continue;
                }

                applicable++;
                // Fallback belongs to this child only. Never reset successfully
                // restored siblings to defaults because another snapshot is missing.
                ToolToggleOperationResult result = await CatalogOperationRunner.ExecuteAsync(
                    child.Service, child.Definition, windowsDefault
                        ? CatalogOperation.RestoreWindowsDefaults
                        : CatalogOperation.RestoreSavedState);
                if (!result.SkippedUnavailable && (!result.Success || !result.Verified))
                {
                    failures.Add($"{child.Definition.Name}: {result.Message}");
                }
            }

            ToolToggleState aggregate = await ReadStateAsync(definition);
            bool verified = applicable > 0 && failures.Count == 0 && aggregate.IsAvailable;
            string target = windowsDefault
                ? "Windows defaults"
                : "the saved state or the documented Windows service default";
            string message = verified
                ? $"{definition.Name} was restored to {target}."
                : failures.Count > 0
                    ? string.Join(Environment.NewLine, failures)
                    : "No applicable target is installed on this PC.";
            return new ToolToggleOperationResult(
                verified,
                verified,
                message,
                aggregate, DefaultFallbackHandled: true);
        }

        private CatalogItem Resolve(ToolToggleDefinition definition) =>
            _items.TryGetValue(definition.Id, out CatalogItem? item)
                ? item
                : throw new KeyNotFoundException(definition.Id);

        private static ToolToggleState AggregateStates(IReadOnlyList<ToolToggleState> states) =>
            CatalogAvailability.Aggregate(states);

        private static CatalogItem Direct(
            IReadOnlyDictionary<string, ChildItem> atomic,
            string id,
            string category,
            ToolToggleTier tier)
        {
            ChildItem child = RequireAtomic(atomic, id);
            ToolToggleDefinition source = child.Definition;
            ToolToggleDefinition visible = source with
            {
                Category = category,
                SelectionTier = tier
            };
            return new CatalogItem(visible, new[] { child });
        }

        private static CatalogItem Composite(
            IReadOnlyDictionary<string, ChildItem> atomic,
            ToolToggleDefinition definition,
            params string[] childIds) =>
            new(
                definition,
                childIds.Select(id => RequireAtomic(atomic, id)).ToArray());

        private static ChildItem RequireAtomic(
            IReadOnlyDictionary<string, ChildItem> atomic,
            string id) =>
            atomic.TryGetValue(id, out ChildItem? item)
                ? item
                : throw new InvalidOperationException($"Missing de-bloat backend: {id}");

        private static void AddSelected(
            IDictionary<string, ChildItem> target,
            IToolToggleService service,
            params string[] ids)
        {
            IReadOnlyDictionary<string, ToolToggleDefinition> available = service
                .GetDefinitions()
                .ToDictionary(definition => definition.Id, StringComparer.OrdinalIgnoreCase);
            foreach (string id in ids)
            {
                if (!available.TryGetValue(id, out ToolToggleDefinition definition))
                {
                    throw new InvalidOperationException($"Missing source toggle definition: {id}");
                }
                if (!target.TryAdd(id, new ChildItem(service, definition)))
                {
                    throw new InvalidOperationException($"Duplicate de-bloat backend id: {id}");
                }
            }
        }

        private sealed record ChildItem(
            IToolToggleService Service,
            ToolToggleDefinition Definition);

        private sealed record CatalogItem(
            ToolToggleDefinition Definition,
            IReadOnlyList<ChildItem> Children);
    }

    internal sealed class StoreSearchToggleService : IToolToggleService
    {
        private readonly EssentialActionsService _actions;
        private static readonly ToolToggleDefinition Definition = new(
            "StoreSearch",
            "Components / Safe",
            "Microsoft Store Recommended Search Results",
            "Blocks Microsoft Store recommended-search promotion injection where the supported Store database is available.",
            false,
            false,
            ToolToggleTier.Safe);

        public StoreSearchToggleService(EssentialActionsService actions)
        {
            _actions = actions;
        }

        public IReadOnlyList<ToolToggleDefinition> GetDefinitions() =>
            new[] { Definition };

        public Task<ToolToggleState> ReadStateAsync(ToolToggleDefinition definition) =>
            _actions.ReadStoreSearchStateAsync();

        public Task<ToolToggleOperationResult> SetStateAsync(
            ToolToggleDefinition definition,
            bool targetOn) =>
            _actions.SetStoreSearchStateAsync(targetOn);
    }
}
