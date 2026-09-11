using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal readonly record struct MmcssStatusSnapshot(
        string Profile,
        int? Win32PrioritySeparation,
        int? SystemResponsiveness,
        int? GamesPriority,
        int? GpuPriority,
        string SchedulingCategory,
        string SfioPriority,
        int? ClockRate,
        int NoLazyMode,
        int AlwaysOn,
        string Error);

    internal readonly record struct GamingStatusSnapshot(
        string PowerPlanName,
        string PowerPlanGuid,
        string GameMode,
        string Hags,
        MmcssStatusSnapshot Mmcss);

    internal sealed class GamingStatusService
    {
        private const string PowerSchemesPath =
            @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes";
        private const string GameModePath = @"Software\Microsoft\GameBar";
        private const string GraphicsDriversPath =
            @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
        private const string MmcssPriorityPath =
            @"SYSTEM\CurrentControlSet\Control\PriorityControl";
        private const string MmcssSystemProfilePath =
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        private const string MmcssGamesPath =
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";

        private static readonly Regex GuidPattern = new(
            @"(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
            RegexOptions.CultureInvariant);

        private static readonly MmcssDefinition[] MmcssProfiles =
        {
            new("Competitive Gaming", 0x24, 1, 2, 8, "High", "High", 10000, 1, 1),
            new("Optimized Gaming", 0x18, 20, 4, 6, "Medium", "High", 10000, 0, 0),
            new("Balanced", 0x02, 20, 2, 8, "Medium", "Normal", 10000, 0, 0)
        };

        public async Task<GamingStatusSnapshot> ReadSnapshotAsync()
        {
            (string powerPlanName, string powerPlanGuid) = await ReadPowerPlanAsync();
            string gameMode = ReadGameMode();
            string hags = ReadHags();
            MmcssStatusSnapshot mmcss = ReadMmcss();

            return new GamingStatusSnapshot(
                powerPlanName,
                powerPlanGuid,
                gameMode,
                hags,
                mmcss);
        }

        private static async Task<(string Name, string Guid)> ReadPowerPlanAsync()
        {
            string registryGuid = ReadString(
                Registry.LocalMachine,
                PowerSchemesPath,
                "ActivePowerScheme") ?? string.Empty;

            string output = await RunPowerCfgAsync("/getactivescheme");
            Match match = GuidPattern.Match(output);
            string guid = match.Success ? match.Value : registryGuid;
            string name = ParsePowerPlanName(output, guid);

            if (string.IsNullOrWhiteSpace(name))
            {
                name = ReadPowerPlanRegistryName(guid);
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = GetBuiltInPowerPlanName(guid);
            }

            return (
                string.IsNullOrWhiteSpace(name) ? "Unknown" : name,
                string.IsNullOrWhiteSpace(guid) ? "Unknown" : guid);
        }

        private static string ReadGameMode()
        {
            int? value = ReadInt(Registry.CurrentUser, GameModePath, "AutoGameModeEnabled");
            return value switch
            {
                1 => "ON",
                0 => "OFF",
                null => "DEFAULT",
                _ => "UNKNOWN"
            };
        }

        private static string ReadHags()
        {
            int? value = ReadInt(Registry.LocalMachine, GraphicsDriversPath, "HwSchMode");
            return value switch
            {
                2 => "ON",
                1 => "OFF",
                null => "DEFAULT",
                _ => "UNKNOWN"
            };
        }

        private static MmcssStatusSnapshot ReadMmcss()
        {
            try
            {
                int? win32 = ReadInt(Registry.LocalMachine, MmcssPriorityPath, "Win32PrioritySeparation");
                int? responsiveness = ReadInt(Registry.LocalMachine, MmcssSystemProfilePath, "SystemResponsiveness");
                int? gamesPriority = ReadInt(Registry.LocalMachine, MmcssGamesPath, "Priority");
                int? gpuPriority = ReadInt(Registry.LocalMachine, MmcssGamesPath, "GPU Priority");
                string category = ReadString(Registry.LocalMachine, MmcssGamesPath, "Scheduling Category") ?? string.Empty;
                string sfio = ReadString(Registry.LocalMachine, MmcssGamesPath, "SFIO Priority") ?? string.Empty;
                int? clockRate = ReadInt(Registry.LocalMachine, MmcssGamesPath, "Clock Rate");
                int noLazyMode = ReadInt(Registry.LocalMachine, MmcssSystemProfilePath, "NoLazyMode") ?? 0;
                int alwaysOn = ReadInt(Registry.LocalMachine, MmcssSystemProfilePath, "AlwaysOn") ?? 0;

                if (win32 is null || responsiveness is null || gamesPriority is null ||
                    gpuPriority is null || clockRate is null)
                {
                    return new MmcssStatusSnapshot(
                        "Unavailable", win32, responsiveness, gamesPriority, gpuPriority,
                        category, sfio, clockRate, noLazyMode, alwaysOn,
                        "One or more required MMCSS registry values are unavailable.");
                }

                MmcssStatusSnapshot snapshot = new(
                    "Custom",
                    win32,
                    responsiveness,
                    gamesPriority,
                    gpuPriority,
                    category,
                    sfio,
                    clockRate,
                    noLazyMode,
                    alwaysOn,
                    string.Empty);

                foreach (MmcssDefinition definition in MmcssProfiles)
                {
                    if (definition.Matches(snapshot))
                    {
                        return snapshot with { Profile = definition.Name };
                    }
                }

                return snapshot;
            }
            catch (Exception exception)
            {
                return new MmcssStatusSnapshot(
                    "Unavailable", null, null, null, null,
                    string.Empty, string.Empty, null, 0, 0, exception.Message);
            }
        }

        public static string FormatDetails(GamingStatusSnapshot snapshot)
        {
            MmcssStatusSnapshot mmcss = snapshot.Mmcss;
            StringBuilder builder = new();

            builder.AppendLine("=== GAMING STATUS ===");
            builder.AppendLine();
            builder.AppendLine($"Power Plan: {snapshot.PowerPlanName}");
            builder.AppendLine($"Power Plan GUID: {snapshot.PowerPlanGuid}");
            builder.AppendLine($"Game Mode: {snapshot.GameMode}");
            builder.AppendLine($"HAGS: {snapshot.Hags}");
            builder.AppendLine();
            builder.AppendLine("=== MMCSS ===");
            builder.AppendLine();
            builder.AppendLine($"Profile: {mmcss.Profile}");
            builder.AppendLine($"Win32PrioritySeparation: {FormatHex(mmcss.Win32PrioritySeparation)}");
            builder.AppendLine($"System Responsiveness: {FormatNullable(mmcss.SystemResponsiveness)}");
            builder.AppendLine($"Games Priority: {FormatNullable(mmcss.GamesPriority)}");
            builder.AppendLine($"GPU Priority: {FormatNullable(mmcss.GpuPriority)}");
            builder.AppendLine($"Scheduling Category: {ValueOrUnavailable(mmcss.SchedulingCategory)}");
            builder.AppendLine($"SFIO Priority: {ValueOrUnavailable(mmcss.SfioPriority)}");
            builder.AppendLine($"Clock Rate: {FormatNullable(mmcss.ClockRate)}");
            builder.AppendLine($"NoLazyMode: {mmcss.NoLazyMode}");
            builder.AppendLine($"AlwaysOn: {mmcss.AlwaysOn}");

            if (!string.IsNullOrWhiteSpace(mmcss.Error))
            {
                builder.AppendLine();
                builder.AppendLine($"MMCSS note: {mmcss.Error}");
            }

            return builder.ToString().TrimEnd();
        }

        private static async Task<string> RunPowerCfgAsync(string argument)
        {
            string executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "powercfg.exe");
            try
            {
                NativeCommandResult result = await new NativeCommandRunner().RunAsync(executable, new[] { argument }, TimeSpan.FromSeconds(4));
                return !result.TimedOut && result.ExitCode == 0 ? result.StandardOutput : string.Empty;
            }
            catch { return string.Empty; }
        }

        private static string ParsePowerPlanName(string output, string guid)
        {
            if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(guid))
            {
                return string.Empty;
            }

            int guidIndex = output.IndexOf(guid, StringComparison.OrdinalIgnoreCase);
            if (guidIndex < 0)
            {
                return string.Empty;
            }

            string afterGuid = output[(guidIndex + guid.Length)..];
            int open = afterGuid.IndexOf('(');
            int close = afterGuid.IndexOf(')', open + 1);

            return open >= 0 && close > open
                ? afterGuid.Substring(open + 1, close - open - 1).Trim()
                : string.Empty;
        }

        private static string ReadPowerPlanRegistryName(string guid)
        {
            if (!Guid.TryParse(guid, out _))
            {
                return string.Empty;
            }

            string path = $@"{PowerSchemesPath}\{guid}";
            string friendlyName = ReadString(Registry.LocalMachine, path, "FriendlyName") ?? string.Empty;
            return friendlyName.StartsWith('@') ? string.Empty : friendlyName;
        }

        private static string GetBuiltInPowerPlanName(string guid)
        {
            return guid.ToLowerInvariant() switch
            {
                "381b4222-f694-41f0-9685-ff5bb260df2e" => "Balanced",
                "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" => "High performance",
                "e9a42b02-d5df-448d-aa00-03f14749eb61" => "Ultimate Performance",
                "a1841308-3541-4fab-bc81-f71556f20b4a" => "Power saver",
                _ => string.Empty
            };
        }

        private static int? ReadInt(RegistryKey hive, string path, string name)
        {
            try
            {
                using RegistryKey? key = hive.OpenSubKey(path, writable: false);
                object? value = key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                return value is null
                    ? null
                    : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        private static string? ReadString(RegistryKey hive, string path, string name)
        {
            try
            {
                using RegistryKey? key = hive.OpenSubKey(path, writable: false);
                object? value = key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                return value?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static string FormatHex(int? value)
        {
            return value is null ? "Unavailable" : $"0x{value.Value:X}";
        }

        private static string FormatNullable(int? value)
        {
            return value?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable";
        }

        private static string ValueOrUnavailable(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Unavailable" : value;
        }

        private readonly record struct MmcssDefinition(
            string Name,
            int Win32PrioritySeparation,
            int SystemResponsiveness,
            int GamesPriority,
            int GpuPriority,
            string SchedulingCategory,
            string SfioPriority,
            int ClockRate,
            int NoLazyMode,
            int AlwaysOn)
        {
            public bool Matches(MmcssStatusSnapshot snapshot)
            {
                return snapshot.Win32PrioritySeparation == Win32PrioritySeparation &&
                       snapshot.SystemResponsiveness == SystemResponsiveness &&
                       snapshot.GamesPriority == GamesPriority &&
                       snapshot.GpuPriority == GpuPriority &&
                       string.Equals(snapshot.SchedulingCategory, SchedulingCategory, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(snapshot.SfioPriority, SfioPriority, StringComparison.OrdinalIgnoreCase) &&
                       snapshot.ClockRate == ClockRate &&
                       snapshot.NoLazyMode == NoLazyMode &&
                       snapshot.AlwaysOn == AlwaysOn;
            }
        }
    }
}
