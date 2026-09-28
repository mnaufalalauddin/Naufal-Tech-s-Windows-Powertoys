using System.Buffers.Binary;
using System.Text;
using Naufal_Windows_Tech_s_Powertoys;

internal static class VendorSmartTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var (id, service, expected) in new[] {
            (@"USB\VID_174C&PID_2362\fixture", "", "ASMedia"),
            (@"USB\VID_0BDA&PID_9210&REV_0100\fixture", "", "Realtek"),
            (@"USB\VID_0BDA&PID_9220\fixture", "", ""), // dual-drive mode switching excluded
            (@"USB\VID_174C&PID_23620\fixture", "", ""),
            (@"USB\VID_152D&PID_0583\fixture", "", ""),
            (@"PCI\VEN_8086&DEV_2822", "iaStorAC", "Intel RST"),
            (@"PCI\VEN_8086&DEV_2822", "storahci", ""),
            (@"PCI\VEN_1022&DEV_2822", "iaStorAC", ""),
            (@"PCI\VEN_8086&DEV_2822", "iaStorVD", "") })
            check(NativeDiskSmart.ClassifyTransport(id, service) == expected, "vendor identity/driver gate: " + id);
        foreach (string bridge in new[] { "ASMedia", "Realtek" })
        foreach (bool identify in new[] { false, true })
        {
            byte[] b = NativeDiskSmart.BridgePacket(bridge, identify);
            check(b[8] == 1 && b[6] == 16 && b[36] == (bridge == "ASMedia" ? 0xE6 : 0xE4), "USB read-only opcode and direction");
            check(b[bridge == "ASMedia" ? 37 : 39] == (identify ? 6 : 2), "USB only identify or health-log commands");
            check(NativeDiskSmart.BridgePayload(b, (uint)b.Length, identify).Length == (identify ? 4096 : 512), "USB payload size");
            check(Throws(() => NativeDiskSmart.BridgePayload(b, 88, identify)), "USB truncated response rejected");
            b[2] = 2;
            check(Throws(() => NativeDiskSmart.BridgePayload(b, (uint)b.Length, identify)), "USB SCSI failure rejected");
            b[2] = 0; b[24] = 0;
            check(Throws(() => NativeDiskSmart.BridgePayload(b, (uint)b.Length, identify)), "USB changed offset rejected");
        }
        check(Throws(() => NativeDiskSmart.BridgePacket("arbitrary", false)), "unknown vendor opcode unavailable");
        foreach (bool identify in new[] { false, true })
        {
            byte[] b = NativeDiskSmart.RstPacket(9, identify);
            check(b.Length == 4260 && b[29] == 9 && b[32] == (identify ? 6 : 2), "RST layout path and command");
            check(BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(72)) == (identify ? 1u : 0x007F0002), "RST fixed read command dword");
            check(NativeDiskSmart.RstPayload(b, (uint)b.Length, identify).Length == (identify ? 4096 : 512), "RST payload extraction");
            b[20] = 1;
            check(Throws(() => NativeDiskSmart.RstPayload(b, (uint)b.Length, identify)), "RST miniport error not success");
            b[20] = 0; b[110] = 2;
            check(Throws(() => NativeDiskSmart.RstPayload(b, (uint)b.Length, identify)), "NVMe completion failure rejected");
            b[110] = 1;
            check(NativeDiskSmart.RstPayload(b, (uint)b.Length, identify).Length > 0, "NVMe phase bit not an error");
            check(Throws(() => NativeDiskSmart.RstPayload(b, 164, identify)), "RST short response rejected");
        }
        byte[] identity = new byte[4096]; Array.Fill(identity, (byte)' ', 4, 68);
        Encoding.ASCII.GetBytes("DISK-1").CopyTo(identity, 4); Encoding.ASCII.GetBytes("NVMe model").CopyTo(identity, 24);
        check(NativeDiskSmart.SameControllerDisk(identity, "DISK-1"), "RST exact member serial maps");
        check(!NativeDiskSmart.SameControllerDisk(identity, "RAID-volume"), "RST member cannot impersonate array");
        check(!NativeDiskSmart.SameControllerDisk(identity, ""), "RST missing identity refused");
        check(Throws(() => NativeDiskSmart.SameControllerDisk(new byte[4096], "DISK-1")), "empty identify rejected");
        foreach (var (model, firmware, id, current, raw, expected) in new (string, string, byte, byte, string, int?)[] {
            ("Samsung SSD 860 EVO", "", 0xB1, 81, "000000000999", 81),
            ("SAMSUNG MZ7LM960", "", 0xE9, 67, "000000000000", 67),
            ("INTEL SSDSC2", "", 0xE9, 95, "000000000001", 95),
            ("CT500MX500SSD1", "", 0xCA, 45, "000000000055", 45),
            ("MICRON MTFDDAK", "", 0xCA, 101, "000000000001", null),
            ("KINGSTON SA400S37", "SBFK62C3", 0xE7, 100, "000000000050", 80),
            ("KINGSTON SA400S37", "03070009", 0xE7, 60, "000000000050", 60),
            ("KINGSTON SKC600", "", 0xA9, 70, "000000000000", 70),
            ("KIOXIA-EXCERIA SATA SSD", "", 0xAD, 127, "000000000000", 27),
            ("KIOXIA-EXCERIA SATA SSD", "", 0xAD, 99, "000000000000", null),
            ("KINGSTON DataTraveler Max", "", 0xE7, 80, "000000000000", null),
            ("Unknown SSD", "", 0xE9, 99, "000000000000", null),
            ("Samsung SSD 860 EVO", "", 0xE9, 99, "000000000000", null),
            ("Samsung SSD 860 EVO", "", 0xB1, 0, "000000000000", 0) })
        {
            SmartAttribute attr = new(id, current, current, null, raw);
            var estimate = SsdEndurance.Evaluate(model, firmware, [attr]);
            check(estimate?.Remaining == expected, "lifespan fixture: " + model + "/" + firmware);
            if (estimate is not null)
                check(estimate.Rule.EndsWith("; model-specific endurance rule", StringComparison.Ordinal), "endurance report uses neutral explanatory wording");
            check(SsdEndurance.Evaluate(model, firmware, [attr, attr]) is null, "duplicate lifespan ID rejected");
        }
    }
    private static bool Throws(Action action)
    { try { action(); return false; } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return true; } }
}
