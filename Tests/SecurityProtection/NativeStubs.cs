namespace Naufal_Windows_Tech_s_Powertoys;

// Native provider is compiled for API checking, but only its file store is exercised here.
// Any accidental host platform read fails immediately rather than accessing WMI or privilege state.
internal static class WindowsPrivilegeService
{
    internal static bool IsAdministrator() => throw new InvalidOperationException("Host privilege access forbidden in mock suite.");
}
internal static class NativeRscReader
{
    internal static void Visit(Action<string, nint> visitor, string ns, string query) => throw new InvalidOperationException("Host WMI access forbidden in mock suite.");
    internal static bool ReadBoolean(nint instance, string property) => throw new InvalidOperationException("Host WMI access forbidden in mock suite.");
    internal static string ReadValue(nint instance, string property) => throw new InvalidOperationException("Host WMI access forbidden in mock suite.");
}
