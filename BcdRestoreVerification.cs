using System;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class BcdRestoreVerification
{
    public static bool Matches(string option, string? expected, string? actual)
    {
        if (expected is null || actual is null) return expected is null && actual is null;
        if (option.ToLowerInvariant() is "debug" or "bootdebug" or "sos" or "highestmode" or "disabledynamictick" or "useplatformclock")
            return string.Equals(NormalizeBoolean(expected), NormalizeBoolean(actual), StringComparison.OrdinalIgnoreCase);
        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeBoolean(string value) => value.ToLowerInvariant() switch
    {
        "yes" or "on" or "true" => "true",
        "no" or "off" or "false" => "false",
        _ => value
    };
}
