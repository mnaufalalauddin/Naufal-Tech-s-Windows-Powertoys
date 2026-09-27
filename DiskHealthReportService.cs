using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

// Read-only, local native WMI. No SMART reset, self-test, disk writes, WMIC or downloads.
internal sealed class DiskHealthReportService
{
    private const string Storage = @"ROOT\Microsoft\Windows\Storage";
    private readonly Func<string, string, string[], IReadOnlyList<Dictionary<string, string>>> _query;
    private readonly BoundedReadProbe<ProviderRows> _disks = new(), _counters = new(), _prediction = new();
    private readonly BoundedReadProbe<IReadOnlyList<DeviceSmartSnapshot>> _smart = new();
    private readonly bool _readHardware;
    internal sealed record ProviderRows(IReadOnlyList<Dictionary<string, string>> Rows, string Error = "");
    internal static readonly (string Property, string Label, string Unit)[] Metrics =
    [
        ("Temperature", "Temperature", " °C"),
        ("TemperatureMax", "Maximum normal operating temperature", " °C"),
        ("Wear", "Wear consumed (100% = estimated wear limit)", "%"),
        ("PowerOnHours", "Power-on hours", " hours"),
        ("ReadErrorsTotal", "Read errors (total)", ""),
        ("ReadErrorsCorrected", "Read errors (corrected)", ""),
        ("ReadErrorsUncorrected", "Read errors (uncorrected)", ""),
        ("WriteErrorsTotal", "Write errors (total)", ""),
        ("WriteErrorsCorrected", "Write errors (corrected)", ""),
        ("WriteErrorsUncorrected", "Write errors (uncorrected)", ""),
        ("StartStopCycleCount", "Start/stop cycles", ""),
        ("LoadUnloadCycleCount", "Load/unload cycles", "")
    ];

    internal DiskHealthReportService(
        Func<string, string, string[], IReadOnlyList<Dictionary<string, string>>>? query = null)
    {
        _readHardware = query is null;
        _query = query ?? ((ns, cls, properties) => cls == "MSFT_StorageReliabilityCounter"
            ? ReadNativeCounters(properties) : NativeHardwareData.Query(ns, cls, properties));
    }

