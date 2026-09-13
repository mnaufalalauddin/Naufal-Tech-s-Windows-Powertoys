using System;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class GameModeSetting
{
    internal static void Apply(bool enabled, Action captureFirstState, Action<int> write)
    {
        captureFirstState();
        write(enabled ? 1 : 0);
    }
}

internal static class CatalogTogglePolicy
{
    internal static CatalogOperation FromSwitch(ToolToggleDefinition definition, bool enabled) =>
        enabled ? CatalogOperation.Apply : definition.IsFeatureSwitch ? CatalogOperation.SetOff : CatalogOperation.RestoreSavedState;
}
