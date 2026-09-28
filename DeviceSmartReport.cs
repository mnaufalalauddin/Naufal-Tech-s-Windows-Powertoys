using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace Naufal_Windows_Tech_s_Powertoys;

// Protocol decoding is pure and independently testable. See THIRD-PARTY-NOTICES
// for the third-party attribution covering adapted transport/health algorithms.
internal sealed record DeviceSmartSnapshot(string Serial, string Size, string Bus,
    IReadOnlyList<SystemReportEntry> Rows);

internal static class DeviceSmartReport
{
    internal static IReadOnlyList<SystemReportEntry> DecodeNvme(byte[] log, string source)
    {
        if (log.Length != 512 || log.All(b => b == 0) || log.All(b => b == 255) || log[3] > 100 || log[4] > 100)
            throw new InvalidOperationException("Invalid or empty NVMe SMART/Health log.");
        int remaining = Math.Max(0, 100 - log[5]);
        string status = log[0] != 0 || (log[4] > 0 && log[3] < log[4]) ? "Bad"
            : remaining <= 10 || (log[4] is > 0 and < 100 && log[3] == log[4]) || Count(log, 160) > 0 ? "Caution" : "Good";
        List<SystemReportEntry> rows =
        [
            new("Disk health", $"{status} — {remaining}% estimated endurance remaining", false),
            new("Health source / assessment", source + "; NVMe SMART/Health log 02h. Critical warnings or spare below threshold = Bad; remaining endurance <= 10%, spare at threshold, or recorded media errors = Caution. Otherwise Good. Not a guarantee against failure.", false),
            new("Temperature", Temperature(U16(log, 1)), false),
            new("Wear consumed (100% = estimated wear limit)", log[5] + "%", false),
            new("Critical warning flags", "0x" + log[0].ToString("X2") + " — " + Warnings(log[0]), false),
            new("Available spare", log[3] + "%", false),
            new("Available spare threshold", log[4] == 0 ? "Not reported" : log[4] + "%", false)
        ];
        string[] labels = ["Data units read", "Data units written", "Host read commands", "Host write commands", "Controller busy time (minutes)", "Power cycle count", "Power-on hours", "Unsafe shutdowns", "Media / data integrity errors", "Error information log entries"];
        for (int i = 0; i < labels.Length; i++)
        {
            BigInteger value = Count(log, 32 + i * 16);
            rows.Add(new(labels[i], value.ToString(CultureInfo.InvariantCulture) + (i == 6 ? " hours" : ""), false));
        }
        rows.Add(new("Total host reads", FormatTerabytes(Count(log, 32) * 512000), false));
        rows.Add(new("Total host writes", FormatTerabytes(Count(log, 48) * 512000), false));
        rows.Add(new("Warning temperature time", U32(log, 192) + " minutes", false));
        rows.Add(new("Critical temperature time", U32(log, 196) + " minutes", false));
        for (int i = 0; i < 8; i++) rows.Add(new("Temperature sensor " + (i + 1), Temperature(U16(log, 200 + i * 2)), false));
        rows.Add(new("Thermal management transition count 1", U32(log, 216).ToString(CultureInfo.InvariantCulture), false));
        rows.Add(new("Thermal management transition count 2", U32(log, 220).ToString(CultureInfo.InvariantCulture), false));
        rows.Add(new("Thermal management total time 1", U32(log, 224) + " seconds", false));
        rows.Add(new("Thermal management total time 2", U32(log, 228) + " seconds", false));
        return rows;
    }