    private static IReadOnlyList<Dictionary<string, string>> ReadNativeCounters(string[] properties)
    {
        List<Dictionary<string, string>> rows = new();
        NativeRscReader.Visit((services, disk) =>
        {
            string id = NativeRscReader.ReadValue(disk, "DeviceId");
            try
            {
                var counter = NativeRscReader.ReadDiskReliability(services, disk, properties);
                string returnedId = Value(counter, "DeviceId");
                if (returnedId.Length > 0 && !returnedId.Equals(id, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Reliability getter returned a different device ID; counters not assigned.");
                counter["DeviceId"] = id; // bound to the physical object passed into the getter
                rows.Add(counter);
            }
            catch (Exception ex)
            {
                rows.Add(new(StringComparer.OrdinalIgnoreCase) { ["DeviceId"] = id,
                    ["ReadError"] = ex.HResult is unchecked((int)0x80070005) or unchecked((int)0x80041003)
                        ? "Access denied. Administrator rights may be required."
                        : $"Not available / not verified (0x{ex.HResult:X8}): {ex.Message}" });
            }
        }, Storage, "SELECT * FROM MSFT_PhysicalDisk");
        return rows;
    }

    internal async Task<IReadOnlyList<SystemReportEntry>> ReadAsync(TimeSpan? timeout = null)
    {
        TimeSpan limit = timeout ?? TimeSpan.FromSeconds(10);
        Task<(IReadOnlyList<DeviceSmartSnapshot> Rows, string Error)> smart = ReadSmartAsync(limit);
        Task<ProviderRows> disks = ReadProvider(_disks, Storage, "MSFT_PhysicalDisk",
            ["DeviceId", "FriendlyName", "HealthStatus", "SerialNumber", "FirmwareVersion", "BusType", "Size"], limit);
        Task<ProviderRows> counters = ReadProvider(_counters, Storage, "MSFT_StorageReliabilityCounter",
            ["DeviceId", .. Metrics.Select(metric => metric.Property)], limit);
        Task<ProviderRows> prediction = ReadProvider(_prediction, @"ROOT\WMI", "MSStorageDriver_FailurePredictStatus",
            ["InstanceName", "Active", "PredictFailure", "Reason"], limit);
        await Task.WhenAll(disks, counters, prediction);
        var direct = await smart;
        var formatted = Format(await disks, await counters, await prediction, direct.Rows).ToList();
        if (direct.Error.Length > 0) formatted.Add(new("Device SMART reader", direct.Error, false));
        return formatted;
    }

    private async Task<(IReadOnlyList<DeviceSmartSnapshot>, string)> ReadSmartAsync(TimeSpan limit)
    {
        if (!_readHardware) return ([], ""); // injected tests never touch real disks
        try { return (await _smart.ReadAsync(NativeDiskSmart.Read, limit), ""); }
        catch (Exception ex) { return ([], "Unknown / not verified: " + ex.Message); }
    }

    private async Task<ProviderRows> ReadProvider(BoundedReadProbe<ProviderRows> probe,
        string ns, string cls, string[] properties, TimeSpan timeout)
    {
        try { return await probe.ReadAsync(() => new ProviderRows(_query(ns, cls, properties)), timeout); }
        catch (TimeoutException) { return new([], "Timed out; status is not verified. Retry later."); }
        catch (Exception exception)
        {
            string detail = exception is UnauthorizedAccessException ||
                exception.HResult == unchecked((int)0x80070005) ||
                exception.HResult == unchecked((int)0x80041003)
                ? "Access denied. Administrator rights may be required."
                : $"Provider unavailable or unsupported (0x{exception.HResult:X8}): {exception.Message}";
            return new([], detail);
        }
    }

    internal static string Value(Dictionary<string, string> row, string property) =>
        row.TryGetValue(property, out string? value) ? value.Trim() : "";

    internal static string Counter(string value, string unit) =>
        ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong number)
            ? number.ToString(CultureInfo.InvariantCulture) + unit : "Not reported";

    internal static IReadOnlyList<SystemReportEntry> Format(ProviderRows disks, ProviderRows counters, ProviderRows prediction,
        IReadOnlyList<DeviceSmartSnapshot>? smart = null)
    {
        List<SystemReportEntry> rows = new()
        {
            new("=== S.M.A.R.T. / STORAGE RELIABILITY ===", "", true),
            new("Read-only snapshot", "Direct device SMART plus Windows reliability counters. USB bridges, RAID and some controllers require transports not supported here. Missing data does not mean a healthy or failing drive.", false)
        };
        if (disks.Error.Length > 0) rows.Add(new("Disk identity provider", disks.Error, false));
        if (counters.Error.Length > 0) rows.Add(new("Reliability provider", counters.Error, false));
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (var disk in disks.Rows)
        {
            string id = Value(disk, "DeviceId");
            rows.Add(new("Storage device " + (id.Length > 0 ? id : "(ID not reported)"), Value(disk, "FriendlyName"), true));
            int sectionStart = rows.Count;
            rows.Add(new("Disk health", "Unknown", false));
            rows.Add(new("Health source / assessment", "No uniquely matched device SMART result. Windows status is not used as disk SMART health.", false));
            rows.Add(new("Device name", Value(disk, "FriendlyName"), false));
            rows.Add(new("Serial number", Reported(Value(disk, "SerialNumber")), false));
            rows.Add(new("Firmware", Reported(Value(disk, "FirmwareVersion")), false));
            rows.Add(new("Bus / interface", Value(disk, "BusType") switch
            {
                "3" => "ATA", "7" => "USB", "8" => "RAID", "10" => "SAS",
                "11" => "SATA", "15" => "Virtual", "16" => "Storage Spaces", "17" => "NVMe",
                _ => "Windows bus type: " + Reported(Value(disk, "BusType"))
            }, false));
            rows.Add(new("Capacity", ulong.TryParse(Value(disk, "Size"), out ulong size)
                ? (size / 1_000_000_000d).ToString("0.00", CultureInfo.InvariantCulture) + " GB (decimal)" : "Not reported", false));
            rows.Add(new("Windows health (not a full SMART test)", NativeHardwareData.DiskHealth(Value(disk, "HealthStatus")), false));
            var matches = id.Length == 0 ? [] : counters.Rows.Where(row =>
                Value(row, "DeviceId").Equals(id, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (id.Length > 0) seen.Add(id);
            if (matches.Length == 1) AddCounters(rows, matches[0]);
            else rows.Add(new("S.M.A.R.T. counters", matches.Length > 1
                ? "Ambiguous device ID; counters are not assigned to this disk."
                : "Not reported for this device/controller. No health conclusion can be drawn.", false));
            var deviceSmart = DeviceSmartReport.Match(disk, smart ?? []);
            if (deviceSmart is not null)
            {
                foreach (var value in deviceSmart.Rows)
                {
                    int existing = rows.FindIndex(sectionStart, row => row.Property == value.Property);
                    if (existing >= 0) rows[existing] = value;
                    else rows.Add(value);
                }
            }
        }
        // Preserve unmapped data by its own provider ID; never match by drive order/model.
        foreach (var counter in counters.Rows.Where(row => !seen.Contains(Value(row, "DeviceId"))))
        {
            rows.Add(new("Unmapped reliability device: " + Value(counter, "DeviceId"), "", true));
            AddCounters(rows, counter);
        }
        if (disks.Rows.Count == 0 && counters.Rows.Count == 0)
            rows.Add(new("Storage reliability data", "No device counters available from the Windows provider.", false));
        rows.Add(new("=== S.M.A.R.T. FAILURE PREDICTION ===", "", true));
        rows.Add(new("Source", "ROOT\\WMI / MSStorageDriver_FailurePredictStatus; listed by provider instance, not guessed drive order.", false));
        if (prediction.Error.Length > 0) rows.Add(new("Failure-prediction provider", prediction.Error, false));
        if (prediction.Rows.Count == 0) rows.Add(new("Failure prediction", "Not available; the driver may not expose legacy SMART prediction. This is not a PASS result.", false));
        foreach (var item in prediction.Rows)
        {
            rows.Add(new("Provider instance", Value(item, "InstanceName"), false));
            bool active = bool.TryParse(Value(item, "Active"), out bool enabled) && enabled;
            bool known = bool.TryParse(Value(item, "PredictFailure"), out bool predicted);
            rows.Add(new("Failure prediction", !active || !known ? "Unknown / inactive provider"
                : predicted ? "WARNING: device predicts failure. Back up important data and consult the drive vendor."
                : "No failure predicted by this provider (not a guarantee of drive health).", false));
            rows.Add(new("Vendor reason code", Value(item, "Reason").Length == 0 ? "Not reported" : Value(item, "Reason"), false));
        }
        return rows;
    }

    private static string Reported(string value) => value.Length == 0 ? "Not reported" : value;

    private static void AddCounters(List<SystemReportEntry> rows, Dictionary<string, string> counter)
    {
        rows.Add(new("Counter source", "MSFT_StorageReliabilityCounter / DeviceId=" + Value(counter, "DeviceId"), false));
        if (Value(counter, "ReadError").Length > 0)
            rows.Add(new("Reliability provider", Value(counter, "ReadError"), false));
        foreach (var metric in Metrics)
            rows.Add(new(metric.Label, Counter(Value(counter, metric.Property), metric.Unit), false));
    }
}
