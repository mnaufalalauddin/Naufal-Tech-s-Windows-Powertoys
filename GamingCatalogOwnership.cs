namespace Naufal_Windows_Tech_s_Powertoys;

/// <summary>UI ownership only. Profile and legacy snapshot backends remain intact.</summary>
internal static class GamingCatalogOwnership
{
    internal const string Notice =
        "CPU, network, timer, and power-related performance settings are managed by Performance Profiles.";

    internal const string IndependentNotice =
        "The advanced CPU, adapter, and boot experiments below change separate settings; they are not part of profile verification.";

    internal static IToolToggleService Create(
        IToolToggleService windowsGaming,
        IToolToggleService bootExperiments,
        IToolToggleService registryExperiments) =>
        new CompositeToolToggleService(
            // DynamicTick and HPET write the same BCD options as profiles.
            // Other CPU/kernel/adapter experiments do NOT share those targets.
            new FilteredToolToggleService(windowsGaming,
                "GameMode", "GameDVR", "HAGS", "MPO", "WindowedOptimizations"),
            bootExperiments,
            registryExperiments);
}
