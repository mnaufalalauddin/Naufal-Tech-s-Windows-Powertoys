using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed class LegacySmartReport
{
    private readonly BoundedReadProbe<IReadOnlyList<SystemReportEntry>> _probe = new();
    internal async Task<IReadOnlyList<SystemReportEntry>> ReadAsync()
    {
        try { return await _probe.ReadAsync(Read, TimeSpan.FromSeconds(10)); }
        catch (Exception exception)
        {
            return [new("Legacy SMART attributes", "Unavailable / not verified: " + exception.Message, false)];
        }
    }

    private static IReadOnlyList<SystemReportEntry> Read()
    {
        var data = ReadBlobs("MSStorageDriver_FailurePredictData");
        List<SystemReportEntry> rows = new();
        if (data.Count == 0) return [new("Legacy SMART attributes", "No raw ATA attributes exposed by the driver.", false)];
        List<(string Instance, byte[] Data)> thresholds;
        string thresholdError = "";
        try { thresholds = ReadBlobs("MSStorageDriver_FailurePredictThresholds"); }
        catch (Exception exception) { thresholds = []; thresholdError = exception.Message; }
        foreach (var device in data)
        {
            rows.Add(new("Legacy SMART device " + device.Instance, "", true));
            rows.Add(new("Provider instance", device.Instance, false));
            rows.Add(new("Interpretation", "Common attribute names are hints; IDs and raw units are vendor-defined. No health percentage is inferred. Provider instances are not assigned to a physical drive by list order.", false));
            if (thresholdError.Length > 0) rows.Add(new("Threshold provider", "Unavailable: " + thresholdError, false));
            var matched = thresholds.Where(item => item.Instance.Equals(device.Instance, StringComparison.OrdinalIgnoreCase)).ToArray();
            byte[]? threshold = matched.Length == 1 ? matched[0].Data : null;
            try
            {
                var attributes = Parse(device.Data, threshold);
                if (attributes.Count == 0) rows.Add(new("Raw attributes", "No populated ATA attribute entries.", false));
                foreach (SmartAttribute attribute in attributes)
                    rows.Add(new($"0x{attribute.Id:X2} — {attribute.Name}",
                        $"Current={attribute.Current}; Worst={attribute.Worst}; Threshold={attribute.Threshold?.ToString() ?? "Not reported"}; Raw={attribute.Raw}",
                        false, Smart: attribute));
            }
            catch (Exception exception) { rows.Add(new("Raw attributes", "Not decoded: " + exception.Message, false)); }
        }
        return rows;
    }

    private static List<(string Instance, byte[] Data)> ReadBlobs(string cls)
    {
        List<(string Instance, byte[] Data)> rows = new();
        NativeRscReader.Visit((_, instance) => rows.Add((
            NativeRscReader.ReadValue(instance, "InstanceName"),
            NativeRscReader.ReadByteArray(instance, "VendorSpecific"))),
            @"ROOT\WMI", "SELECT * FROM " + cls);
        return rows;
    }

    internal static IReadOnlyList<SmartAttribute> Parse(byte[] data, byte[]? thresholds)
    {
        if (!ValidBlock(data)) throw new InvalidOperationException("Not a valid 512-byte ATA SMART block (revision/checksum).");
        Dictionary<byte, byte> limits = new();
        if (thresholds is not null && ValidBlock(thresholds))
        {
            for (int offset = 2; offset < 362; offset += 12)
                if (thresholds[offset] != 0 && !limits.TryAdd(thresholds[offset], thresholds[offset + 1]))
                    throw new InvalidOperationException("Duplicate SMART threshold IDs.");
        }
        List<SmartAttribute> rows = new();
        HashSet<byte> ids = new();
        for (int offset = 2; offset < 362; offset += 12)
        {
            byte id = data[offset];
            if (id == 0) continue;
            if (!ids.Add(id)) throw new InvalidOperationException("Duplicate SMART attribute IDs.");
            // Six-byte raw field is shown as hexadecimal; no vendor unit conversion.
            string raw = string.Concat(data.Skip(offset + 5).Take(6).Reverse().Select(value => value.ToString("X2")));
            rows.Add(new(id, data[offset + 3], data[offset + 4],
                limits.TryGetValue(id, out byte threshold) ? threshold : null, raw,
                (ushort)(data[offset + 1] | data[offset + 2] << 8)));
        }
        return rows;
    }

    private static bool ValidBlock(byte[] data) =>
        data.Length == 512 && (data[0] != 0 || data[1] != 0) && (data.Sum(value => (int)value) & 255) == 0;
}
