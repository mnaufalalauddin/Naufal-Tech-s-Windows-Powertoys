using Microsoft.Win32;
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
    internal readonly record struct GamingLiveMmcssSnapshot(
        string Profile,
        int? Win32PrioritySeparation,
        int? SystemResponsiveness,
        int? GamesPriority,
        int? GpuPriority,
        string SchedulingCategory,
        string SfioPriority,
        int? ClockRate,
        int? NoLazyMode,
        int? AlwaysOn);

    internal readonly record struct GamingLiveCommandResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);

    internal readonly record struct GamingLiveStatusSnapshot(
        string PowerPlanName,
        string PowerPlanGuid,
        string GameMode,
        string GameModeValue,
        string Hags,
        string HagsValue,
        string WindowedOptimization,
        string WindowedDetail,
        string DynamicTick,
        string DynamicTickValue,
        string Hpet,
        string HpetValue,
        string CoreParking,
        string CoreParkingDetail,
        string CpuPerformance,
        string CpuPerformanceDetail,
        string Mpo,
        string MpoDetail,
        string SysMain,
        string SysMainDetail,
        string GameDvr,
        string Nagle,
        string NetworkThrottling,
        string Rsc,
        string ShaderCacheInfo,
        GamingLiveCommandResult PowerCfgList,
        GamingLiveCommandResult PowerCfgActive,
        GamingLiveMmcssSnapshot Mmcss)
    {
        public GamingLiveCommandResult Bcd { get; init; }
        public bool RscReadable { get; init; }
        public IReadOnlyList<ProfileRscAdapter> RscAdapters { get; init; } = Array.Empty<ProfileRscAdapter>();
        public PerformanceProfileState? PerformanceProfile { get; init; }
    }

    /// <summary>
    /// Read-only native equivalent of the reference LIVE GAMING STATUS worker.
    /// It never writes registry, BCD, service, power-plan, or adapter state.
    /// </summary>
    internal sealed class GamingLiveStatusService
    {
        private const string PowerSchemesPath =
            @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes";
        private const string ProcessorSubgroupGuid =
            "54533251-82be-4824-96c1-47b60b740d00";
        private const string GameModePath = @"Software\Microsoft\GameBar";
        private const string GraphicsDriversPath =
            @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
        private const string WindowedOptimizationPath =
            @"Software\Microsoft\DirectX\UserGpuPreferences";
        private const string MpoPath = @"SOFTWARE\Microsoft\Windows\Dwm";
        private const string GameDvrConfigPath = @"System\GameConfigStore";
        private const string GameDvrCapturePath =
            @"Software\Microsoft\Windows\CurrentVersion\GameDVR";
        private const string GameDvrPolicyPath =
            @"SOFTWARE\Policies\Microsoft\Windows\GameDVR";
        private const string TcpInterfacesPath =
            @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
        private const string MultimediaProfilePath =
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        private const string SysMainPath =
            @"SYSTEM\CurrentControlSet\Services\SysMain";
        private const string MmcssPriorityPath =
            @"SYSTEM\CurrentControlSet\Control\PriorityControl";
        private const string MmcssSystemProfilePath =
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        private const string MmcssGamesPath =
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";

        private static readonly Regex GuidPattern = new(
            @"(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
            RegexOptions.CultureInvariant);

        private static readonly IReadOnlyDictionary<string, string> PowerSettingGuids =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CPMINCORES"] = "0cc5b647-c1df-4637-891a-dec35c318583",
                ["CPMAXCORES"] = "ea062031-0e34-4ff1-9b6d-eb1059334028",
                ["PROCTHROTTLEMIN"] = "893dee8e-2bef-41e0-89c6-b55d0929964c",
                ["PROCTHROTTLEMAX"] = "bc5038f7-23e0-4960-96da-33abaf5935ec",
                ["PERFINCPOL"] = "465e1f50-b610-473a-ab58-00d1077dc418"
            };

        private static readonly LiveMmcssDefinition[] MmcssProfiles =
        {
            new("Competitive Gaming", 0x24, 1, 2, 8, "High", "High", 10000, 1, 1),
            new("Optimized Gaming", 0x18, 20, 4, 6, "Medium", "High", 10000, 0, 0),
            new("Balanced", 0x02, 20, 2, 8, "Medium", "Normal", 10000, 0, 0)
        };

        public async Task<GamingLiveStatusSnapshot> ReadSnapshotAsync()
        {
            string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
            Task<GamingLiveCommandResult> powerListTask = RunCommandAsync(
                Path.Combine(systemDirectory, "powercfg.exe"), new[] { "/list" });
            Task<GamingLiveCommandResult> powerActiveTask = RunCommandAsync(
                Path.Combine(systemDirectory, "powercfg.exe"), new[] { "/getactivescheme" });
            Task<GamingLiveCommandResult> bcdTask = RunCommandAsync(
                Path.Combine(systemDirectory, "bcdedit.exe"), new[] { "/enum", "{current}" });
            Task<(bool Readable, IReadOnlyList<ProfileRscAdapter> Adapters)> rscTask = Task.Run(() =>
            {
                try { return (true, NativeRscReader.Read()); }
                catch { return (false, (IReadOnlyList<ProfileRscAdapter>)Array.Empty<ProfileRscAdapter>()); }
            });

            await Task.WhenAll(powerListTask, powerActiveTask, bcdTask, rscTask)
                .ConfigureAwait(false);
            GamingLiveCommandResult powerList = await powerListTask.ConfigureAwait(false);
            GamingLiveCommandResult powerActive = await powerActiveTask.ConfigureAwait(false);
            GamingLiveCommandResult bcd = await bcdTask.ConfigureAwait(false);
            var rscResult = await rscTask.ConfigureAwait(false);
            (string powerName, string powerGuid) = ReadPowerPlan(powerList, powerActive);

            Task<uint?> coreMinTask = ReadPowerSettingAsync(powerGuid, "CPMINCORES");
            Task<uint?> coreMaxTask = ReadPowerSettingAsync(powerGuid, "CPMAXCORES");
            Task<uint?> cpuMinTask = ReadPowerSettingAsync(powerGuid, "PROCTHROTTLEMIN");
            Task<uint?> cpuMaxTask = ReadPowerSettingAsync(powerGuid, "PROCTHROTTLEMAX");
            Task<uint?> perfIncreaseTask = ReadPowerSettingAsync(powerGuid, "PERFINCPOL");
            await Task.WhenAll(coreMinTask, coreMaxTask, cpuMinTask, cpuMaxTask, perfIncreaseTask)
                .ConfigureAwait(false);

            (string gameMode, string gameModeValue) = ReadGameMode();
            (string hags, string hagsValue) = ReadHags();
            (string windowed, string windowedDetail) = ReadWindowedOptimization();
            (string dynamicTick, string dynamicTickValue) = bcd.ExitCode == 0
                ? ReadDynamicTick(bcd.StandardOutput) : ("UNKNOWN", "UNAVAILABLE");
            (string hpet, string hpetValue) = bcd.ExitCode == 0
                ? ReadHpet(bcd.StandardOutput) : ("UNKNOWN", "UNAVAILABLE");
            (string coreParking, string coreParkingDetail) = ReadCoreParking(
                await coreMinTask.ConfigureAwait(false), await coreMaxTask.ConfigureAwait(false));
            (string cpuPerformance, string cpuPerformanceDetail) = ReadCpuPerformance(
                await cpuMinTask.ConfigureAwait(false),
                await cpuMaxTask.ConfigureAwait(false),
                await perfIncreaseTask.ConfigureAwait(false));
            (string mpo, string mpoDetail) = ReadMpo();
            (string sysMain, string sysMainDetail) = ReadSysMain();

            GamingLiveStatusSnapshot snapshot = new(
                powerName,
                powerGuid,
                gameMode,
                gameModeValue,
                hags,
                hagsValue,
                windowed,
                windowedDetail,
                dynamicTick,
                dynamicTickValue,
                hpet,
                hpetValue,
                coreParking,
                coreParkingDetail,
                cpuPerformance,
                cpuPerformanceDetail,
                mpo,
                mpoDetail,
                sysMain,
                sysMainDetail,
                ReadGameDvr(),
                ReadNagle(),
                ReadNetworkThrottling(),
                FormatRsc(rscResult.Readable, rscResult.Adapters),
                ReadShaderCacheInfo(),
                powerList,
                powerActive,
                ReadMmcss()) { Bcd = bcd, RscReadable = rscResult.Readable, RscAdapters = rscResult.Adapters };
            return snapshot with
            {
                PerformanceProfile = await PerformanceProfileVerificationService.ReadAsync(snapshot).ConfigureAwait(false)
            };
        }

        public static string FormatDetails(GamingLiveStatusSnapshot snapshot)
        {
            GamingLiveMmcssSnapshot mmcss = snapshot.Mmcss;
            string shortGuid = snapshot.PowerPlanGuid.Length >= 8
                ? snapshot.PowerPlanGuid[..8]
                : "--------";
            StringBuilder output = new();
            output.AppendLine("=== MMCSS ===");
            output.AppendLine();
            AppendValueBlock(output, "Profile:", mmcss.Profile);
            AppendValueBlock(
                output,
                "Win32PrioritySeparation:",
                mmcss.Win32PrioritySeparation.HasValue
                    ? $"0x{mmcss.Win32PrioritySeparation.Value:X2}"
                    : "UNKNOWN");
            AppendValueBlock(output, "System Responsiveness:", NumberOrUnknown(mmcss.SystemResponsiveness));
            AppendValueBlock(output, "Games Priority:", NumberOrUnknown(mmcss.GamesPriority));
            AppendValueBlock(output, "GPU Priority:", NumberOrUnknown(mmcss.GpuPriority));
            AppendValueBlock(output, "Scheduling Category:", TextOrUnknown(mmcss.SchedulingCategory));
            AppendValueBlock(output, "SFIO Priority:", TextOrUnknown(mmcss.SfioPriority));
            AppendValueBlock(output, "Clock Rate:", NumberOrUnknown(mmcss.ClockRate));
            AppendValueBlock(
                output,
                "NoLazyMode / AlwaysOn:",
                mmcss.NoLazyMode.HasValue && mmcss.AlwaysOn.HasValue
                    ? $"{mmcss.NoLazyMode.Value} / {mmcss.AlwaysOn.Value}"
                    : "UNKNOWN");

            output.AppendLine("=== WINDOWS ===");
            output.AppendLine();
            output.AppendLine($"POWER PLAN : {snapshot.PowerPlanName}  [{shortGuid}]");
            output.AppendLine($"GAME MODE  : {snapshot.GameMode}  (AutoGameModeEnabled={snapshot.GameModeValue})");
            output.AppendLine($"HAGS       : {snapshot.Hags}  (HwSchMode={snapshot.HagsValue})");
            output.AppendLine($"WINDOWED   : {snapshot.WindowedOptimization}  ({snapshot.WindowedDetail})");
            output.AppendLine($"DYN TICK   : {snapshot.DynamicTick}  (BCD={snapshot.DynamicTickValue})");
            output.AppendLine($"HPET       : {snapshot.Hpet}  (BCD={snapshot.HpetValue})");
            output.AppendLine($"CORE PARK  : {snapshot.CoreParking}  ({snapshot.CoreParkingDetail})");
            output.AppendLine($"CPU PERF   : {snapshot.CpuPerformance}  ({snapshot.CpuPerformanceDetail})");
            output.AppendLine($"MPO        : {snapshot.Mpo}  ({snapshot.MpoDetail})");
            output.AppendLine($"SYSMAIN    : {snapshot.SysMain}  ({snapshot.SysMainDetail})");
            output.AppendLine($"GAME DVR   : {snapshot.GameDvr}");
            output.AppendLine(
                $"NETWORK    : Nagle={snapshot.Nagle}  " +
                $"Throttle={snapshot.NetworkThrottling}  RSC={snapshot.Rsc}");
            output.AppendLine($"SHADER     : {snapshot.ShaderCacheInfo}");
            output.AppendLine();
            output.AppendLine($"--- RAW powercfg /list (exit={snapshot.PowerCfgList.ExitCode}) ---");
            output.AppendLine(NormalizeNativeOutput(snapshot.PowerCfgList.StandardOutput));
            AppendNativeError(output, snapshot.PowerCfgList.StandardError);
            output.AppendLine();
            output.AppendLine(
                $"--- RAW powercfg /getactivescheme (exit={snapshot.PowerCfgActive.ExitCode}) ---");
            output.AppendLine(NormalizeNativeOutput(snapshot.PowerCfgActive.StandardOutput));
            AppendNativeError(output, snapshot.PowerCfgActive.StandardError);
            return output.ToString().TrimEnd();
        }

        private static void AppendValueBlock(StringBuilder output, string name, string value)
        {
            output.AppendLine(name);
            output.AppendLine(value);
            output.AppendLine();
        }

        private static string NumberOrUnknown(int? value) =>
            value?.ToString(CultureInfo.InvariantCulture) ?? "UNKNOWN";

        private static string TextOrUnknown(string value) =>
            string.IsNullOrWhiteSpace(value) ? "UNKNOWN" : value;

        private static string NormalizeNativeOutput(string? value)
        {
            string result = (value ?? string.Empty).Trim('\r', '\n');
            return string.IsNullOrWhiteSpace(result) ? "<no stdout>" : result;
        }

        private static void AppendNativeError(StringBuilder output, string? value)
        {
            string error = (value ?? string.Empty).Trim('\r', '\n');
            if (string.IsNullOrWhiteSpace(error))
            {
                return;
            }
            string[] lines = Regex.Split(error, "\\r?\\n");
            for (int index = 0; index < lines.Length; index++)
            {
                output.AppendLine(index == 0 ? $"[stderr] {lines[index]}" : lines[index]);
            }
        }

        private static (string Name, string Guid) ReadPowerPlan(
            GamingLiveCommandResult list,
            GamingLiveCommandResult active)
        {
            string registryGuid = ReadString(
                Registry.LocalMachine,
                PowerSchemesPath,
                "ActivePowerScheme") ?? string.Empty;
            Match activeMatch = GuidPattern.Match(active.StandardOutput);
            string guid = activeMatch.Success ? activeMatch.Value : registryGuid;
            string name = ParsePowerPlanName(list.StandardOutput, guid);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = ParsePowerPlanName(active.StandardOutput, guid);
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                name = ReadPowerPlanRegistryName(guid);
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                name = guid.ToLowerInvariant() switch
                {
                    "381b4222-f694-41f0-9685-ff5bb260df2e" => "Balanced",
                    "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" => "High performance",
                    "e9a42b02-d5df-448d-aa00-03f14749eb61" => "Ultimate Performance",
                    "a1841308-3541-4fab-bc81-f71556f20b4a" => "Power saver",
                    _ => "Unknown"
                };
            }
            return (name, Guid.TryParse(guid, out _) ? guid : string.Empty);
        }

        private static string ParsePowerPlanName(string output, string guid)
        {
            if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(guid))
            {
                return string.Empty;
            }
            foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int guidIndex = line.IndexOf(guid, StringComparison.OrdinalIgnoreCase);
                if (guidIndex < 0)
                {
                    continue;
                }
                string afterGuid = line[(guidIndex + guid.Length)..];
                int open = afterGuid.IndexOf('(');
                int close = afterGuid.LastIndexOf(')');
                if (open >= 0 && close > open)
                {
                    return afterGuid.Substring(open + 1, close - open - 1).Trim();
                }
            }
            return string.Empty;
        }

        private static string ReadPowerPlanRegistryName(string guid)
        {
            if (!Guid.TryParse(guid, out _))
            {
                return string.Empty;
            }
            string friendlyName = ReadString(
                Registry.LocalMachine,
                $@"{PowerSchemesPath}\{guid}",
                "FriendlyName") ?? string.Empty;
            return friendlyName.StartsWith('@') ? string.Empty : friendlyName;
        }

        private static (string Status, string Value) ReadGameMode()
        {
            int? value = ReadInt(Registry.CurrentUser, GameModePath, "AutoGameModeEnabled");
            return (
                value switch { 1 => "ON", 0 => "OFF", null => "DEFAULT", _ => "UNKNOWN" },
                value?.ToString(CultureInfo.InvariantCulture) ?? "-");
        }

        private static (string Status, string Value) ReadHags()
        {
            int? value = ReadInt(Registry.LocalMachine, GraphicsDriversPath, "HwSchMode");
            return (
                value switch { 2 => "ON", 1 => "OFF", null => "DEFAULT", _ => "UNKNOWN" },
                value?.ToString(CultureInfo.InvariantCulture) ?? "-");
        }

        private static (string Status, string Detail) ReadWindowedOptimization()
        {
            string? value = ReadString(
                Registry.CurrentUser,
                WindowedOptimizationPath,
                "DirectXUserGlobalSettings");
            if (value is null)
            {
                return ("DEFAULT", "not overridden");
            }
            if (Regex.IsMatch(value, @"(?i)SwapEffectUpgradeEnable=0"))
            {
                return ("OFF", "SwapEffectUpgradeEnable=0");
            }
            if (Regex.IsMatch(value, @"(?i)SwapEffectUpgradeEnable=1"))
            {
                return ("ON", "SwapEffectUpgradeEnable=1");
            }
            return ("CUSTOM", "custom DirectXUserGlobalSettings");
        }

        private static (string Status, string Value) ReadDynamicTick(string bcdOutput)
        {
            string? value = ReadBcdOption(bcdOutput, "disabledynamictick");
            return (
                value?.ToUpperInvariant() switch { "YES" => "OFF", "NO" => "ON", _ => "DEFAULT" },
                value ?? "not set");
        }

        private static (string Status, string Value) ReadHpet(string bcdOutput)
        {
            string? value = ReadBcdOption(bcdOutput, "useplatformclock");
            return (
                value?.ToUpperInvariant() switch
                {
                    "YES" => "FORCED ON",
                    "NO" => "FORCED OFF",
                    _ => "DEFAULT"
                },
                value ?? "not set");
        }

        private static string? ReadBcdOption(string output, string name)
        {
            Match match = Regex.Match(
                output ?? string.Empty,
                $@"(?im)^\s*{Regex.Escape(name)}\s+(\S+)\s*$",
                RegexOptions.CultureInvariant);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static (string Status, string Detail) ReadCoreParking(uint? minimum, uint? maximum)
        {
            if (!minimum.HasValue)
            {
                return ("UNKNOWN", "CPMinCores unavailable");
            }
            return (
                minimum.Value >= 100 ? "OFF" : "ON",
                $"Min {minimum.Value}% / Max {(maximum.HasValue ? $"{maximum.Value}%" : "?")}");
        }

        private static (string Status, string Detail) ReadCpuPerformance(
            uint? minimum,
            uint? maximum,
            uint? increasePolicy)
        {
            if (!minimum.HasValue || !maximum.HasValue)
            {
                return ("UNKNOWN", "processor settings unavailable");
            }
            string status = minimum.Value >= 100 && maximum.Value >= 100
                ? "MAX"
                : minimum.Value == 0 && maximum.Value == 100
                    ? "DYNAMIC"
                    : "CUSTOM";
            string policy = increasePolicy switch
            {
                0 => "0 (Ideal)",
                1 => "1 (One step closer to Ideal)",
                2 => "2 (Highest speed/power)",
                3 => "3 (Ideal / Responsiveness optimized)",
                not null => $"{increasePolicy.Value} (Unknown)",
                _ => "? (unavailable)"
            };
            return (
                status,
                $"Min {minimum.Value}% / Max {maximum.Value}%{Environment.NewLine}" +
                $"           IncPolicy {policy}");
        }

        private static (string Status, string Detail) ReadMpo()
        {
            int? value = ReadInt(Registry.LocalMachine, MpoPath, "OverlayTestMode");
            return value switch
            {
                5 => ("OFF", "OverlayTestMode=5"),
                not null => ("CUSTOM", $"OverlayTestMode={value.Value}"),
                _ => ("DEFAULT", "Windows default")
            };
        }

        private static (string Status, string Detail) ReadSysMain()
        {
            int? start = ReadInt(Registry.LocalMachine, SysMainPath, "Start");
            if (!start.HasValue)
            {
                return ("UNKNOWN", "service unavailable");
            }
            string startMode = start.Value switch
            {
                0 => "Boot",
                1 => "System",
                2 => "Auto",
                3 => "Manual",
                4 => "Disabled",
                _ => "Unknown"
            };
            string state = TryReadServiceState("SysMain", out string serviceState)
                ? serviceState
                : "Unknown";
            string status = start.Value == 4 && state != "Running"
                ? "OFF"
                : state == "Running"
                    ? "ON"
                    : "NOT RUNNING";
            return (status, $"{startMode} / {state}");
        }

        private static string ReadGameDvr()
        {
            int?[] values =
            {
                ReadInt(Registry.CurrentUser, GameDvrConfigPath, "GameDVR_Enabled"),
                ReadInt(Registry.CurrentUser, GameDvrCapturePath, "AppCaptureEnabled"),
                ReadInt(Registry.CurrentUser, GameDvrCapturePath, "HistoricalCaptureEnabled"),
                ReadInt(Registry.LocalMachine, GameDvrPolicyPath, "AllowGameDVR")
            };
            if (!values.All(value => value.HasValue))
            {
                return "DEFAULT";
            }
            int enabled = values.Count(value => value == 1);
            int disabled = values.Count(value => value == 0);
            return disabled == 4 ? "OFF" : enabled == 4 ? "ON" : "MIXED";
        }

        private static string ReadNagle()
        {
            List<int> values = new();
            try
            {
                using RegistryKey? interfaces = Registry.LocalMachine.OpenSubKey(TcpInterfacesPath);
                foreach (string name in interfaces?.GetSubKeyNames() ?? Array.Empty<string>())
                {
                    using RegistryKey? key = interfaces?.OpenSubKey(name);
                    object? raw = key?.GetValue(
                        "TCPNoDelay",
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (raw is not null)
                    {
                        values.Add(Convert.ToInt32(raw, CultureInfo.InvariantCulture));
                    }
                }
            }
            catch
            {
                return "UNKNOWN";
            }
            return values.Count == 0
                ? "DEFAULT"
                : values.All(value => value == 1)
                    ? "OFF"
                    : values.All(value => value == 0)
                        ? "ON"
                        : "MIXED";
        }

        private static string ReadNetworkThrottling()
        {
            int? value = ReadInt(
                Registry.LocalMachine,
                MultimediaProfilePath,
                "NetworkThrottlingIndex");
            return !value.HasValue
                ? "DEFAULT"
                : unchecked((uint)value.Value) == uint.MaxValue
                    ? "OFF"
                    : "ON";
        }

        internal static string FormatRsc(bool readable, IReadOnlyList<ProfileRscAdapter> adapters)
        {
            if (!readable || adapters.Count == 0) return "UNKNOWN";
            if (adapters.All(adapter => !adapter.Ipv4Enabled && !adapter.Ipv6Enabled)) return "OFF";
            if (adapters.All(adapter => adapter.Ipv4Enabled && adapter.Ipv6Enabled)) return "ON";
            return "MIXED";
        }

        private static string ReadShaderCacheInfo()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string[] paths =
            {
                Path.Combine(local, "D3DSCache"),
                Path.Combine(local, "NVIDIA", "DXCache"),
                Path.Combine(local, "NVIDIA", "GLCache"),
                Path.Combine(common, "NVIDIA Corporation", "NV_Cache")
            };
            string[] existing = paths.Where(Directory.Exists).ToArray();
            if (existing.Length == 0)
            {
                return "NOT FOUND";
            }
            long bytes = 0;
            foreach (string path in existing)
            {
                try
                {
                    foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            bytes += new FileInfo(file).Length;
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }
            }
            return $"{Math.Round(bytes / 1024d / 1024d, 1).ToString(CultureInfo.CurrentCulture)} MB";
        }

        private static GamingLiveMmcssSnapshot ReadMmcss()
        {
            int? win32 = ReadInt(Registry.LocalMachine, MmcssPriorityPath, "Win32PrioritySeparation");
            int? responsiveness = ReadInt(Registry.LocalMachine, MmcssSystemProfilePath, "SystemResponsiveness");
            int? gamesPriority = ReadInt(Registry.LocalMachine, MmcssGamesPath, "Priority");
            int? gpuPriority = ReadInt(Registry.LocalMachine, MmcssGamesPath, "GPU Priority");
            string category = ReadString(Registry.LocalMachine, MmcssGamesPath, "Scheduling Category") ?? string.Empty;
            string sfio = ReadString(Registry.LocalMachine, MmcssGamesPath, "SFIO Priority") ?? string.Empty;
            int? clockRate = ReadInt(Registry.LocalMachine, MmcssGamesPath, "Clock Rate");
            int? noLazyMode = ReadInt(Registry.LocalMachine, MmcssSystemProfilePath, "NoLazyMode");
            int? alwaysOn = ReadInt(Registry.LocalMachine, MmcssSystemProfilePath, "AlwaysOn");

            GamingLiveMmcssSnapshot snapshot = new(
                "Custom",
                win32,
                responsiveness,
                gamesPriority,
                gpuPriority,
                category,
                sfio,
                clockRate,
                noLazyMode,
                alwaysOn);
            if (win32 is null || responsiveness is null || gamesPriority is null ||
                gpuPriority is null || clockRate is null)
            {
                return snapshot with { Profile = "Unavailable" };
            }
            foreach (LiveMmcssDefinition definition in MmcssProfiles)
            {
                if (definition.Matches(snapshot))
                {
                    return snapshot with { Profile = definition.Name };
                }
            }
            return snapshot;
        }

        private static async Task<uint?> ReadPowerSettingAsync(string schemeGuid, string alias)
        {
            if (!Guid.TryParse(schemeGuid, out _) ||
                !PowerSettingGuids.TryGetValue(alias, out string? settingGuid))
            {
                return null;
            }
            string powerCfg = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "powercfg.exe");
            GamingLiveCommandResult result = await RunCommandAsync(
                    powerCfg,
                    new[] { "/query", schemeGuid, ProcessorSubgroupGuid, settingGuid })
                .ConfigureAwait(false);
            Match match = Regex.Match(
                result.StandardOutput,
                @"(?im)Current AC Power Setting Index:\s*0x([0-9a-f]+)",
                RegexOptions.CultureInvariant);
            if (match.Success &&
                uint.TryParse(
                    match.Groups[1].Value,
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out uint parsed))
            {
                return parsed;
            }
            string explicitPath =
                $@"{PowerSchemesPath}\{schemeGuid}\{ProcessorSubgroupGuid}\{settingGuid}";
            uint? explicitValue = ReadUInt(
                Registry.LocalMachine,
                explicitPath,
                "ACSettingIndex");
            if (explicitValue.HasValue)
            {
                return explicitValue;
            }
            string defaultPath =
                $@"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\{ProcessorSubgroupGuid}\{settingGuid}\DefaultPowerSchemeValues\{schemeGuid}";
            return ReadUInt(Registry.LocalMachine, defaultPath, "ACSettingIndex");
        }

        private static async Task<GamingLiveCommandResult> RunCommandAsync(
            string executable, IReadOnlyList<string> arguments)
        {
            try
            {
                NativeCommandResult result = await new NativeCommandRunner().RunAsync(executable, arguments, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                return new GamingLiveCommandResult(result.ExitCode, result.StandardOutput, result.StandardError);
            }
            catch (Exception exception)
            {
                return new GamingLiveCommandResult(-1, string.Empty, exception.Message);
            }
        }

        private static int? ReadInt(RegistryKey hive, string path, string name)
        {
            try
            {
                using RegistryKey? key = hive.OpenSubKey(path, writable: false);
                object? value = key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                return value is null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        private static uint? ReadUInt(RegistryKey hive, string path, string name)
        {
            int? value = ReadInt(hive, path, name);
            return value.HasValue ? unchecked((uint)value.Value) : null;
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

        internal static bool TryReadServiceState(string serviceName, out string state)
        {
            state = "Unknown";
            IntPtr manager = OpenSCManagerW(null, null, ScManagerConnect);
            if (manager == IntPtr.Zero)
            {
                return false;
            }
            try
            {
                IntPtr service = OpenServiceW(manager, serviceName, ServiceQueryStatus);
                if (service == IntPtr.Zero)
                {
                    return false;
                }
                try
                {
                    int size = Marshal.SizeOf<ServiceStatusProcess>();
                    IntPtr buffer = Marshal.AllocHGlobal(size);
                    try
                    {
                        if (!QueryServiceStatusEx(
                                service,
                                ScStatusProcessInfo,
                                buffer,
                                size,
                                out _))
                        {
                            return false;
                        }
                        ServiceStatusProcess status = Marshal.PtrToStructure<ServiceStatusProcess>(buffer);
                        state = status.CurrentState switch
                        {
                            1 => "Stopped",
                            2 => "Start Pending",
                            3 => "Stop Pending",
                            4 => "Running",
                            5 => "Continue Pending",
                            6 => "Pause Pending",
                            7 => "Paused",
                            _ => "Unknown"
                        };
                        return true;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(manager);
            }
        }

        private readonly record struct LiveMmcssDefinition(
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
            public bool Matches(GamingLiveMmcssSnapshot value) =>
                value.Win32PrioritySeparation == Win32PrioritySeparation &&
                value.SystemResponsiveness == SystemResponsiveness &&
                value.GamesPriority == GamesPriority &&
                value.GpuPriority == GpuPriority &&
                string.Equals(value.SchedulingCategory, SchedulingCategory, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(value.SfioPriority, SfioPriority, StringComparison.OrdinalIgnoreCase) &&
                value.ClockRate == ClockRate &&
                value.NoLazyMode == NoLazyMode &&
                value.AlwaysOn == AlwaysOn;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ServiceStatusProcess
        {
            public int ServiceType;
            public int CurrentState;
            public int ControlsAccepted;
            public int Win32ExitCode;
            public int ServiceSpecificExitCode;
            public int CheckPoint;
            public int WaitHint;
            public int ProcessId;
            public int ServiceFlags;
        }

        private const int ScManagerConnect = 0x0001;
        private const int ServiceQueryStatus = 0x0004;
        private const int ScStatusProcessInfo = 0;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenSCManagerW(
            string? machineName,
            string? databaseName,
            int desiredAccess);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenServiceW(
            IntPtr serviceControlManager,
            string serviceName,
            int desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryServiceStatusEx(
            IntPtr service,
            int infoLevel,
            IntPtr buffer,
            int bufferSize,
            out int bytesNeeded);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseServiceHandle(IntPtr handle);
    }
}
