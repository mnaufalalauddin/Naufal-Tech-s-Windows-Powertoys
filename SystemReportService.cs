using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal readonly record struct SystemReportEntry(
        string Property,
        string Value,
        bool IsSection);

    internal sealed class SystemReportService
    {
        private const double BytesPerGigabyte = 1024d * 1024d * 1024d;
        private const uint IoctlStorageQueryProperty = 0x002D1400;
        private const uint IoctlDiskGetLengthInfo = 0x0007405C;
        private const uint GenericRead = 0x80000000;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint OpenExisting = 3;

        private static readonly Regex ProductKeyPattern = new(
            @"(?i)\b[A-Z0-9]{5}(?:-[A-Z0-9]{5}){4}\b",
            RegexOptions.CultureInvariant);
        private static readonly Regex PercentagePattern = new(
            @"(?i)([0-9]+(?:[\.,][0-9]+)?)\s*%",
            RegexOptions.CultureInvariant);

        public IReadOnlyList<SystemReportEntry> CollectDiskInformation()
        {
            List<SystemReportEntry> rows = new();
            IReadOnlyList<PhysicalDiskSummary> physicalDisks = ReadPhysicalDisks();

            AddSection(rows, "PHYSICAL DISK");
            if (physicalDisks.Count == 0)
            {
                AddRow(rows, "Physical Disk", "No physical disks detected.");
            }
            else
            {
                for (int index = 0; index < physicalDisks.Count; index++)
                {
                    PhysicalDiskSummary disk = physicalDisks[index];
                    string size = disk.SizeGigabytes > 0
                        ? $"{disk.SizeGigabytes:0.00} GB"
                        : "Unknown";
                    AddRow(
                        rows,
                        $"Disk {index + 1}",
                        $"{disk.Name} | {disk.MediaType} | {size} | {disk.Status}");
                }
            }

            DriveInfo[] logicalDrives = GetReadyLogicalDrives();
            AddSection(rows, "LOGICAL DRIVE");
            if (logicalDrives.Length == 0)
            {
                AddRow(rows, "Logical Drive", "No mounted logical drives detected.");
            }
            else
            {
                foreach (DriveInfo drive in logicalDrives)
                {
                    string label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                        ? "-"
                        : drive.VolumeLabel.Trim();
                    AddRow(
                        rows,
                        drive.Name.TrimEnd('\\'),
                        $"{label} | {drive.DriveFormat} | " +
                        $"{drive.AvailableFreeSpace / BytesPerGigabyte:0.00} GB Free / " +
                        $"{drive.TotalSize / BytesPerGigabyte:0.00} GB");
                }
            }

            AddSection(rows, "STORAGE SUMMARY");
            double totalPhysicalGigabytes = physicalDisks.Sum(
                static disk => Math.Max(0, disk.SizeGigabytes));
            AddRow(
                rows,
                "Total Physical Storage",
                totalPhysicalGigabytes > 0
                    ? $"{totalPhysicalGigabytes:0.00} GB"
                    : "Unknown");
            AddRow(rows, "Physical Disk Count", physicalDisks.Count.ToString(CultureInfo.InvariantCulture));
            AddRow(rows, "Logical Drive Count", logicalDrives.Length.ToString(CultureInfo.InvariantCulture));
            return rows;
        }

        public async Task<IReadOnlyList<SystemReportEntry>> CollectAsync()
        {
            List<SystemReportEntry> rows = new();

            string ntPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            string biosPath = @"HARDWARE\DESCRIPTION\System\BIOS";
            SmbiosSummary smbios = ReadSmbiosSummary();

            string manufacturer = FirstAvailable(
                ReadRegistryString(Registry.LocalMachine, biosPath, "SystemManufacturer"),
                smbios.SystemManufacturer);
            string model = FirstAvailable(
                ReadRegistryString(Registry.LocalMachine, biosPath, "SystemProductName"),
                smbios.SystemProductName);
            string productName = ReadRegistryString(
                Registry.LocalMachine, ntPath, "ProductName");
            string displayVersion = ReadRegistryString(
                Registry.LocalMachine, ntPath, "DisplayVersion");
            string buildNumber = ReadRegistryString(
                Registry.LocalMachine, ntPath, "CurrentBuildNumber");
            string processorName = ReadRegistryString(
                Registry.LocalMachine,
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                "ProcessorNameString");
            MemorySummary memory = ReadMemory();

            AddSection(rows, "SYSTEM");
            AddRow(rows, "Computer Name", Environment.MachineName);
            AddRow(rows, "Manufacturer", manufacturer);
            AddRow(rows, "Model", model);
            AddRow(rows, "Windows", NormalizeWindowsProductName(productName, buildNumber));
            AddRow(rows, "Version", BuildWindowsVersion(buildNumber, displayVersion));
            AddRow(rows, "Build", buildNumber);

            AddSection(rows, "PROCESSOR");
            AddRow(rows, "CPU", processorName);
            AddRow(rows, "RAM", $"{memory.TotalGigabytes:0.00} GB");

            AddSection(rows, "GRAPHICS");
            IReadOnlyList<string> graphicsAdapters = ReadGraphicsAdapters();
            if (graphicsAdapters.Count == 0)
            {
                AddRow(rows, "GPU", "Not Detected");
            }
            else
            {
                for (int index = 0; index < graphicsAdapters.Count; index++)
                {
                    AddRow(rows, $"GPU {index + 1}", graphicsAdapters[index]);
                }
            }

            AddSection(rows, "MOTHERBOARD");
            AddRow(rows, "Manufacturer", FirstAvailable(
                ReadRegistryString(Registry.LocalMachine, biosPath, "BaseBoardManufacturer"),
                smbios.BaseboardManufacturer));
            AddRow(rows, "Product", FirstAvailable(
                ReadRegistryString(Registry.LocalMachine, biosPath, "BaseBoardProduct"),
                smbios.BaseboardProduct));
            AddRow(rows, "Serial Number", FirstAvailable(
                ReadRegistryString(Registry.LocalMachine, biosPath, "BaseBoardSerialNumber"),
                smbios.BaseboardSerialNumber));

            AddSection(rows, "BIOS");
            AddRow(rows, "Version", FirstAvailable(
                ReadRegistryString(Registry.LocalMachine, biosPath, "BIOSVersion"),
                smbios.BiosVersion));
            AddRow(rows, "Serial Number", FirstAvailable(
                ReadRegistryString(Registry.LocalMachine, biosPath, "SystemSerialNumber"),
                smbios.SystemSerialNumber));

            AddSection(rows, "IDENTIFICATION");
            AddRow(rows, "UUID", smbios.Uuid);
            AddRow(rows, "OEM Product Key", ReadOemProductKey());

            AddSection(rows, "SECURITY");
            AddRow(rows, "Secure Boot", ReadSecureBootStatus());
            AddRow(rows, "TPM", ReadTpmStatus());
            AddRow(rows, "Smart App Control", ReadSmartAppControlStatus());

            DriveInfo[] fixedDrives = GetFixedReadyDrives();
            foreach (DriveInfo drive in fixedDrives)
            {
                string bitLockerStatus = await ReadBitLockerStatusAsync(drive.Name);
                AddRow(rows, $"BitLocker {drive.Name.TrimEnd('\\')}", bitLockerStatus);
            }

            AddSection(rows, "STORAGE");
            IReadOnlyList<PhysicalDiskSummary> physicalDisks = ReadPhysicalDisks();
            if (physicalDisks.Count == 0)
            {
                AddRow(rows, "Physical Disk", "Not Detected");
            }
            else
            {
                foreach (PhysicalDiskSummary disk in physicalDisks)
                {
                    AddRow(
                        rows,
                        disk.Name,
                        $"{disk.MediaType} | {disk.SizeGigabytes:0.00} GB | {disk.Status}");
                }
            }

            AddSection(rows, "LOGICAL DRIVE");
            if (fixedDrives.Length == 0)
            {
                AddRow(rows, "Logical Drive", "Not Detected");
            }
            else
            {
                foreach (DriveInfo drive in fixedDrives)
                {
                    AddRow(
                        rows,
                        drive.Name.TrimEnd('\\'),
                        $"{drive.AvailableFreeSpace / BytesPerGigabyte:0.00} GB Free / " +
                        $"{drive.TotalSize / BytesPerGigabyte:0.00} GB");
                }
            }

            return rows;
        }

        private static void AddSection(List<SystemReportEntry> rows, string title)
        {
            rows.Add(new SystemReportEntry($"=== {title} ===", string.Empty, true));
        }

        private static void AddRow(
            List<SystemReportEntry> rows,
            string property,
            string value)
        {
            rows.Add(new SystemReportEntry(
                property,
                string.IsNullOrWhiteSpace(value) ? "-" : value.Trim(),
                false));
        }

        private static string FirstAvailable(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static string BuildWindowsVersion(string buildNumber, string displayVersion)
        {
            if (int.TryParse(buildNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out int build))
            {
                return $"10.0.{build}";
            }

            return string.IsNullOrWhiteSpace(displayVersion)
                ? Environment.OSVersion.Version.ToString()
                : displayVersion;
        }

        private static string NormalizeWindowsProductName(
            string productName,
            string buildNumber)
        {
            if (int.TryParse(
                    buildNumber,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int build) &&
                build >= 22000 &&
                productName.Contains("Windows 10", StringComparison.OrdinalIgnoreCase))
            {
                return productName.Replace(
                    "Windows 10",
                    "Windows 11",
                    StringComparison.OrdinalIgnoreCase);
            }

            return productName;
        }

        private static MemorySummary ReadMemory()
        {
            MemoryStatusEx status = new()
            {
                Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
            };

            if (!GlobalMemoryStatusEx(ref status) || status.TotalPhysical == 0)
            {
                return new MemorySummary(0);
            }

            return new MemorySummary(status.TotalPhysical / BytesPerGigabyte);
        }

        private static string ReadRegistryString(
            RegistryKey hive,
            string path,
            string name)
        {
            try
            {
                using RegistryKey? key = hive.OpenSubKey(path, writable: false);
                object? value = key?.GetValue(
                    name,
                    null,
                    RegistryValueOptions.DoNotExpandEnvironmentNames);

                return value switch
                {
                    null => string.Empty,
                    string[] values => string.Join(" ", values),
                    byte[] bytes => DecodeRegistryString(bytes),
                    _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
                };
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int? ReadRegistryInt(string path, string name)
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(path, writable: false);
                object? value = key?.GetValue(name);
                return value is null
                    ? null
                    : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        private static string DecodeRegistryString(byte[] bytes)
        {
            string unicode = Encoding.Unicode.GetString(bytes).TrimEnd('\0', ' ');
            return unicode.Any(character => !char.IsControl(character))
                ? unicode
                : Encoding.ASCII.GetString(bytes).TrimEnd('\0', ' ');
        }

        private static IReadOnlyList<string> ReadGraphicsAdapters()
        {
            List<string> names = new();
            const string videoPath = @"SYSTEM\CurrentControlSet\Control\Video";

            try
            {
                using RegistryKey? videoRoot = Registry.LocalMachine.OpenSubKey(
                    videoPath,
                    writable: false);
                if (videoRoot is null)
                {
                    return names;
                }

                foreach (string adapterKeyName in videoRoot.GetSubKeyNames())
                {
                    using RegistryKey? adapterKey = videoRoot.OpenSubKey(adapterKeyName);
                    if (adapterKey is null)
                    {
                        continue;
                    }

                    foreach (string childName in adapterKey.GetSubKeyNames())
                    {
                        if (!childName.StartsWith("000", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        using RegistryKey? child = adapterKey.OpenSubKey(childName);
                        string name = Convert.ToString(
                            child?.GetValue("DriverDesc") ?? child?.GetValue("AdapterString"),
                            CultureInfo.InvariantCulture) ?? string.Empty;

                        name = name.Trim();
                        if (string.IsNullOrWhiteSpace(name) ||
                            names.Exists(existing => string.Equals(
                                existing,
                                name,
                                StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        names.Add(name);
                    }
                }
            }
            catch
            {
                return names;
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        private static string ReadSecureBootStatus()
        {
            int? value = ReadRegistryInt(
                @"SYSTEM\CurrentControlSet\Control\SecureBoot\State",
                "UEFISecureBootEnabled");

            return value switch
            {
                1 => "Enabled",
                0 => "Disabled",
                _ => "Unsupported"
            };
        }

        private static string ReadSmartAppControlStatus()
        {
            int? value = ReadRegistryInt(
                @"SYSTEM\CurrentControlSet\Control\CI\Policy",
                "VerifiedAndReputablePolicyState");

            return value switch
            {
                0 => "OFF",
                1 => "ON",
                2 => "Evaluation",
                _ => "Unknown"
            };
        }

        private static string ReadTpmStatus()
        {
            TbsDeviceInfo info = new() { StructVersion = 1 };
            uint result = TbsiGetDeviceInfo(
                (uint)Marshal.SizeOf<TbsDeviceInfo>(),
                ref info);

            if (result != 0)
            {
                return "Not Present";
            }

            string version = info.TpmVersion switch
            {
                1 => "1.2",
                2 => "2.0",
                _ => "Unknown"
            };
            string manufacturerVersion = ReadRegistryString(
                Registry.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\TPM\WMI",
                "ManufacturerVersion");

            return string.IsNullOrWhiteSpace(manufacturerVersion)
                ? $"Present (TPM {version})"
                : $"Present (Version {manufacturerVersion})";
        }

        private static SmbiosSummary ReadSmbiosSummary()
        {
            byte[] table = ReadFirmwareTable(FourCc("RSMB"), 0);
            if (table.Length < 32)
            {
                return new SmbiosSummary(
                    string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty,
                    string.Empty, "Not Available");
            }

            int tableLength = checked((int)Math.Min(
                BitConverter.ToUInt32(table, 4),
                (uint)Math.Max(0, table.Length - 8)));
            int position = 8;
            int end = position + tableLength;
            string biosVersion = string.Empty;
            string systemManufacturer = string.Empty;
            string systemProductName = string.Empty;
            string systemSerialNumber = string.Empty;
            string baseboardManufacturer = string.Empty;
            string baseboardProduct = string.Empty;
            string baseboardSerialNumber = string.Empty;
            string uuid = string.Empty;

            while (position + 4 <= end)
            {
                byte type = table[position];
                int structureLength = table[position + 1];
                if (structureLength < 4 || position + structureLength > end)
                {
                    break;
                }

                int stringsStart = position + structureLength;
                int stringsEnd = stringsStart;
                while (stringsEnd + 1 < end &&
                       !(table[stringsEnd] == 0 && table[stringsEnd + 1] == 0))
                {
                    stringsEnd++;
                }

                if (type == 0 && structureLength >= 9)
                {
                    biosVersion = GetSmbiosString(
                        table,
                        stringsStart,
                        stringsEnd,
                        table[position + 5]);
                }

                if (type == 1 && structureLength >= 24)
                {
                    systemManufacturer = GetSmbiosString(
                        table,
                        stringsStart,
                        stringsEnd,
                        table[position + 4]);
                    systemProductName = GetSmbiosString(
                        table,
                        stringsStart,
                        stringsEnd,
                        table[position + 5]);
                    systemSerialNumber = GetSmbiosString(
                        table,
                        stringsStart,
                        stringsEnd,
                        table[position + 7]);

                    byte[] uuidBytes = new byte[16];
                    Array.Copy(table, position + 8, uuidBytes, 0, uuidBytes.Length);

                    bool allZero = Array.TrueForAll(uuidBytes, value => value == 0);
                    bool allOnes = Array.TrueForAll(uuidBytes, value => value == 0xFF);
                    if (!allZero && !allOnes)
                    {
                        uuid = new Guid(uuidBytes).ToString("D").ToUpperInvariant();
                    }
                }

                if (type == 2 && structureLength >= 8)
                {
                    baseboardManufacturer = GetSmbiosString(
                        table,
                        stringsStart,
                        stringsEnd,
                        table[position + 4]);
                    baseboardProduct = GetSmbiosString(
                        table,
                        stringsStart,
                        stringsEnd,
                        table[position + 5]);
                    baseboardSerialNumber = GetSmbiosString(
                        table,
                        stringsStart,
                        stringsEnd,
                        table[position + 7]);
                }

                position = stringsEnd + 2;
                if (type == 127)
                {
                    break;
                }
            }

            return new SmbiosSummary(
                biosVersion,
                systemManufacturer,
                systemProductName,
                systemSerialNumber,
                baseboardManufacturer,
                baseboardProduct,
                baseboardSerialNumber,
                string.IsNullOrWhiteSpace(uuid) ? "Not Available" : uuid);
        }

        private static string GetSmbiosString(
            byte[] table,
            int start,
            int end,
            byte stringIndex)
        {
            if (stringIndex == 0 || start >= end)
            {
                return string.Empty;
            }

            int currentIndex = 1;
            int position = start;

            while (position < end)
            {
                int stringEnd = position;
                while (stringEnd < end && table[stringEnd] != 0)
                {
                    stringEnd++;
                }

                if (currentIndex == stringIndex)
                {
                    return Encoding.UTF8.GetString(
                        table,
                        position,
                        stringEnd - position).Trim();
                }

                currentIndex++;
                position = stringEnd + 1;
            }

            return string.Empty;
        }

        private static string ReadOemProductKey()
        {
            byte[] table = ReadFirmwareTable(FourCc("ACPI"), FourCc("MSDM"));
            if (table.Length == 0)
            {
                return "Not Available";
            }

            string text = Encoding.ASCII.GetString(table);
            Match match = ProductKeyPattern.Match(text);
            return match.Success ? match.Value.ToUpperInvariant() : "Not Available";
        }

        private static byte[] ReadFirmwareTable(uint provider, uint tableId)
        {
            try
            {
                uint size = GetSystemFirmwareTable(provider, tableId, null, 0);
                if (size == 0 || size > 16 * 1024 * 1024)
                {
                    return Array.Empty<byte>();
                }

                byte[] buffer = new byte[size];
                uint written = GetSystemFirmwareTable(provider, tableId, buffer, size);
                if (written == 0)
                {
                    return Array.Empty<byte>();
                }

                if (written != buffer.Length)
                {
                    Array.Resize(ref buffer, checked((int)written));
                }

                return buffer;
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        private static uint FourCc(string value)
        {
            if (value.Length != 4)
            {
                throw new ArgumentException("A firmware signature must contain four characters.", nameof(value));
            }

            return ((uint)value[0] << 24) |
                   ((uint)value[1] << 16) |
                   ((uint)value[2] << 8) |
                   value[3];
        }

        private static DriveInfo[] GetFixedReadyDrives()
        {
            List<DriveInfo> drives = new();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                    {
                        drives.Add(drive);
                    }
                }
                catch
                {
                    // Ignore a volume that disappears during enumeration.
                }
            }

            drives.Sort(static (left, right) => string.Compare(
                left.Name,
                right.Name,
                StringComparison.OrdinalIgnoreCase));
            return drives.ToArray();
        }

        private static DriveInfo[] GetReadyLogicalDrives()
        {
            List<DriveInfo> drives = new();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady)
                    {
                        drives.Add(drive);
                    }
                }
                catch
                {
                    // Ignore media that disappears while the read-only report is collected.
                }
            }

            drives.Sort(static (left, right) => string.Compare(
                left.Name,
                right.Name,
                StringComparison.OrdinalIgnoreCase));
            return drives.ToArray();
        }

        private static async Task<string> ReadBitLockerStatusAsync(string driveRoot)
        {
            try
            {
                var volumes = await Task.Run(NativeHardwareData.ReadBitLockerVolumes);
                string mount = driveRoot.TrimEnd('\\');
                foreach (var volume in volumes)
                    if (volume.MountPoint.Equals(mount, StringComparison.OrdinalIgnoreCase))
                        return volume.VolumeStatus;
                return "Unknown (volume not reported)";
            }
            catch (Exception exception) { return "Unavailable: " + exception.Message; }
        }

        private static IReadOnlyList<PhysicalDiskSummary> ReadPhysicalDisks()
        {
            List<PhysicalDiskSummary> disks = new();
            try
            {
                foreach (var row in NativeHardwareData.Query(
                    @"ROOT\Microsoft\Windows\Storage", "MSFT_PhysicalDisk",
                    "FriendlyName", "Size", "MediaType", "HealthStatus"))
                {
                    double.TryParse(row["Size"], NumberStyles.Float, CultureInfo.InvariantCulture, out double bytes);
                    disks.Add(new PhysicalDiskSummary(
                        string.IsNullOrWhiteSpace(row["FriendlyName"]) ? "Unknown disk" : row["FriendlyName"],
                        NativeHardwareData.DiskMedia(row["MediaType"]), bytes / BytesPerGigabyte,
                        NativeHardwareData.DiskHealth(row["HealthStatus"])));
                }
                if (disks.Count > 0) return disks;
            }
            catch { disks.Clear(); /* IOCTL fallback retains capacity, never claims health or media type. */ }
            foreach (string deviceName in EnumeratePhysicalDriveNames())
            {
                using SafeFileHandle handle = OpenPhysicalDrive(deviceName);

                if (handle.IsInvalid)
                {
                    continue;
                }

                byte[] query = new byte[12];
                byte[] descriptor = new byte[2048];
                bool descriptorRead = DeviceIoControl(
                    handle,
                    IoctlStorageQueryProperty,
                    query,
                    query.Length,
                    descriptor,
                    descriptor.Length,
                    out int descriptorBytes,
                    IntPtr.Zero);

                byte[] lengthBuffer = new byte[8];
                bool lengthRead = DeviceIoControl(
                    handle,
                    IoctlDiskGetLengthInfo,
                    null,
                    0,
                    lengthBuffer,
                    lengthBuffer.Length,
                    out int lengthBytes,
                    IntPtr.Zero);

                string vendor = string.Empty;
                string product = string.Empty;
                string mediaType = "Storage";

                if (descriptorRead && descriptorBytes >= 33)
                {
                    vendor = ReadDescriptorString(descriptor, descriptorBytes, 12);
                    product = ReadDescriptorString(descriptor, descriptorBytes, 16);
                    mediaType = GetBusTypeName(descriptor[28]);
                }

                string name = string.Join(" ", new[] { vendor, product })
                    .Replace("  ", " ", StringComparison.Ordinal)
                    .Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = deviceName;
                }

                double sizeGigabytes = lengthRead && lengthBytes >= 8
                    ? BitConverter.ToInt64(lengthBuffer, 0) / BytesPerGigabyte
                    : 0;

                disks.Add(new PhysicalDiskSummary(
                    name,
                    $"Unknown (bus: {mediaType})",
                    sizeGigabytes,
                    "Unknown (health provider unavailable)"));
            }

            return disks;
        }

        private static SafeFileHandle OpenPhysicalDrive(string deviceName)
        {
            string path = $@"\\.\{deviceName}";
            SafeFileHandle handle = CreateFileW(
                path,
                GenericRead,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                0,
                IntPtr.Zero);

            if (!handle.IsInvalid)
            {
                return handle;
            }

            handle.Dispose();
            return CreateFileW(
                path,
                0,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                0,
                IntPtr.Zero);
        }

        private static IReadOnlyList<string> EnumeratePhysicalDriveNames()
        {
            List<string> names = new();
            char[] buffer = new char[65536];
            uint length = QueryDosDeviceW(null, buffer, buffer.Length);
            if (length == 0)
            {
                return names;
            }

            string allNames = new(buffer, 0, checked((int)length));
            foreach (string name in allNames.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                if (name.StartsWith("PhysicalDrive", StringComparison.OrdinalIgnoreCase))
                {
                    names.Add(name);
                }
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        private static string ReadDescriptorString(
            byte[] descriptor,
            int descriptorBytes,
            int offsetField)
        {
            int valueOffset = BitConverter.ToInt32(descriptor, offsetField);
            if (valueOffset <= 0 || valueOffset >= descriptorBytes)
            {
                return string.Empty;
            }

            int end = valueOffset;
            while (end < descriptorBytes && descriptor[end] != 0)
            {
                end++;
            }

            return Encoding.ASCII.GetString(descriptor, valueOffset, end - valueOffset).Trim();
        }

        private static string GetBusTypeName(byte busType)
        {
            return busType switch
            {
                3 => "ATA",
                7 => "USB",
                8 => "RAID",
                10 => "SAS",
                11 => "SATA",
                12 => "SD",
                13 => "MMC",
                14 => "Virtual",
                15 => "Virtual",
                16 => "Storage Spaces",
                17 => "NVMe",
                18 => "SCM",
                19 => "UFS",
                _ => "Storage"
            };
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatusEx
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhysical;
            public ulong AvailablePhysical;
            public ulong TotalPageFile;
            public ulong AvailablePageFile;
            public ulong TotalVirtual;
            public ulong AvailableVirtual;
            public ulong AvailableExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TbsDeviceInfo
        {
            public uint StructVersion;
            public uint TpmVersion;
            public uint TpmInterfaceType;
            public uint TpmImpRevision;
        }

        private readonly record struct MemorySummary(double TotalGigabytes);

        private readonly record struct SmbiosSummary(
            string BiosVersion,
            string SystemManufacturer,
            string SystemProductName,
            string SystemSerialNumber,
            string BaseboardManufacturer,
            string BaseboardProduct,
            string BaseboardSerialNumber,
            string Uuid);

        private readonly record struct PhysicalDiskSummary(
            string Name,
            string MediaType,
            double SizeGigabytes,
            string Status);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx memoryStatus);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetSystemFirmwareTable(
            uint firmwareTableProviderSignature,
            uint firmwareTableId,
            byte[]? firmwareTableBuffer,
            uint bufferSize);

        [DllImport("tbs.dll", EntryPoint = "Tbsi_GetDeviceInfo")]
        private static extern uint TbsiGetDeviceInfo(
            uint size,
            ref TbsDeviceInfo info);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint QueryDosDeviceW(
            string? deviceName,
            [Out] char[] targetPath,
            int maximumLength);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(
            SafeFileHandle device,
            uint controlCode,
            byte[]? inputBuffer,
            int inputBufferSize,
            [Out] byte[] outputBuffer,
            int outputBufferSize,
            out int bytesReturned,
            IntPtr overlapped);
    }
}
