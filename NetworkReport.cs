using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class NetworkReport
{
    internal static IReadOnlyList<SystemReportEntry> VisibleRows(
        IReadOnlyList<SystemReportEntry> rows, bool includeAddresses) =>
        rows.Where(row => includeAddresses || !row.IsNetworkAddress).ToArray();

    internal static string FormatMac(byte[] bytes) => bytes.Length == 0
        ? "Not reported (this adapter may not have a MAC address)"
        : string.Join(":", bytes.Select(value => value.ToString("X2")));

    internal static string FormatAddresses(IEnumerable<IPAddress> addresses, AddressFamily family)
    {
        string[] values = addresses.Where(address => address.AddressFamily == family)
            .Select(address => address.ToString()).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(address => address, StringComparer.OrdinalIgnoreCase).ToArray();
        return values.Length == 0 ? "Not assigned" : string.Join("; ", values);
    }

    internal static IReadOnlyList<SystemReportEntry> Read()
    {
        List<SystemReportEntry> rows = new()
        {
            new("=== NETWORK ADDRESSES ===", "", true, true),
            new("Privacy", "Local adapter addresses only; no public-IP website is contacted. Copy/Save includes these rows only while the checkbox is selected. Wi-Fi may use a randomized MAC.", false, true)
        };
        try
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces()
                .OrderBy(adapter => adapter.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            if (adapters.Length == 0) rows.Add(new("Network adapters", "None reported", false, true));
            foreach (NetworkInterface adapter in adapters)
            {
                rows.Add(new("Adapter", adapter.Name + " | " + adapter.Description + " | " +
                    adapter.NetworkInterfaceType + " | " + adapter.OperationalStatus, false, true));
                try { rows.Add(new("MAC address", FormatMac(adapter.GetPhysicalAddress().GetAddressBytes()), false, true)); }
                catch (Exception exception) { rows.Add(new("MAC address", "Unavailable: " + exception.Message, false, true)); }
                try
                {
                    IPAddress[] addresses = adapter.GetIPProperties().UnicastAddresses.Select(item => item.Address).ToArray();
                    rows.Add(new("IPv4 address", FormatAddresses(addresses, AddressFamily.InterNetwork), false, true));
                    rows.Add(new("IPv6 address", FormatAddresses(addresses, AddressFamily.InterNetworkV6), false, true));
                }
                catch (Exception exception) { rows.Add(new("IP addresses", "Unavailable: " + exception.Message, false, true)); }
            }
        }
        catch (Exception exception) { rows.Add(new("Network inventory", "Unavailable: " + exception.Message, false, true)); }
        return rows;
    }
}
