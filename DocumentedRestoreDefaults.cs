using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class DocumentedRestoreDefaults
{
    // Deliberately allow-listed. Deleting every tweak value is NOT a Windows reset:
    // for example deleting a driver service's Start value can prevent driver loading.
    // Source URLs and applicability are recorded in RESTORE_DEFAULTS_AUDIT_2026-09-09.md.
    internal static IReadOnlyList<RestoreRegistryValue> PerformanceLab(string id, IReadOnlyList<RestoreRegistryTarget> targets)
    {
        if (targets.Count == 1)
        {
            var target = targets[0];
            if (id == "UacSecureDesktopDimOff" && target.Hive == RegistryHive.LocalMachine &&
                target.Path.Equals(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", StringComparison.OrdinalIgnoreCase) &&
                target.Name == "PromptOnSecureDesktop")
                return new[] { new RestoreRegistryValue(target, 1, RegistryValueKind.DWord) };
            if (id == "NvTdr10" && target.Hive == RegistryHive.LocalMachine &&
                target.Path.Equals(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", StringComparison.OrdinalIgnoreCase) && target.Name == "TdrDelay")
                return new[] { new RestoreRegistryValue(target, null, null) };
            if (id == "LongPathsOn" && target.Hive == RegistryHive.LocalMachine &&
                target.Path.Equals(@"SYSTEM\CurrentControlSet\Control\FileSystem", StringComparison.OrdinalIgnoreCase) && target.Name == "LongPathsEnabled")
                return new[] { new RestoreRegistryValue(target, 0, RegistryValueKind.DWord) };
        }
        throw new InvalidOperationException("No original backup is available, and no verified Windows/vendor default is registered for this option on this PC. No values were changed. Restore requires a valid backup or a supported default definition.");
    }
}
