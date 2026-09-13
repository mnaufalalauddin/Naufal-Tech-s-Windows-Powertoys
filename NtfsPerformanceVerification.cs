using Microsoft.Win32;
using System.Globalization;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed record NtfsPerformanceState(object? LastAccess, RegistryValueKind? LastAccessKind,
    object? ShortNames, RegistryValueKind? ShortNamesKind)
{
    // This preset explicitly requests USER-managed disabled (mode 1), not the
    // system-managed disabled mode 3, whose management semantics are different.
    internal bool LastAccessMatches => NtfsPerformanceVerification.LastAccessMode(LastAccess, LastAccessKind) == 1;
    internal bool ShortNamesMatch => ShortNamesKind == RegistryValueKind.DWord && ShortNames is int value && value == 1;
    internal bool IsApplied => LastAccessMatches && ShortNamesMatch;
    internal string ActualValue => "NtfsDisableLastAccessUpdate=" + NtfsPerformanceVerification.FormatLastAccess(LastAccess, LastAccessKind) +
        "; NtfsDisable8dot3NameCreation=" + NtfsPerformanceVerification.FormatDword(ShortNames, ShortNamesKind);
}

internal static class NtfsPerformanceVerification
{
    // Microsoft IIS Support documents 0x80000000..03 as the new last-access
    // encoding. Registry.GetValue returns DWORDs as signed Int32, so mode 1
    // may be read as -2147483647. Decode ONLY this setting's known encoding;
    // never mask arbitrary unknown bits or normalize exact restore snapshots.
    internal static int? LastAccessMode(object? value, RegistryValueKind? kind)
    {
        if (kind != RegistryValueKind.DWord || value is not int number) return null;
        uint raw = unchecked((uint)number);
        uint mode = raw & 0x7fffffffU;
        return mode <= 3 ? (int)mode : null;
    }

    internal static string FormatLastAccess(object? value, RegistryValueKind? kind)
    {
        int? mode = LastAccessMode(value, kind);
        return mode.HasValue
            ? mode.Value.ToString(CultureInfo.InvariantCulture) + " [DWORD=0x" + unchecked((uint)(int)value!).ToString("X8", CultureInfo.InvariantCulture) + "]"
            : FormatDword(value, kind);
    }

    internal static string FormatDword(object? value, RegistryValueKind? kind) => value is null
        ? "Windows default"
        : kind == RegistryValueKind.DWord && value is int number
            ? "0x" + unchecked((uint)number).ToString("X8", CultureInfo.InvariantCulture)
            : (System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? "") + " [" + kind + "]";

    internal static bool ApplySucceeded(NativeCommandResult lastAccess, NativeCommandResult shortNames, NtfsPerformanceState state) =>
        !lastAccess.TimedOut && lastAccess.ExitCode == 0 && !shortNames.TimedOut && shortNames.ExitCode == 0 && state.IsApplied;
}
