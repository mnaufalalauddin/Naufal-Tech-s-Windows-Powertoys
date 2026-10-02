using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class BackgroundOwnerService
{
    private static readonly BoundedReadProbe<BackgroundOwnerSnapshot> Probe = new();
    internal static Task<BackgroundOwnerSnapshot> ReadAsync() => Probe.ReadAsync(Read, TimeSpan.FromSeconds(30));

    private static BackgroundOwnerSnapshot Read()
    {
        var before = ReadProcesses();
        IReadOnlyList<BackgroundService> services = [];
        string error = "";
        try
        {
            services = Query("Win32_Service", ["ProcessId", "Name", "State"])
                .Where(r => UInt(r["ProcessId"]).HasValue)
                .Select(r => new BackgroundService(UInt(r["ProcessId"])!.Value, r["Name"], r["State"])).ToArray();
        }
        catch (Exception exception) { error = exception.Message; }
        return new(DateTimeOffset.UtcNow, before, ReadProcesses(), services, error);
    }

    private static IReadOnlyList<BackgroundProcess> ReadProcesses() => Query("Win32_Process",
        ["ProcessId", "ParentProcessId", "Name", "ExecutablePath", "CreationDate", "WorkingSetSize", "PrivatePageCount", "ThreadCount", "HandleCount"])
        .Where(r => UInt(r["ProcessId"]).HasValue)
        .Select(r => new BackgroundProcess(UInt(r["ProcessId"])!.Value, UInt(r["ParentProcessId"]) ?? 0,
            r["Name"], r["ExecutablePath"], BackgroundOwnerReport.ParseCreationDate(r["CreationDate"]),
            ULong(r["WorkingSetSize"]), ULong(r["PrivatePageCount"]), UInt(r["ThreadCount"]), UInt(r["HandleCount"]))).ToArray();

    private static IReadOnlyList<Dictionary<string, string>> Query(string cls, string[] fields)
    {
        // Fixed internal class/field lists only; never interpolate user input.
        List<Dictionary<string, string>> rows = [];
        NativeRscReader.Visit((_, instance) =>
        {
            Dictionary<string, string> row = new(StringComparer.OrdinalIgnoreCase);
            foreach (string field in fields)
            {
                try { row[field] = NativeRscReader.ReadValue(instance, field); }
                catch { row[field] = ""; }
            }
            rows.Add(row);
        }, @"ROOT\CIMV2", "SELECT " + string.Join(",", fields) + " FROM " + cls);
        return rows;
    }
    private static uint? UInt(string value) => uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) ? result : null;
    private static ulong? ULong(string value) => ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) ? result : null;
}