    internal static IReadOnlyList<SystemReportEntry> DecodeAta(byte[] data, byte[]? thresholds, bool? failing, bool isHdd, string source, string model = "", string firmware = "")
    {
        var attributes = LegacySmartReport.Parse(data, thresholds);
        if (attributes.Count == 0) throw new InvalidOperationException("ATA SMART data has no attributes.");
        bool exceeded = attributes.Any(a => (a.Flags & 1) != 0 && a.Threshold is > 0 && a.Current <= a.Threshold && a.Id != 0xC2);
        bool caution = isHdd && attributes.Any(a => a.Id is 0x05 or 0xC5 or 0xC6 &&
            ulong.TryParse(a.Raw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong raw) && raw is > 0 and < 0xFFFFFFFFFFFF);
        string health = failing == true || exceeded ? "Bad" : caution ? "Caution" : failing == false ? "Good" : "Unknown";
        var life = isHdd ? null : SsdEndurance.Evaluate(model, firmware, attributes);
        if (life is not null)
        {
            if (health != "Bad" && life.Remaining <= 10) health = "Caution";
            health += $" — {life.Remaining}% estimated endurance remaining";
        }
        List<SystemReportEntry> rows =
        [
            new("Disk health", health, false),
            new("Health source / assessment", source + "; ATA SMART RETURN STATUS and pre-failure thresholds. HDD reallocated/pending/uncorrectable sectors trigger Caution. SSD endurance is shown only for recognized model-scoped rules, separately from overall health. Unknown means insufficient health evidence.", false),
            new("ATA SMART RETURN STATUS", failing is null ? "Not reported" : failing.Value ? "Device reports impending failure" : "Device reports no impending failure", false)
        ];
        if (life is not null)
        {
            rows.Add(new("SSD endurance rule", life.Rule + ". Estimated write endurance, not time-to-failure or a health guarantee.", false));
            rows.Add(new("Wear consumed (100% = estimated wear limit)", (100 - life.Remaining) + "%", false));
        }
        foreach (var a in attributes)
            rows.Add(new($"0x{a.Id:X2} — {a.Name}", $"Current={a.Current}; Worst={a.Worst}; Threshold={a.Threshold?.ToString() ?? "Not reported"}; Raw={a.Raw}", false, Smart: a));
        return rows;
    }

    internal static DeviceSmartSnapshot? Match(Dictionary<string, string> disk, IReadOnlyList<DeviceSmartSnapshot> snapshots)
    {
        string serial = DiskHealthReportService.Value(disk, "SerialNumber");
        if (serial.Length == 0) return null;
        var matches = snapshots.Where(s => s.Serial.Trim().Equals(serial, StringComparison.OrdinalIgnoreCase)
            && s.Size == DiskHealthReportService.Value(disk, "Size") && s.Bus == DiskHealthReportService.Value(disk, "BusType")).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static string Warnings(byte flags)
    {
        if (flags == 0) return "None reported";
        string[] names = ["spare below threshold", "temperature outside threshold", "reliability degraded", "read-only media", "volatile-memory backup failure", "persistent-memory region warning", "reserved bit 6", "reserved bit 7"];
        return string.Join("; ", Enumerable.Range(0, 8).Where(i => (flags & (1 << i)) != 0).Select(i => names[i]));
    }
    // Decimal TB (1,000,000,000,000 bytes), not TiB. Keep BigInteger throughout so
    // 128-bit device counters cannot overflow or lose precision via double.
    internal static string FormatTerabytes(BigInteger bytes)
    {
        if (bytes.Sign < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        BigInteger hundredths = (bytes + 5_000_000_000) / 10_000_000_000;
        BigInteger whole = BigInteger.DivRem(hundredths, 100, out BigInteger fraction);
        return whole.ToString("N0", CultureInfo.InvariantCulture) + "." + fraction.ToString("D2", CultureInfo.InvariantCulture) + " TB";
    }
    internal static BigInteger Count(byte[] data, int offset) => new(data.AsSpan(offset, 16), isUnsigned: true, isBigEndian: false);
    private static string Temperature(ushort kelvin) => kelvin is >= 200 and <= 1000 ? (kelvin - 273.15).ToString("0.#", CultureInfo.InvariantCulture) + " °C" : "Not reported";
    private static ushort U16(byte[] data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));
    private static uint U32(byte[] data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));
}
