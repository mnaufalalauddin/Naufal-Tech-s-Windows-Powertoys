using System;
using System.Collections.Generic;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed record DiskReportPage(string Title, IReadOnlyList<SystemReportEntry> Rows, bool IsDrive)
{
    internal string Value(string property) =>
        Rows.FirstOrDefault(row => row.Property == property).Value is { Length: > 0 } value ? value : "Not reported";
}

internal static class DiskReportPresentation
{
    internal static IReadOnlyList<DiskReportPage> Pages(IReadOnlyList<SystemReportEntry> rows)
    {
        List<DiskReportPage> devices = new();
        List<SystemReportEntry> overview = new();
        int index = 0;
        while (index < rows.Count)
        {
            if (rows[index].IsSection && (rows[index].Property.StartsWith("Storage device ", StringComparison.Ordinal) ||
                rows[index].Property.StartsWith("Legacy SMART device ", StringComparison.Ordinal)))
            {
                string id = rows[index++].Property;
                List<SystemReportEntry> device = new();
                while (index < rows.Count && !rows[index].IsSection) device.Add(rows[index++]);
                string name = device.FirstOrDefault(row => row.Property == "Device name").Value;
                devices.Add(new(id + (string.IsNullOrWhiteSpace(name) ? "" : " — " + name), device, true));
            }
            else overview.Add(rows[index++]);
        }
        devices.Add(new("All disks / volumes / provider notes", overview, false));
        return devices;
    }
}
