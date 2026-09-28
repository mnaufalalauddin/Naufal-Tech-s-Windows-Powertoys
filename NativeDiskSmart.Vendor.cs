using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Naufal_Windows_Tech_s_Powertoys;

// Read-only subset adapted from MIT-licensed third-party code. See notices.
// No controller mode changes, RAID configuration, SMART enable or device writes.
internal static partial class NativeDiskSmart
{
    internal static string ClassifyTransport(string instance, string service)
    {
        string id = instance.ToUpperInvariant();
        // Exact USB identities only: don't probe vendor commands against arbitrary USB storage.
        if (id.StartsWith(@"USB\VID_174C&PID_2362\") || id.StartsWith(@"USB\VID_174C&PID_2362&")) return "ASMedia";
        if (id.StartsWith(@"USB\VID_0BDA&PID_9210\") || id.StartsWith(@"USB\VID_0BDA&PID_9210&")) return "Realtek";
        if (id.StartsWith(@"PCI\VEN_8086&") && (service.Equals("iaStorAC", StringComparison.OrdinalIgnoreCase) || service.Equals("iaStorAVC", StringComparison.OrdinalIgnoreCase))) return "Intel RST";
        return "";
    }

    private static string DetectTransport(int number)
    {
        var disks = NativeHardwareData.Query(@"ROOT\CIMV2", "Win32_DiskDrive", "Index", "PNPDeviceID");
        var matches = disks.Where(d => DiskHealthReportService.Value(d, "Index") == number.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if (matches.Length != 1) return "";
        string id = DiskHealthReportService.Value(matches[0], "PNPDeviceID");
        if (CM_Locate_DevNodeW(out uint node, id, 0) != 0) return "";
        for (int depth = 0; depth < 12; depth++)
        {
            StringBuilder text = new(512);
            if (CM_Get_Device_IDW(node, text, text.Capacity, 0) != 0) break;
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\" + text);
            string result = ClassifyTransport(text.ToString(), key?.GetValue("Service") as string ?? "");
            if (result.Length > 0) return result;
            if (CM_Get_Parent(out uint parent, node, 0) != 0) break;
            node = parent;
        }
        return "";
    }

    internal static byte[] BridgePacket(string bridge, bool identify)
    {
        if (bridge is not ("ASMedia" or "Realtek")) throw new ArgumentException("Unrecognized read-only USB transport.");
        int length = identify ? 4096 : 512;
        byte[] b = new byte[88 + length];
        Put16(b, 0, 56); b[6] = 16; b[7] = 32; b[8] = 1;
        Put32(b, 12, (uint)length); Put32(b, 16, 2); Put32(b, 32, 56);
        BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(24), 88);
        if (bridge == "ASMedia")
        {
            b[36] = 0xE6; b[37] = identify ? (byte)6 : (byte)2;
            b[39] = identify ? (byte)1 : (byte)2;
            if (!identify) b[43] = 0x7F;
        }
        else
        {
            b[36] = 0xE4; b[37] = (byte)(length & 255); b[38] = (byte)(length >> 8);
            b[39] = identify ? (byte)6 : (byte)2; b[40] = identify ? (byte)1 : (byte)0;
        }
        return b;
    }

    internal static byte[] BridgePayload(byte[] b, uint returned, bool identify)
    {
        int size = identify ? 4096 : 512;
        if (b.Length != 88 + size || returned != b.Length || b[2] != 0 || b[8] != 1 || U32(b, 12) != size ||
            BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(24)) != 88)
            throw new InvalidOperationException("USB bridge read failed or response was truncated.");
        return b.AsSpan(88, size).ToArray();
    }

    private static IReadOnlyList<SystemReportEntry> ReadBridge(SafeFileHandle handle, string bridge)
    {
        byte[] Read(bool identify)
        {
            byte[] b = BridgePacket(bridge, identify); Io(handle, 0x4D004, b, out uint returned);
            return BridgePayload(b, returned, identify);
        }
        byte[] identity = Read(true);
        ValidateNvmeIdentity(identity);
        var rows = DeviceSmartReport.DecodeNvme(Read(false), bridge + " USB NVMe read-only pass-through (hardware-dependent)").ToList();
        AddIdentity(rows, "Device name", Ascii(identity, 24, 40));
        AddIdentity(rows, "Serial number", Ascii(identity, 4, 20));
        AddIdentity(rows, "Firmware", Ascii(identity, 64, 8));
        return rows;
    }

    // Intel RST NVMe miniport packet: SRB(28), payload(136), data(4096).
    internal static byte[] RstPacket(byte path, bool identify)
    {
        byte[] b = new byte[164 + 4096];
        Put32(b, 0, 28); Encoding.ASCII.GetBytes("IntelNvm").CopyTo(b, 4);
        Put32(b, 12, 3); Put32(b, 16, 0xF0002808); Put32(b, 24, (uint)b.Length - 28);
        b[28] = 1; b[29] = path; b[32] = identify ? (byte)6 : (byte)2;
        Put32(b, 36, identify ? 0 : uint.MaxValue);
        Put32(b, 72, identify ? 1u : 0x007F0002);
        Put32(b, 116, 164); Put32(b, 120, 4096);
        return b;
    }

    internal static byte[] RstPayload(byte[] b, uint returned, bool identify)
    {
        int required = identify ? 4096 : 512;
        if (b.Length != 4260 || returned > b.Length || returned < 164 + required || U32(b, 0) != 28 ||
            Ascii(b, 4, 8) != "IntelNvm" || U32(b, 20) != 0 || U32(b, 116) != 164 || U32(b, 120) < required ||
            (BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(110)) & 0xFFFE) != 0)
            throw new InvalidOperationException("Intel RST read failed or returned an invalid NVMe completion.");
        return b.AsSpan(164, required).ToArray();
    }

    internal static bool SameControllerDisk(byte[] identify, string serial)
    {
        ValidateNvmeIdentity(identify);
        return serial.Trim().Length > 0 && Ascii(identify, 4, 20).Equals(serial.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateNvmeIdentity(byte[] identify)
    {
        if (identify.Length != 4096 || !Ascii(identify, 4, 20).Any(char.IsLetterOrDigit) || !Ascii(identify, 24, 40).Any(char.IsLetterOrDigit) ||
            identify.AsSpan(4, 60).ToArray().Any(b => b is < 32 or > 126))
            throw new InvalidOperationException("NVMe controller identity was not verified.");
    }

    private static IReadOnlyList<SystemReportEntry> ReadRst(int number, string path, string serial)
    {
        if (DetectTransport(number) != "Intel RST") throw new NotSupportedException("RAID controller is not in the supported Intel RST driver allowlist. Array/member health not inferred.");
        using var disk = Open(path, 0xC0000000); VerifyDeviceNumber(disk, number);
        byte[] address = new byte[8]; Put32(address, 0, 8);
        Io(disk, 0x41018, address, out uint count); // IOCTL_SCSI_GET_ADDRESS
        if (count != 8 || U32(address, 0) != 8 || address[6] != 0 || address[7] != 0)
            throw new NotSupportedException("RST target/LUN mapping is not supported; no member health assigned.");
        using var controller = Open(@"\\.\Scsi" + address[4] + ":", 0xC0000000);
        byte[] Read(bool identify)
        {
            byte[] b = RstPacket(address[5], identify); Io(controller, 0x4D008, b, out uint returned);
            return RstPayload(b, returned, identify);
        }
        byte[] identity = Read(true);
        if (!SameControllerDisk(identity, serial)) throw new InvalidOperationException("RST member identity does not match this Windows disk. Array health cannot be inferred from one member.");
        var rows = DeviceSmartReport.DecodeNvme(Read(false), "Intel RST / verified NVMe member identity; not aggregate RAID array health").ToList();
        AddIdentity(rows, "Device name", Ascii(identity, 24, 40));
        AddIdentity(rows, "Firmware", Ascii(identity, 64, 8));
        return rows;
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern uint CM_Locate_DevNodeW(out uint node, string id, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern uint CM_Get_Device_IDW(uint node, StringBuilder id, int length, uint flags);
    [DllImport("cfgmgr32.dll")] private static extern uint CM_Get_Parent(out uint parent, uint node, uint flags);
}
