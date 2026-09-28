using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Naufal_Windows_Tech_s_Powertoys;

// Read-only standard NVMe / ATA / SAT transports. Includes adapted MIT-licensed
// code; full third-party copyright and license in THIRD-PARTY-NOTICES.txt.
// No SMART enable/disable, self-test, firmware or write-sector commands.
internal static partial class NativeDiskSmart
{
    internal static IReadOnlyList<DeviceSmartSnapshot> Read()
    {
        List<DeviceSmartSnapshot> snapshots = new();
        var disks = NativeHardwareData.Query(@"ROOT\Microsoft\Windows\Storage", "MSFT_Disk", "Number", "SerialNumber", "Size", "BusType");
        foreach (var disk in disks)
        {
            if (!int.TryParse(DiskHealthReportService.Value(disk, "Number"), out int number) || number is < 0 or > 4096) continue;
            string bus = DiskHealthReportService.Value(disk, "BusType");
            List<SystemReportEntry> rows = new();
            try
            {
                if (bus is not ("17" or "3" or "11" or "7" or "8")) throw new NotSupportedException("No verified SMART transport for this bus/controller.");
                string path = @"\\.\PhysicalDrive" + number;
                if (bus == "8") rows.AddRange(ReadRst(number, path, DiskHealthReportService.Value(disk, "SerialNumber")));
                if (bus is "17" or "7")
                {
                    using var queryHandle = Open(path, 0);
                    VerifyDeviceNumber(queryHandle, number);
                    try
                    {
                        rows.AddRange(DeviceSmartReport.DecodeNvme(ReadNvme(queryHandle, false), "PhysicalDrive" + number + " / NVMe storage protocol query"));
                        try
                        {
                            byte[] identify = ReadNvme(queryHandle, true);
                            AddIdentity(rows, "Serial number", Ascii(identify, 4, 20));
                            AddIdentity(rows, "Device name", Ascii(identify, 24, 40));
                            AddIdentity(rows, "Firmware", Ascii(identify, 64, 8));
                        }
                        catch (Exception ex) { rows.Add(new("NVMe identify", "Not reported: " + ex.Message, false)); }
                    }
                    catch when (bus == "7") { /* USB SAT bridge may expose ATA, not NVMe */ }
                }
                if (rows.Count == 0)
                {
                    // Windows pass-through IOCTLs require a read/write handle even
                    // for these strictly allowlisted DATA_IN/read-status commands.
                    using var handle = Open(path, 0xC0000000);
                    VerifyDeviceNumber(handle, number);
                    if (bus == "7")
                    {
                        string bridge = "";
                        try { bridge = DetectTransport(number); }
                        catch { /* PnP metadata failure must not suppress standard SAT. */ }
                        if (bridge is "ASMedia" or "Realtek")
                        {
                            try { rows.AddRange(ReadBridge(handle, bridge)); }
                            catch (Exception ex) { rows.Add(new("USB NVMe transport note", ex.Message, false)); }
                            if (rows.Any(r => r.Property == "Disk health"))
                            {
                                snapshots.Add(new(DiskHealthReportService.Value(disk, "SerialNumber"), DiskHealthReportService.Value(disk, "Size"), bus, rows));
                                continue;
                            }
                        }
                    }
                    bool sat = bus == "7";
                    byte[] identify = ReadAtaBlock(handle, 0xEC, 0, sat);
                    bool hdd = BinaryPrimitives.ReadUInt16LittleEndian(identify.AsSpan(434)) > 1;
                    byte[] data = ReadAtaBlock(handle, 0xB0, 0xD0, sat);
                    byte[]? thresholds = null;
                    string thresholdError = "";
                    try { thresholds = ReadAtaBlock(handle, 0xB0, 0xD1, sat); }
                    catch (Exception ex) { thresholdError = ex.Message; }
                    bool? failure = null;
                    if (sat)
                    {
                        try { failure = SatHealthStatus(handle); } catch { /* unsupported remains Unknown */ }
                    }
                    else
                    {
                        try
                        {
                            byte[] status = AtaRequest(handle, 0xB0, 0xDA, false);
                            failure = (status[43], status[44]) switch { (0x4F, 0xC2) => false, (0xF4, 0x2C) => true, _ => null };
                        }
                        catch { /* health remains unknown unless threshold evidence exists */ }
                    }
                    rows.AddRange(DeviceSmartReport.DecodeAta(data, thresholds, failure, hdd,
                        "PhysicalDrive" + number + (sat ? " / SAT ATA PASS-THROUGH(16)" : " / ATA_PASS_THROUGH_EX"), AtaText(identify, 54, 40), AtaText(identify, 46, 8)));
                    AddIdentity(rows, "Device name", AtaText(identify, 54, 40));
                    AddIdentity(rows, "Serial number", AtaText(identify, 20, 20));
                    AddIdentity(rows, "Firmware", AtaText(identify, 46, 8));
                    if (thresholdError.Length > 0) rows.Add(new("ATA threshold read", "Not reported: " + thresholdError, false));
                }
            }
            catch (Exception ex)
            {
                rows.Add(new("Disk health", "Unknown", false));
                rows.Add(new("Health source / assessment", "Device SMART unavailable / not verified: " + ex.Message, false));
            }
            snapshots.Add(new(DiskHealthReportService.Value(disk, "SerialNumber"), DiskHealthReportService.Value(disk, "Size"), bus, rows));
        }
        return snapshots;
    }

