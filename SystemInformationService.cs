using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal sealed class SystemInformationService
    {
        private const double BytesPerGigabyte = 1024d * 1024d * 1024d;
        private readonly GamingStatusService _gamingStatusService = new();

        public string BuildDiskReport()
        {
            StringBuilder report = new();
            report.AppendLine("DISK AND VOLUME INFORMATION");
            report.AppendLine("============================================");
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            DriveInfo[] drives = DriveInfo.GetDrives();
            Array.Sort(drives, static (left, right) =>
                string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));

            int readyDriveCount = 0;
            foreach (DriveInfo drive in drives)
            {
                report.AppendLine($"Drive: {drive.Name}");
                report.AppendLine($"Type: {drive.DriveType}");

                try
                {
                    if (!drive.IsReady)
                    {
                        report.AppendLine("Status: Not ready");
                        report.AppendLine("--------------------------------------------");
                        continue;
                    }

                    readyDriveCount++;
                    long totalBytes = drive.TotalSize;
                    long freeBytes = drive.AvailableFreeSpace;
                    long usedBytes = Math.Max(0, totalBytes - freeBytes);
                    double usedPercent = totalBytes > 0
                        ? 100d * usedBytes / totalBytes
                        : 0d;

                    report.AppendLine($"Label: {ValueOrFallback(drive.VolumeLabel, "(none)")}");
                    report.AppendLine($"File system: {ValueOrFallback(drive.DriveFormat, "Unknown")}");
                    report.AppendLine($"Total: {FormatBytes(totalBytes)}");
                    report.AppendLine($"Used: {FormatBytes(usedBytes)} ({usedPercent:0.0}%)");
                    report.AppendLine($"Free: {FormatBytes(freeBytes)}");
                    report.AppendLine("Status: Ready");
                }
                catch (Exception exception)
                {
                    report.AppendLine($"Status: Could not read this volume ({exception.Message})");
                }

                report.AppendLine("--------------------------------------------");
            }

            report.AppendLine();
            report.AppendLine($"Ready volumes: {readyDriveCount}");
            report.AppendLine("This report is read-only. No disk operation was performed.");
            return report.ToString().TrimEnd();
        }

        public async Task<string> BuildSystemReportAsync()
        {
            GamingStatusSnapshot gaming =
                await _gamingStatusService.ReadSnapshotAsync();
            MemorySummary memory = ReadMemory();

            string productName = ReadRegistryValue(
                Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "ProductName");
            string displayVersion = ReadRegistryValue(
                Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "DisplayVersion");
            string buildNumber = ReadRegistryValue(
                Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "CurrentBuildNumber");
            string updateBuildRevision = ReadRegistryValue(
                Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "UBR");
            string cpuName = ReadRegistryValue(
                Registry.LocalMachine,
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                "ProcessorNameString");
            string manufacturer = ReadRegistryValue(
                Registry.LocalMachine,
                @"HARDWARE\DESCRIPTION\System\BIOS",
                "SystemManufacturer");
            string model = ReadRegistryValue(
                Registry.LocalMachine,
                @"HARDWARE\DESCRIPTION\System\BIOS",
                "SystemProductName");
            string biosVendor = ReadRegistryValue(
                Registry.LocalMachine,
                @"HARDWARE\DESCRIPTION\System\BIOS",
                "BIOSVendor");
            string biosVersion = ReadRegistryValue(
                Registry.LocalMachine,
                @"HARDWARE\DESCRIPTION\System\BIOS",
                "BIOSVersion");

            StringBuilder report = new();
            report.AppendLine("SYSTEM REPORT");
            report.AppendLine("============================================");
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
            report.AppendLine($"Administrator: {(PerformanceProfileService.IsAdministrator() ? "Yes" : "No")}");
            report.AppendLine();

            report.AppendLine("WINDOWS");
            report.AppendLine("--------------------------------------------");
            report.AppendLine($"Edition: {ValueOrUnavailable(productName)}");
            report.AppendLine($"Version: {ValueOrUnavailable(displayVersion)}");
            report.AppendLine($"Build: {FormatBuild(buildNumber, updateBuildRevision)}");
            report.AppendLine($"OS architecture: {RuntimeInformation.OSArchitecture}");
            report.AppendLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
            report.AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
            report.AppendLine($"Uptime: {FormatUptime(TimeSpan.FromMilliseconds(Environment.TickCount64))}");
            report.AppendLine();

            report.AppendLine("HARDWARE");
            report.AppendLine("--------------------------------------------");
            report.AppendLine($"Manufacturer: {ValueOrUnavailable(manufacturer)}");
            report.AppendLine($"Model: {ValueOrUnavailable(model)}");
            report.AppendLine($"Processor: {ValueOrUnavailable(cpuName)}");
            report.AppendLine($"Logical processors: {Environment.ProcessorCount}");
            report.AppendLine($"Physical memory: {memory.TotalGigabytes:0.00} GB");
            report.AppendLine($"Memory in use: {memory.UsedGigabytes:0.00} GB ({memory.UsedPercent:0.0}%)");
            report.AppendLine($"BIOS vendor: {ValueOrUnavailable(biosVendor)}");
            report.AppendLine($"BIOS version: {ValueOrUnavailable(biosVersion)}");
            report.AppendLine();

            report.AppendLine("GAMING CONFIGURATION");
            report.AppendLine("--------------------------------------------");
            report.AppendLine($"Power plan: {gaming.PowerPlanName}");
            report.AppendLine($"Power plan GUID: {gaming.PowerPlanGuid}");
            report.AppendLine($"MMCSS profile: {gaming.Mmcss.Profile}");
            report.AppendLine($"Game Mode: {gaming.GameMode}");
            report.AppendLine($"HAGS: {gaming.Hags}");
            report.AppendLine();

            AppendNetworkSummary(report);

            report.AppendLine();
            report.AppendLine("This report intentionally excludes product keys, serial numbers, MAC addresses and IP addresses.");
            report.AppendLine("No Windows setting was changed while generating this report.");
            return report.ToString().TrimEnd();
        }

        private static void AppendNetworkSummary(StringBuilder report)
        {
            report.AppendLine("ACTIVE NETWORK ADAPTERS");
            report.AppendLine("--------------------------------------------");

            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            Array.Sort(interfaces, static (left, right) =>
                string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));

            int activeCount = 0;
            foreach (NetworkInterface networkInterface in interfaces)
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                activeCount++;
                report.AppendLine($"Name: {networkInterface.Name}");
                report.AppendLine($"Type: {networkInterface.NetworkInterfaceType}");
                report.AppendLine($"Link speed: {FormatBitRate(networkInterface.Speed)}");
                report.AppendLine("--------------------------------------------");
            }

            if (activeCount == 0)
            {
                report.AppendLine("No active non-loopback adapter was detected.");
            }
        }

        private static MemorySummary ReadMemory()
        {
            MemoryStatusEx status = new()
            {
                Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
            };

            if (!GlobalMemoryStatusEx(ref status) || status.TotalPhysical == 0)
            {
                return new MemorySummary(0, 0, 0);
            }

            ulong usedBytes = status.TotalPhysical - status.AvailablePhysical;
            return new MemorySummary(
                status.TotalPhysical / BytesPerGigabyte,
                usedBytes / BytesPerGigabyte,
                100d * usedBytes / status.TotalPhysical);
        }

        private static string ReadRegistryValue(
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
                    string[] values => string.Join(", ", values),
                    _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
                };
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string FormatBuild(string buildNumber, string revision)
        {
            if (string.IsNullOrWhiteSpace(buildNumber))
            {
                return "Unavailable";
            }

            return string.IsNullOrWhiteSpace(revision)
                ? buildNumber
                : $"{buildNumber}.{revision}";
        }

        private static string FormatBytes(long bytes)
        {
            double gigabytes = bytes / BytesPerGigabyte;
            return gigabytes >= 1d
                ? $"{gigabytes:0.00} GB"
                : $"{bytes / (1024d * 1024d):0.00} MB";
        }

        private static string FormatBitRate(long bitsPerSecond)
        {
            if (bitsPerSecond >= 1_000_000_000L)
            {
                return $"{bitsPerSecond / 1_000_000_000d:0.##} Gbps";
            }

            if (bitsPerSecond >= 1_000_000L)
            {
                return $"{bitsPerSecond / 1_000_000d:0.##} Mbps";
            }

            return $"{Math.Max(0, bitsPerSecond) / 1_000d:0.##} Kbps";
        }

        private static string FormatUptime(TimeSpan uptime)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours:00}h {uptime.Minutes:00}m";
        }

        private static string ValueOrUnavailable(string value)
        {
            return ValueOrFallback(value, "Unavailable");
        }

        private static string ValueOrFallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
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

        private readonly record struct MemorySummary(
            double TotalGigabytes,
            double UsedGigabytes,
            double UsedPercent);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx memoryStatus);
    }
}
