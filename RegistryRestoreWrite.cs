using Microsoft.Win32;
using System;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class RegistryRestoreWrite
{
    // Do not request write access to protected service keys when the value is
    // already correct (including a saved absent DelayedAutoStart).
    internal static bool Apply(Func<(object? Value, RegistryValueKind? Kind)> read,
        object? expected, RegistryValueKind? kind, Action write)
    {
        var actual = read();
        if (Equals(actual.Value, expected) && (expected is null || actual.Kind == kind)) return false;
        write();
        actual = read();
        if (!Equals(actual.Value, expected) || (expected is not null && actual.Kind != kind))
            throw new InvalidOperationException("Restored registry value did not match its saved state. Backup retained.");
        return true;
    }
}
