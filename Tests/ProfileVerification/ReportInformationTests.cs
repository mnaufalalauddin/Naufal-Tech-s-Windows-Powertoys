using Naufal_Windows_Tech_s_Powertoys;
using System.Net;
using System.Net.Sockets;

internal static class ReportInformationTests
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        check(NetworkReport.FormatMac([0, 17, 34, 171, 205, 255]) == "00:11:22:AB:CD:FF", "MAC preserves leading zeroes");
        check(NetworkReport.FormatMac([]).Contains("Not reported"), "missing MAC not invented");
        IPAddress[] ips = [IPAddress.Parse("192.0.2.1"), IPAddress.Parse("192.0.2.1"), IPAddress.Parse("fe80::1234%12")];
        check(NetworkReport.FormatAddresses(ips, AddressFamily.InterNetwork) == "192.0.2.1", "IPv4 duplicates removed");
        check(NetworkReport.FormatAddresses(ips, AddressFamily.InterNetworkV6) == "fe80::1234%12", "IPv6 scope retained");
        check(NetworkReport.FormatAddresses([], AddressFamily.InterNetwork) == "Not assigned", "disconnected adapter has no invented IP");
        SystemReportEntry[] source = [new("System", "Example", false), new("IPv4", "192.0.2.1", false, true)];
        check(NetworkReport.VisibleRows(source, false).Count == 1, "hidden addresses excluded from export model");
        check(NetworkReport.VisibleRows(source, true).Count == 2, "opt-in includes addresses");
        check(NetworkReport.VisibleRows(source, false).Count == 1 && source.Length == 2, "uncheck restores privacy without changing original rows");

        static Dictionary<string, string> Row(params string[] pairs) =>
            Enumerable.Range(0, pairs.Length / 2).ToDictionary(i => pairs[i * 2], i => pairs[i * 2 + 1]);
        DiskHealthReportService.ProviderRows disks = new([
            Row("DeviceId", "0", "FriendlyName", "Drive A", "HealthStatus", "0"),
            Row("DeviceId", "4", "FriendlyName", "Drive B", "HealthStatus", "1")]);
        DiskHealthReportService.ProviderRows counters = new([
            Row("DeviceId", "4", "Temperature", "39", "Wear", "100", "ReadErrorsUncorrected", "0"),
            Row("DeviceId", "0", "PowerOnHours", "123", "WriteErrorsTotal", "18446744073709551615")]);
        DiskHealthReportService.ProviderRows prediction = new([Row("InstanceName", "provider0", "Active", "True", "PredictFailure", "True")]);
        var rows = DiskHealthReportService.Format(disks, counters, prediction);
        var pages = DiskReportPresentation.Pages(rows);
        check(pages.Count == 3, "two device pages plus retained overview");
        check(pages[0].Value("Temperature") == "Not reported", "empty temperature never zero");
        check(pages[1].Value("Temperature") == "39 °C", "join counters by ID not provider order");
        check(pages[1].Value("Wear consumed (100% = estimated wear limit)") == "100%", "wear is consumed not remaining health");
        check(pages[0].Value("Write errors (total)") == "18446744073709551615", "64-bit counter precision preserved");
        check(pages[1].Value("Read errors (uncorrected)") == "0", "actual zero remains zero");
        check(rows.Any(row => row.Value.StartsWith("WARNING: device predicts")), "positive prediction is a warning");
        var noData = DiskHealthReportService.Format(new([]), new([]), new([]));
        check(noData.Any(row => row.Value.Contains("not a PASS")), "unsupported SMART not shown as healthy");
        var duplicates = DiskHealthReportService.Format(disks, new([counters.Rows[0], counters.Rows[0]]), new([]));
        check(duplicates.Any(row => row.Value.Contains("Ambiguous device")), "duplicate device identity cannot overwrite results");
        var perDiskFailure = DiskHealthReportService.Format(disks,
            new([Row("DeviceId", "0", "ReadError", "Access denied. Administrator rights may be required.")]), new([]));
        check(perDiskFailure.Any(row => row.Property == "Reliability provider" && row.Value.Contains("Access denied")), "per-disk getter error retained");
        check(DiskReportPresentation.Pages(perDiskFailure)[0].Value("Temperature") == "Not reported", "failed getter cannot invent a zero temperature");
        var inactive = DiskHealthReportService.Format(new([]), new([]), new([Row("Active", "False", "PredictFailure", "False")]));
        check(inactive.Any(row => row.Value == "Unknown / inactive provider"), "inactive status not no-failure");
        var denied = new DiskHealthReportService((_, _, _) => throw new UnauthorizedAccessException());
        var deniedRows = await denied.ReadAsync();
        check(deniedRows.Any(row => row.Value.Contains("Access denied")), "access denied distinguished from unsupported");
        var unsupported = new DiskHealthReportService((_, _, _) => throw new InvalidOperationException("Unsupported"));
        check((await unsupported.ReadAsync()).Any(row => row.Value.Contains("Provider unavailable")), "provider failure preserves report");
        using var gate = new ManualResetEventSlim(false);
        int calls = 0;
        var slow = new DiskHealthReportService((_, _, _) => { Interlocked.Increment(ref calls); gate.Wait(); return []; });
        try
        {
            var timed = await slow.ReadAsync(TimeSpan.FromMilliseconds(30));
            await slow.ReadAsync(TimeSpan.FromMilliseconds(30));
            check(timed.Any(row => row.Value.Contains("Timed out")), "slow SMART provider returns bounded report");
            check(calls <= 3, "retry reuses pending native reads");
        }
        finally { gate.Set(); await Task.Delay(50); }

        static void Checksum(byte[] block) { block[511] = 0; block[511] = (byte)((256 - (block.Sum(x => (int)x) & 255)) & 255); }
        byte[] data = new byte[512], limits = new byte[512];
        data[0] = limits[0] = 1;
        data[2] = 9; data[5] = 100; data[6] = 90; data[7] = 0x92; data[8] = 1;
        limits[2] = 9; limits[3] = 10;
        Checksum(data); Checksum(limits);
        var attribute = LegacySmartReport.Parse(data, limits).Single();
        check(attribute.Id == 9 && attribute.Current == 100 && attribute.Worst == 90 && attribute.Threshold == 10, "ATA attribute offsets parsed");
        check(attribute.Raw == "000000000192", "ATA raw field byte order retained");
        check(LegacySmartReport.Parse(data, null).Single().Threshold is null, "missing thresholds are unknown");
        limits[20] ^= 1;
        check(LegacySmartReport.Parse(data, limits).Single().Threshold is null, "corrupt threshold block does not invent a limit");
        byte[] duplicateData = (byte[])data.Clone();
        duplicateData[14] = 9; Checksum(duplicateData);
        bool duplicateRejected = false;
        try { LegacySmartReport.Parse(duplicateData, null); } catch (InvalidOperationException) { duplicateRejected = true; }
        check(duplicateRejected, "duplicate raw SMART IDs rejected");
        bool rejected = false;
        try { LegacySmartReport.Parse(new byte[20], null); } catch (InvalidOperationException) { rejected = true; }
        check(rejected, "truncated ATA buffer rejected");
        data[20] ^= 1; rejected = false;
        try { LegacySmartReport.Parse(data, null); } catch (InvalidOperationException) { rejected = true; }
        check(rejected, "bad checksum never displayed as valid SMART");
        check(DiskReportPresentation.Pages([new("Logical drive C:", "Existing backend information", false)]).Single().Rows.Count == 1,
            "no SMART leaves existing disk backend visible");
    }

    internal static async Task ReadProbeAsync(string? outputPath = null)
    {
        var network = NetworkReport.Read();
        var report = await new SystemReportService().CollectDiskInformationAsync();
        System.Text.StringBuilder summary = new();
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        summary.AppendLine("Administrator=" + new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator));
        summary.AppendLine($"Read-only report probe: network rows={network.Count}; hidden address rows={NetworkReport.VisibleRows(network, false).Count}; disk rows={report.Count}; pages={DiskReportPresentation.Pages(report).Count}; raw attributes={report.Count(row => row.Smart is not null)}");
        foreach (var row in report.Where(row => row.Property is "Disk health" or "Health source / assessment" or "Temperature" or "Power-on hours" or "Total host writes" or "Reliability provider" or "Failure-prediction provider" or "Legacy SMART attributes" or "Device SMART reader"))
            summary.AppendLine(row.Property + ": " + row.Value);
        summary.AppendLine("No IP/MAC addresses, serials or device identities printed. No settings changed.");
        Console.Write(summary);
        if (outputPath is not null) await File.WriteAllTextAsync(outputPath, summary.ToString());
    }
}
