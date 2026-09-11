using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class TeredoConfiguration
{
    // MSFT_NetTeredoConfiguration.Type (NetworkTransition CDXML). This is
    // configuration, not netsh's effective tunnel state/Internet connectivity.
    internal static string TypeName(int type) => type switch
    {
        0 => "Default", 1 => "Relay", 2 => "Client", 3 => "Server",
        4 => "Disabled", 5 => "Automatic", 6 => "Enterpriseclient",
        7 => "Natawareclient", _ => throw new InvalidDataException("Unknown Teredo configuration type.")
    };

    internal static int ReadActiveType(IReadOnlyList<Dictionary<string, string>> rows)
    {
        var active = rows.Where(row => row.TryGetValue("PolicyStore", out string? store) &&
            store.Equals("ActiveStore", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (active.Length != 1 || !active[0].TryGetValue("Type", out string? raw) ||
            !int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int type))
            throw new InvalidDataException("Teredo configuration could not be read unambiguously. No state was verified.");
        _ = TypeName(type);
        return type;
    }

    internal static ToolToggleState ToState(int type) => new(type == 4, true, "Configured type: " + TypeName(type));
    internal static bool MatchesTarget(ToolToggleState state, bool disabled) =>
        state.IsAvailable && state.ActualValue == ToState(disabled ? 4 : 0).ActualValue;
}
