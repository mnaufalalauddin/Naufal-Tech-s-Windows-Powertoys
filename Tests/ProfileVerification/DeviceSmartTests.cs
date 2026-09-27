using System.Buffers.Binary;
using System.Numerics;
using Naufal_Windows_Tech_s_Powertoys;

internal static class DeviceSmartTests
{
    internal static void Run(Action<bool, string> check)
    {
        VendorSmartTests.Run(check);
        byte[] log = new byte[512];
        log[3] = 100; log[4] = 10; log[5] = 7;
        BinaryPrimitives.WriteUInt16LittleEndian(log.AsSpan(1), 300);
        log[128] = 0xE9; log[129] = 3; // 1001 hours
        log[48 + 15] = 1; // a counter beyond UInt64
        var rows = DeviceSmartReport.DecodeNvme(log, "fixture");
        check(rows.First(r => r.Property == "Disk health").Value == "Good — 93% estimated endurance remaining", "NVMe endurance is device log, not Windows status");
        check(rows.First(r => r.Property == "Power-on hours").Value == "1001 hours", "NVMe power hours decoded");
        check(DeviceSmartReport.Count(log, 48) == (BigInteger.One << 120), "full 128-bit NVMe counters not truncated");
        check(rows.First(r => r.Property == "Total host writes").Value == DeviceSmartReport.FormatTerabytes((BigInteger.One << 120) * 512000), "host writes display TB without counter overflow");
        check(rows.First(r => r.Property == "Total host reads").Value == "0.00 TB", "zero host reads display TB");
        check(DeviceSmartReport.FormatTerabytes(40756260352000) == "40.76 TB", "host writes screenshot converted to decimal terabytes");
        check(DeviceSmartReport.FormatTerabytes(39179055104000) == "39.18 TB", "host reads screenshot rounded to two decimals");
        check(DeviceSmartReport.FormatTerabytes(1_000_000_000_000) == "1.00 TB", "TB is decimal, not TiB");
        check(DeviceSmartReport.FormatTerabytes(995_000_000_000) == "1.00 TB", "rounding carries across TB boundary");
        check(DeviceSmartReport.FormatTerabytes(994_999_999_999) == "0.99 TB", "values below rounding boundary preserved");
        check(DeviceSmartReport.FormatTerabytes((BigInteger.One << 120) * 512000) == "680,564,733,841,876,926,926,749,214,863.54 TB", "very large 128-bit totals keep exact rounded decimal digits");
        check(Throws(() => DeviceSmartReport.FormatTerabytes(-1)), "negative byte counters rejected");
        check(rows.First(r => r.Property == "Temperature").Value == "26.9 °C", "Kelvin converts accurately");
        log[0] = 4;
        check(Health(log).StartsWith("Bad"), "critical warning makes health bad");
        log[0] = 0; log[5] = 101;
        check(Health(log).StartsWith("Caution — 0%"), "wear above 100 does not underflow");
        log[5] = 0; log[3] = 9;
        check(Health(log).StartsWith("Bad"), "spare below threshold bad");
        log[3] = 10;
        check(Health(log).StartsWith("Caution"), "spare at threshold caution");
        log[3] = 100; log[160] = 1;
        check(Health(log).StartsWith("Caution"), "recorded media errors surfaced");
        check(Throws(() => DeviceSmartReport.DecodeNvme(new byte[512], "fixture")), "empty device response not healthy");
        check(Throws(() => DeviceSmartReport.DecodeNvme(new byte[12], "fixture")), "truncated NVMe response rejected");
        byte[] packet = NativeDiskSmart.NvmeRequest(49, 0, false);
        check(BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(12)) == 2 && BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(16)) == 2, "only SMART log queried");
        BinaryPrimitives.WriteUInt32LittleEndian(packet, 48); BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), 48);
        log.CopyTo(packet, 48);
        check(NativeDiskSmart.NvmePayload(packet, (uint)packet.Length, 512).SequenceEqual(log), "descriptor offsets respected");
        check(Throws(() => NativeDiskSmart.NvmePayload(packet, 60, 512)), "returned byte count bounds read");
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(24), uint.MaxValue);
        check(Throws(() => NativeDiskSmart.NvmePayload(packet, (uint)packet.Length, 512)), "malicious protocol offset rejected");
        foreach (byte command in new byte[] { 0x30, 0xF4, 0xEF })
            check(Throws(() => NativeDiskSmart.AtaBuffer(command, 0, true)), "ATA write/security/set-feature commands forbidden");
        check(Throws(() => NativeDiskSmart.AtaBuffer(0xB0, 0xD8, false)), "SMART enable mutation forbidden");
        byte[] ata = NativeDiskSmart.AtaBuffer(0xB0, 0xD0, true);
        check(ata.Length == 560 && ata[40] == 0xD0 && ata[43] == 0x4F && ata[44] == 0xC2 && ata[46] == 0xB0, "ATA read-only taskfile correctly laid out");
        byte[] sense = new byte[22];
        sense[0] = 0x72; sense[1] = 1; sense[3] = 0x1D; sense[7] = 14; sense[8] = 9; sense[9] = 12;
        sense[17] = 0x4F; sense[19] = 0xC2; sense[21] = 0x50;
        check(NativeDiskSmart.DecodeSatStatus(sense) == false, "SAT returned healthy signature decoded");
        sense[17] = 0xF4; sense[19] = 0x2C;
        check(NativeDiskSmart.DecodeSatStatus(sense) == true, "SAT impending failure decoded");
        sense[21] = 0x51;
        check(NativeDiskSmart.DecodeSatStatus(sense) is null, "ATA error bit cannot certify health");
        sense[9] = 99;
        check(NativeDiskSmart.DecodeSatStatus(sense) is null, "truncated SAT descriptor rejected");
        byte[] attributes = new byte[512], thresholds = new byte[512];
        attributes[0] = thresholds[0] = 1; attributes[2] = thresholds[2] = 5;
        attributes[3] = 1; attributes[5] = 99; attributes[6] = 98; thresholds[3] = 50;
        attributes[511] = (byte)((256 - (attributes.Sum(b => (int)b) & 255)) & 255);
        thresholds[511] = (byte)((256 - (thresholds.Sum(b => (int)b) & 255)) & 255);
        check(DeviceSmartReport.DecodeAta(attributes, thresholds, false, true, "fixture")[0].Value == "Good", "ATA status plus valid data used for health");
        check(DeviceSmartReport.DecodeAta(attributes, thresholds, null, false, "fixture")[0].Value == "Unknown", "no ATA status is not automatically healthy");
        check(DeviceSmartReport.DecodeAta(attributes, thresholds, true, false, "fixture")[0].Value == "Bad", "ATA failure preserved even without failing threshold");
        attributes[5] = 49; attributes[511] = 0;
        attributes[511] = (byte)((256 - (attributes.Sum(b => (int)b) & 255)) & 255);
        check(DeviceSmartReport.DecodeAta(attributes, thresholds, false, false, "fixture")[0].Value == "Bad", "pre-failure threshold crossing overrides pass status");
        Dictionary<string, string> disk = new() { ["SerialNumber"] = "unique", ["Size"] = "100", ["BusType"] = "17" };
        DeviceSmartSnapshot snapshot = new("unique", "100", "17", rows);
        check(DeviceSmartReport.Match(disk, [snapshot]) == snapshot, "match by exact serial size bus");
        disk["DeviceId"] = "7"; disk["HealthStatus"] = "0";
        var merged = DiskHealthReportService.Format(new([disk]),
            new([new() { ["DeviceId"] = "7", ["Wear"] = "0" }]), new([]), [snapshot]);
        check(merged.Single(r => r.Property == "Wear consumed (100% = estimated wear limit)").Value == "7%", "device log overrides generic Windows wear");
        check(merged.Single(r => r.Property == "Disk health").Value.StartsWith("Good — 93%"), "direct device health assigned only once");
        var unavailable = snapshot with { Rows = [new("Disk health", "Unknown", false)] };
        check(DiskHealthReportService.Format(new([disk]), new([]), new([]), [unavailable])
            .Single(r => r.Property == "Disk health").Value == "Unknown", "Windows Healthy cannot override unknown device health");
        check(DeviceSmartReport.Match(disk, [snapshot, snapshot]) is null, "duplicate serial identity not assigned");
        check(DeviceSmartReport.Match(disk, [snapshot with { Size = "200" }]) is null, "size mismatch not assigned");
        disk["SerialNumber"] = "";
        check(DeviceSmartReport.Match(disk, [snapshot with { Serial = "" }]) is null, "blank serial not guessed by enumeration order");
        var missing = DiskHealthReportService.Format(new([disk]), new([]), new([]));
        check(missing.Any(r => r.Property == "Disk health" && r.Value == "Unknown"), "no SMART stays unknown regardless Windows health");
        static string Health(byte[] log) => DeviceSmartReport.DecodeNvme(log, "fixture").First(r => r.Property == "Disk health").Value;
        static bool Throws(Action action) { try { action(); return false; } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return true; } }
    }
}
