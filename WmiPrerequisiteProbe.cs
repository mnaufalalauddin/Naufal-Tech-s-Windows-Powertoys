using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal enum WmiPrerequisiteState { Verified, TimedOut, Failed }

internal readonly record struct WmiPrerequisiteResult(WmiPrerequisiteState State, string Detail)
{
    internal bool Verified => State == WmiPrerequisiteState.Verified;
    internal string Status => State switch
    {
        WmiPrerequisiteState.Verified => "PASS",
        WmiPrerequisiteState.TimedOut => "WARNING",
        _ => "FAILED"
    };
}

// Read-only checks shared by Windows 10 and 11. No WMIC, PowerShell host,
// package installation, service configuration, or WMI repository repair.
internal sealed class WmiPrerequisiteProbe
{
    internal const string SystemStage = "Verify WMI system access";
    internal const string MemoryStage = "Verify WMI memory access";
    private readonly Func<string, string[], IReadOnlyList<Dictionary<string, string>>> _query;
    private readonly BoundedReadProbe<WmiPrerequisiteResult> _systemProbe = new();
    private readonly BoundedReadProbe<WmiPrerequisiteResult> _memoryProbe = new();

    internal WmiPrerequisiteProbe(
        Func<string, string[], IReadOnlyList<Dictionary<string, string>>>? query = null)
    {
        _query = query ?? QueryStrict;
    }

    internal Task<WmiPrerequisiteResult> VerifySystemAsync(TimeSpan? timeout = null) =>
        ReadAsync(_systemProbe, VerifySystem, timeout ?? TimeSpan.FromSeconds(30));

    internal Task<WmiPrerequisiteResult> VerifyMemoryAsync(TimeSpan? timeout = null) =>
        ReadAsync(_memoryProbe, VerifyMemory, timeout ?? TimeSpan.FromSeconds(30));

    private static async Task<WmiPrerequisiteResult> ReadAsync(
        BoundedReadProbe<WmiPrerequisiteResult> probe,
        Func<WmiPrerequisiteResult> read, TimeSpan timeout)
    {
        try { return await probe.ReadAsync(read, timeout).ConfigureAwait(false); }
        catch (TimeoutException exception)
        {
            // Timeout is unverified, never success or "not available on this PC".
            // A still-pending read is reused on retry, not duplicated or aborted.
            return new(WmiPrerequisiteState.TimedOut,
                "WMI verification timed out; readiness is not confirmed. " + exception.Message);
        }
        catch (Exception exception)
        {
            return new(WmiPrerequisiteState.Failed,
                $"WMI verification failed. HRESULT 0x{exception.HResult:X8}: {exception.Message}");
        }
    }

    private WmiPrerequisiteResult VerifySystem()
    {
        var rows = _query("Win32_OperatingSystem", ["Caption", "Version", "BuildNumber"]);
        if (rows.Count != 1 || !TryValue(rows[0], "Caption", out string caption) ||
            !TryValue(rows[0], "Version", out string version) || !Version.TryParse(version, out _) ||
            !TryValue(rows[0], "BuildNumber", out string build) ||
            !uint.TryParse(build, NumberStyles.None, CultureInfo.InvariantCulture, out uint number) || number == 0)
            return new(WmiPrerequisiteState.Failed,
                "Win32_OperatingSystem did not return a valid Caption, Version and BuildNumber.");

        // Do not branch on the localized Caption or assume 10.0 means Windows 10:
        // both Windows 10 and Windows 11 report NT version 10.0.
        return new(WmiPrerequisiteState.Verified,
            $"WMI system query verified: {caption}; Version={version}; Build={build}.");
    }

    private WmiPrerequisiteResult VerifyMemory()
    {
        // ComputerSystem works on physical and virtual machines without requiring
        // SMBIOS DIMM inventory or a configured page file.
        var rows = _query("Win32_ComputerSystem", ["TotalPhysicalMemory"]);
        if (rows.Count != 1 || !TryValue(rows[0], "TotalPhysicalMemory", out string value) ||
            !ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong bytes) || bytes == 0)
            return new(WmiPrerequisiteState.Failed,
                "Win32_ComputerSystem did not return a valid TotalPhysicalMemory value.");
        return new(WmiPrerequisiteState.Verified,
            $"WMI memory query verified: TotalPhysicalMemory={bytes.ToString(CultureInfo.InvariantCulture)} bytes.");
    }

    private static bool TryValue(Dictionary<string, string> row, string key, out string value)
    {
        value = row.TryGetValue(key, out string? found) ? found : string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static IReadOnlyList<Dictionary<string, string>> QueryStrict(string className, string[] properties)
    {
        List<Dictionary<string, string>> rows = new();
        NativeRscReader.Visit((_, instance) =>
        {
            Dictionary<string, string> row = new(StringComparer.Ordinal);
            foreach (string property in properties)
                row.Add(property, NativeRscReader.ReadValue(instance, property));
            rows.Add(row);
        }, @"ROOT\CIMV2", "SELECT " + string.Join(", ", properties) + " FROM " + className);
        return rows;
    }
}