    private static SafeFileHandle Open(string path, uint access)
    {
        var handle = CreateFileW(path, access, 3, 0, 3, 0, 0);
        if (!handle.IsInvalid) return handle;
        int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error);
    }

    private static void VerifyDeviceNumber(SafeFileHandle handle, int number)
    {
        byte[] output = new byte[12];
        if (!DeviceIoControl(handle, 0x2D1080, output, 0, output, output.Length, out uint returned, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (returned < 12 || U32(output, 0) != 7 || U32(output, 4) != number) throw new InvalidOperationException("Disk handle identity changed; refusing to assign SMART data.");
    }

    private static byte[] ReadNvme(SafeFileHandle handle, bool identify)
    {
        Exception? last = null;
        foreach (uint property in new uint[] { 49, 50 })
        foreach (uint ns in identify ? new uint[] { 0 } : new uint[] { 0, uint.MaxValue })
        {
            byte[] buffer = NvmeRequest(property, ns, identify);
            try
            {
                Io(handle, 0x2D1400, buffer, out uint returned);
                return NvmePayload(buffer, returned, identify ? 4096 : 512);
            }
            catch (Exception ex) { last = ex; }
        }
        throw new InvalidOperationException("NVMe protocol query unsupported or failed: " + last?.Message);
    }

    internal static byte[] NvmeRequest(uint property, uint ns, bool identify)
    {
        if (property is not (49 or 50)) throw new ArgumentOutOfRangeException(nameof(property));
        byte[] buffer = new byte[48 + 4096];
        Put32(buffer, 0, property); // PropertyStandardQuery = 0
        Put32(buffer, 8, 3); // ProtocolTypeNvme
        Put32(buffer, 12, identify ? 1u : 2u);
        Put32(buffer, 16, identify ? 1u : 2u); // CNS controller / SMART log 02h
        Put32(buffer, 20, ns);
        Put32(buffer, 24, 40); Put32(buffer, 28, 4096);
        return buffer;
    }

    internal static byte[] NvmePayload(byte[] buffer, uint returned, int required)
    {
        if (returned > buffer.Length || returned < 48 || U32(buffer, 0) != 48 || U32(buffer, 4) != 48 || U32(buffer, 8) != 3
            || U32(buffer, 12) != (required == 4096 ? 1u : 2u))
            throw new InvalidOperationException("Invalid NVMe protocol descriptor.");
        uint offset = U32(buffer, 24), length = U32(buffer, 28);
        if (offset < 40 || length < required || (ulong)8 + offset + length > returned)
            throw new InvalidOperationException("Truncated/out-of-bounds NVMe protocol data.");
        return buffer.AsSpan(checked((int)(8 + offset)), required).ToArray();
    }

    private static byte[] ReadAtaBlock(SafeFileHandle handle, byte command, byte feature, bool sat) =>
        sat ? SatRequest(handle, command, feature) : AtaRequest(handle, command, feature, true).AsSpan(48, 512).ToArray();

    internal static byte[] AtaBuffer(byte command, byte feature, bool dataIn)
    {
        if (IntPtr.Size != 8) throw new PlatformNotSupportedException("ATA transport requires the x64 build.");
        if (!((command == 0xEC && feature == 0 && dataIn) || (command == 0xB0 &&
            ((feature is 0xD0 or 0xD1 && dataIn) || (feature == 0xDA && !dataIn)))))
            throw new ArgumentException("Only read-only IDENTIFY/SMART data, thresholds and status commands are allowed.");
        byte[] buffer = new byte[48 + (dataIn ? 512 : 0)];
        Put16(buffer, 0, 48); Put16(buffer, 2, dataIn ? (ushort)2 : (ushort)0);
        Put32(buffer, 8, dataIn ? 512u : 0); Put32(buffer, 12, 3);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(24), dataIn ? 48UL : 0);
        buffer[40] = feature; buffer[41] = 1; buffer[42] = 1;
        if (command == 0xB0) { buffer[43] = 0x4F; buffer[44] = 0xC2; }
        buffer[45] = 0xA0; buffer[46] = command;
        return buffer;
    }

    private static byte[] AtaRequest(SafeFileHandle handle, byte command, byte feature, bool dataIn)
    {
        byte[] buffer = AtaBuffer(command, feature, dataIn);
        Io(handle, 0x4D02C, buffer, out uint returned);
        if (returned != buffer.Length || (buffer[46] & 0xA1) != 0 || (dataIn &&
            (U32(buffer, 8) != 512 || BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(24)) != 48)))
            throw new InvalidOperationException("ATA read failed, truncated or busy.");
        return buffer;
    }

    private static byte[] SatRequest(SafeFileHandle handle, byte command, byte feature)
    {
        // Validate through the same immutable read-only allowlist.
        _ = AtaBuffer(command, feature, true);
        byte[] buffer = new byte[88 + 512];
        Put16(buffer, 0, 56); buffer[6] = 16; buffer[7] = 32; buffer[8] = 1;
        Put32(buffer, 12, 512); Put32(buffer, 16, 3);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(24), 88);
        Put32(buffer, 32, 56);
        buffer[36] = 0x85; buffer[37] = 8; buffer[38] = 0x0E;
        buffer[40] = feature; buffer[42] = 1; buffer[44] = 1;
        if (command == 0xB0) { buffer[46] = 0x4F; buffer[48] = 0xC2; }
        buffer[49] = 0xA0; buffer[50] = command;
        Io(handle, 0x4D004, buffer, out uint returned);
        if (returned != buffer.Length || buffer[2] != 0 || U32(buffer, 12) != 512
            || BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(24)) != 88)
            throw new InvalidOperationException("SAT read rejected or truncated by the controller.");
        return buffer.AsSpan(88, 512).ToArray();
    }

    private static bool? SatHealthStatus(SafeFileHandle handle)
    {
        // SAT-16 non-data SMART RETURN STATUS, CK_COND requests ATA register
        // read-back in sense data. This does not enable/reset SMART or run a test.
        byte[] buffer = new byte[88];
        Put16(buffer, 0, 56); buffer[6] = 16; buffer[7] = 32; buffer[8] = 2;
        Put32(buffer, 16, 3); Put32(buffer, 32, 56);
        buffer[36] = 0x85; buffer[37] = 6; buffer[38] = 0x20;
        buffer[40] = 0xDA; buffer[42] = 1; buffer[44] = 1;
        buffer[46] = 0x4F; buffer[48] = 0xC2; buffer[49] = 0xA0; buffer[50] = 0xB0;
        Io(handle, 0x4D004, buffer, out uint returned);
        if (returned < 64 || buffer[2] != 2) return null;
        int count = Math.Min(buffer[7], (int)returned - 56);
        return DecodeSatStatus(buffer.AsSpan(56, Math.Min(count, 32)).ToArray());
    }

    internal static bool? DecodeSatStatus(byte[] sense)
    {
        if (sense.Length < 8 || sense[0] != 0x72 || (sense[1] & 15) is not (0 or 1)
            || sense[2] != 0 || sense[3] != 0x1D || sense[7] + 8 > sense.Length) return null;
        int end = sense[7] + 8;
        for (int offset = 8; offset + 2 <= end;)
        {
            int length = sense[offset + 1] + 2;
            if (offset + length > end) return null;
            if (sense[offset] == 9 && length >= 14)
            {
                if ((sense[offset + 13] & 0xA1) != 0) return null;
                return (sense[offset + 9], sense[offset + 11]) switch
                { (0x4F, 0xC2) => false, (0xF4, 0x2C) => true, _ => null };
            }
            offset += length;
        }
        return null;
    }

    private static void Io(SafeFileHandle handle, uint code, byte[] buffer, out uint returned)
    {
        if (!DeviceIoControl(handle, code, buffer, buffer.Length, buffer, buffer.Length, out returned, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    private static string Ascii(byte[] bytes, int offset, int count) => Encoding.ASCII.GetString(bytes, offset, count).Trim(' ', '\0');
    private static void AddIdentity(List<SystemReportEntry> rows, string property, string value)
    {
        if (value.Length > 0 && value.All(c => c is >= ' ' and <= '~')) rows.Add(new(property, value, false));
    }
    private static string AtaText(byte[] bytes, int offset, int count)
    {
        byte[] text = bytes.AsSpan(offset, count).ToArray();
        for (int i = 0; i < text.Length; i += 2) (text[i], text[i + 1]) = (text[i + 1], text[i]);
        return Ascii(text, 0, count);
    }
    private static uint U32(byte[] buffer, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset));
    private static void Put32(byte[] buffer, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset), value);
    private static void Put16(byte[] buffer, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset), value);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, byte[] input, int inputSize, [In, Out] byte[] output, int outputSize, out uint returned, nint overlapped);
}
