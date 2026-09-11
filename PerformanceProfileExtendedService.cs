using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal readonly record struct ExtendedProfileApplyResult(
        bool Success,
        bool RolledBack,
        bool RestartRequired,
        string Message,
        bool? RollbackSucceeded = null);

    internal sealed class PerformanceProfileExtendedService
    {
        private const string ProcessorSubgroup =
            "54533251-82be-4824-96c1-47b60b740d00";
        private const string BalancedGuid =
            "381b4222-f694-41f0-9685-ff5bb260df2e";
        private const string HighPerformanceGuid =
            "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        private const string InterfaceRoot =
            @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
        private const string TcpGlobalPath =
            @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";
        private const string MultimediaProfilePath =
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        private const string QosPath =
            @"SOFTWARE\Policies\Microsoft\Windows\Psched";

        private static readonly IReadOnlyDictionary<string, string> PowerSettings =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PROCTHROTTLEMIN"] = "893dee8e-2bef-41e0-89c6-b55d0929964c",
                ["PROCTHROTTLEMAX"] = "bc5038f7-23e0-4960-96da-33abaf5935ec",
                ["PERFINCPOL"] = "465e1f50-b610-473a-ab58-00d1077dc418",
                ["CPMINCORES"] = "0cc5b647-c1df-4637-891a-dec35c318583",
                ["CPMAXCORES"] = "ea062031-0e34-4ff1-9b6d-eb1059334028"
            };

        private readonly NativeCommandRunner _commandRunner = new();

        public async Task<ExtendedProfileApplyResult> ApplyAsync(
            PerformanceProfileKind profile,
            string targetPowerGuid,
            Func<Task> verifyFullProfile)
        {
            ExtendedSnapshot snapshot;
            try
            {
                snapshot = await CaptureSnapshotAsync(targetPowerGuid);
            }
            catch (Exception exception)
            {
                return new ExtendedProfileApplyResult(
                    false,
                    false,
                    false,
                    $"Extended profile preflight failed: {exception.Message}");
            }

            try
            {
                await ApplyPowerPolicyAsync(profile, targetPowerGuid);
                await ApplyBcdPolicyAsync(profile);
                await ApplyNetworkPolicyAsync(profile, snapshot.Rsc);
                await VerifyAsync(profile, targetPowerGuid);
                // Keep the full 23-check verification inside the rollback boundary.
                await verifyFullProfile();
                bool restartRequired = !string.Equals(snapshot.DynamicTick,
                    profile == PerformanceProfileKind.CompetitiveGaming ? "Yes" : null,
                    StringComparison.OrdinalIgnoreCase) || !string.Equals(snapshot.Hpet,
                    profile == PerformanceProfileKind.CompetitiveGaming ? "No" : null,
                    StringComparison.OrdinalIgnoreCase);
                return new ExtendedProfileApplyResult(
                    true,
                    false,
                    restartRequired,
                    "CPU policy, BCD/timer, TCP/QoS, and RSC settings were applied and verified.");
            }
            catch (Exception exception)
            {
                List<string> rollbackErrors = await RestoreSnapshotAsync(snapshot);
                string rollback = rollbackErrors.Count == 0
                    ? "Extended profile rollback completed."
                    : $"Extended rollback warnings: {string.Join(" | ", rollbackErrors)}";
                return new ExtendedProfileApplyResult(
                    false,
                    true,
                    false,
                    $"{exception.Message} {rollback}", rollbackErrors.Count == 0);
            }
        }

        private async Task<ExtendedSnapshot> CaptureSnapshotAsync(string targetPowerGuid)
        {
            List<RegistrySnapshot> registry = new();
            using (RegistryKey? interfaces = OpenLocalMachineKey(
                       InterfaceRoot,
                       writable: false))
            {
                if (interfaces is not null)
                {
                    foreach (string interfaceName in interfaces.GetSubKeyNames())
                    {
                        string path = $@"{InterfaceRoot}\{interfaceName}";
                        registry.Add(CaptureRegistry(path, "TCPNoDelay"));
                        registry.Add(CaptureRegistry(path, "TcpAckFrequency"));
                    }
                }
            }
            registry.Add(CaptureRegistry(TcpGlobalPath, "TcpNoDelay"));
            registry.Add(CaptureRegistry(MultimediaProfilePath, "NetworkThrottlingIndex"));
            registry.Add(CaptureRegistry(QosPath, "NonBestEffortLimit"));
            registry.Add(CaptureRegistry(QosPath, "DisableUserTOSSetting"));

            List<PowerSettingSnapshot> power = new();
            foreach ((string alias, string settingGuid) in PowerSettings)
            {
                PowerPair pair = ReadCurrentPowerPair(targetPowerGuid, settingGuid);
                power.Add(new PowerSettingSnapshot(alias, pair));
            }

            return new ExtendedSnapshot(
                registry,
                power,
                await ReadBcdValueAsync("disabledynamictick"),
                await ReadBcdValueAsync("useplatformclock"),
                await Task.Run(NativeRscReader.Read),
                targetPowerGuid);
        }

        private async Task ApplyPowerPolicyAsync(
            PerformanceProfileKind profile,
            string targetPowerGuid)
        {
            IReadOnlyDictionary<string, PowerPair> desired;
            if (profile == PerformanceProfileKind.CompetitiveGaming)
            {
                desired = new Dictionary<string, PowerPair>(StringComparer.OrdinalIgnoreCase)
                {
                    ["PROCTHROTTLEMIN"] = new PowerPair(100, 100),
                    ["PROCTHROTTLEMAX"] = new PowerPair(100, 100),
                    ["PERFINCPOL"] = new PowerPair(2, 2),
                    ["CPMINCORES"] = new PowerPair(100, 100),
                    ["CPMAXCORES"] = new PowerPair(100, 100)
                };
            }
            else
            {
                string sourceGuid = profile == PerformanceProfileKind.Balanced
                    ? BalancedGuid
                    : HighPerformanceGuid;
                Dictionary<string, PowerPair> values = new(
                    StringComparer.OrdinalIgnoreCase);
                foreach ((string alias, string settingGuid) in PowerSettings)
                {
                    values[alias] = ReadDefaultPowerPair(
                        sourceGuid,
                        settingGuid) ??
                        ReadCurrentPowerPair(targetPowerGuid, settingGuid);
                }
                desired = values;
            }

            foreach ((string alias, PowerPair pair) in desired)
            {
                await RequireSuccessAsync(
                    "powercfg.exe",
                    new[]
                    {
                        "/setacvalueindex",
                        targetPowerGuid,
                        "SUB_PROCESSOR",
                        alias,
                        pair.Ac.ToString(CultureInfo.InvariantCulture)
                    });
                await RequireSuccessAsync(
                    "powercfg.exe",
                    new[]
                    {
                        "/setdcvalueindex",
                        targetPowerGuid,
                        "SUB_PROCESSOR",
                        alias,
                        pair.Dc.ToString(CultureInfo.InvariantCulture)
                    });
            }
            await RequireSuccessAsync(
                "powercfg.exe",
                new[] { "/setactive", targetPowerGuid });
        }

        private async Task ApplyBcdPolicyAsync(PerformanceProfileKind profile)
        {
            if (profile == PerformanceProfileKind.CompetitiveGaming)
            {
                await RequireSuccessAsync(
                    "bcdedit.exe",
                    new[] { "/set", "{current}", "disabledynamictick", "yes" });
                await RequireSuccessAsync(
                    "bcdedit.exe",
                    new[] { "/set", "{current}", "useplatformclock", "no" });
            }
            else
            {
                await DeleteBcdValueAsync("disabledynamictick");
                await DeleteBcdValueAsync("useplatformclock");
            }
        }

        private async Task ApplyNetworkPolicyAsync(
            PerformanceProfileKind profile, IReadOnlyList<ProfileRscAdapter> originalAdapters)
        {
            bool gaming = profile is PerformanceProfileKind.CompetitiveGaming or
                PerformanceProfileKind.OptimizedGaming;
            using (RegistryKey? interfaces = OpenLocalMachineKey(
                       InterfaceRoot,
                       writable: false))
            {
                if (interfaces is not null)
                {
                    foreach (string interfaceName in interfaces.GetSubKeyNames())
                    {
                        string path = $@"{InterfaceRoot}\{interfaceName}";
                        if (gaming)
                        {
                            SetDword(path, "TCPNoDelay", 1);
                            SetDword(path, "TcpAckFrequency", 1);
                        }
                        else
                        {
                            DeleteRegistryValue(path, "TCPNoDelay");
                            DeleteRegistryValue(path, "TcpAckFrequency");
                        }
                    }
                }
            }

            if (gaming)
            {
                SetDword(TcpGlobalPath, "TcpNoDelay", 1);
                SetDword(MultimediaProfilePath, "NetworkThrottlingIndex", -1);
                SetDword(QosPath, "NonBestEffortLimit", 0);
                SetDword(QosPath, "DisableUserTOSSetting", 0);
            }
            else
            {
                DeleteRegistryValue(TcpGlobalPath, "TcpNoDelay");
                DeleteRegistryValue(MultimediaProfilePath, "NetworkThrottlingIndex");
                DeleteRegistryValue(QosPath, "NonBestEffortLimit");
                DeleteRegistryValue(QosPath, "DisableUserTOSSetting");
            }

            bool rscEnabled = profile != PerformanceProfileKind.CompetitiveGaming;
            ProfileRscAdapter[] targets = originalAdapters.Select(adapter =>
                new ProfileRscAdapter(adapter.Name, rscEnabled, rscEnabled)).ToArray();
            await Task.Run(() => NativeRscService.SetStates(targets));
        }

        private async Task VerifyAsync(
            PerformanceProfileKind profile,
            string targetPowerGuid)
        {
            IReadOnlyDictionary<string, PowerPair> expected;
            if (profile == PerformanceProfileKind.CompetitiveGaming)
            {
                expected = new Dictionary<string, PowerPair>(StringComparer.OrdinalIgnoreCase)
                {
                    ["PROCTHROTTLEMIN"] = new PowerPair(100, 100),
                    ["PROCTHROTTLEMAX"] = new PowerPair(100, 100),
                    ["PERFINCPOL"] = new PowerPair(2, 2),
                    ["CPMINCORES"] = new PowerPair(100, 100),
                    ["CPMAXCORES"] = new PowerPair(100, 100)
                };
            }
            else
            {
                string sourceGuid = profile == PerformanceProfileKind.Balanced
                    ? BalancedGuid
                    : HighPerformanceGuid;
                expected = PowerSettings.ToDictionary(
                    item => item.Key,
                    item => ReadDefaultPowerPair(sourceGuid, item.Value) ??
                            ReadCurrentPowerPair(targetPowerGuid, item.Value),
                    StringComparer.OrdinalIgnoreCase);
            }

            foreach ((string alias, string settingGuid) in PowerSettings)
            {
                PowerPair actual = ReadCurrentPowerPair(targetPowerGuid, settingGuid);
                if (actual != expected[alias])
                {
                    throw new InvalidOperationException(
                        $"CPU power verification failed for {alias}. Expected AC/DC " +
                        $"{expected[alias].Ac}/{expected[alias].Dc}, actual {actual.Ac}/{actual.Dc}.");
                }
            }

            string? dynamicTick = await ReadBcdValueAsync("disabledynamictick");
            string? hpet = await ReadBcdValueAsync("useplatformclock");
            if (profile == PerformanceProfileKind.CompetitiveGaming)
            {
                if (!string.Equals(dynamicTick, "Yes", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(hpet, "No", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "BCD verification failed for Competitive Gaming.");
                }
            }
            else if (dynamicTick is not null || hpet is not null)
            {
                throw new InvalidOperationException(
                    "BCD verification expected Windows defaults, but an override remains.");
            }

            bool gaming = profile is PerformanceProfileKind.CompetitiveGaming or
                PerformanceProfileKind.OptimizedGaming;
            bool registryMatches = gaming
                ? ReadDword(TcpGlobalPath, "TcpNoDelay") == 1 &&
                  unchecked((uint)(ReadDword(MultimediaProfilePath, "NetworkThrottlingIndex") ?? 0)) == uint.MaxValue &&
                  ReadDword(QosPath, "NonBestEffortLimit") == 0 &&
                  ReadDword(QosPath, "DisableUserTOSSetting") == 0
                : ReadDword(TcpGlobalPath, "TcpNoDelay") is null &&
                  ReadDword(MultimediaProfilePath, "NetworkThrottlingIndex") is null &&
                  ReadDword(QosPath, "NonBestEffortLimit") is null &&
                  ReadDword(QosPath, "DisableUserTOSSetting") is null;
            if (!registryMatches)
            {
                throw new InvalidOperationException(
                    "TCP/QoS registry verification failed.");
            }
        }

        private async Task<List<string>> RestoreSnapshotAsync(
            ExtendedSnapshot snapshot)
        {
            List<string> errors = new();
            foreach (RegistrySnapshot value in snapshot.Registry)
            {
                try
                {
                    RestoreRegistry(value);
                }
                catch (Exception exception)
                {
                    errors.Add($"Registry {value.Name}: {exception.Message}");
                }
            }
            foreach (PowerSettingSnapshot setting in snapshot.Power)
            {
                try
                {
                    await RequireSuccessAsync(
                        "powercfg.exe",
                        new[]
                        {
                            "/setacvalueindex",
                            snapshot.PowerGuid,
                            "SUB_PROCESSOR",
                            setting.Alias,
                            setting.Pair.Ac.ToString(CultureInfo.InvariantCulture)
                        });
                    await RequireSuccessAsync(
                        "powercfg.exe",
                        new[]
                        {
                            "/setdcvalueindex",
                            snapshot.PowerGuid,
                            "SUB_PROCESSOR",
                            setting.Alias,
                            setting.Pair.Dc.ToString(CultureInfo.InvariantCulture)
                        });
                }
                catch (Exception exception)
                {
                    errors.Add($"Power {setting.Alias}: {exception.Message}");
                }
            }
            try
            {
                await RestoreBcdValueAsync(
                    "disabledynamictick",
                    snapshot.DynamicTick);
                await RestoreBcdValueAsync(
                    "useplatformclock",
                    snapshot.Hpet);
            }
            catch (Exception exception)
            {
                errors.Add($"BCD: {exception.Message}");
            }
            try
            {
                await Task.Run(() => NativeRscService.SetStates(snapshot.Rsc));
            }
            catch (Exception exception)
            {
                errors.Add($"RSC: {exception.Message}");
            }
            return errors;
        }

        private static PowerPair? ReadDefaultPowerPair(
            string sourceGuid,
            string settingGuid)
        {
            string path =
                $@"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\{ProcessorSubgroup}\{settingGuid}\DefaultPowerSchemeValues\{sourceGuid}";
            using RegistryKey? key = OpenLocalMachineKey(path, writable: false);
            object? ac = key?.GetValue("ACSettingIndex");
            object? dc = key?.GetValue("DCSettingIndex");
            return ac is null || dc is null
                ? null
                : new PowerPair(
                    Convert.ToUInt32(ac, CultureInfo.InvariantCulture),
                    Convert.ToUInt32(dc, CultureInfo.InvariantCulture));
        }

        private static PowerPair ReadCurrentPowerPair(
            string powerGuid,
            string settingGuid)
        {
            PowerPolicyPair pair = NativePowerPolicyReader.ReadRequiredPair(powerGuid, settingGuid);
            return new PowerPair(pair.Ac, pair.Dc);
        }

        private async Task<string?> ReadBcdValueAsync(string option)
        {
            NativeCommandResult result = await _commandRunner.RunAsync(
                "bcdedit.exe",
                new[] { "/enum", "{current}" },
                TimeSpan.FromSeconds(15));
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(result.CombinedOutput);
            }
            foreach (string line in result.StandardOutput.Split(
                         new[] { '\r', '\n' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith(option, StringComparison.OrdinalIgnoreCase))
                {
                    string value = trimmed[option.Length..].Trim();
                    return string.IsNullOrWhiteSpace(value) ? null : value;
                }
            }
            return null;
        }

        private async Task RestoreBcdValueAsync(string option, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                await DeleteBcdValueAsync(option);
            }
            else
            {
                await RequireSuccessAsync(
                    "bcdedit.exe",
                    new[] { "/set", "{current}", option, value });
            }
        }

        private async Task DeleteBcdValueAsync(string option)
        {
            NativeCommandResult result = await _commandRunner.RunAsync(
                "bcdedit.exe",
                new[] { "/deletevalue", "{current}", option },
                TimeSpan.FromSeconds(15));
            if (result.ExitCode != 0 &&
                !string.IsNullOrWhiteSpace(await ReadBcdValueAsync(option)))
            {
                throw new InvalidOperationException(result.CombinedOutput);
            }
        }

        private async Task RequireSuccessAsync(
            string executable,
            IReadOnlyList<string> arguments)
        {
            NativeCommandResult result = await _commandRunner.RunAsync(
                executable,
                arguments,
                TimeSpan.FromSeconds(30));
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(result.CombinedOutput)
                        ? $"{executable} failed with exit code {result.ExitCode}."
                        : result.CombinedOutput);
            }
        }

        private static RegistrySnapshot CaptureRegistry(string path, string name)
        {
            using RegistryKey? key = OpenLocalMachineKey(path, writable: false);
            object? value = key?.GetValue(
                name,
                null,
                RegistryValueOptions.DoNotExpandEnvironmentNames);
            return value is null || key is null
                ? new RegistrySnapshot(path, name, false, null, RegistryValueKind.None)
                : new RegistrySnapshot(path, name, true, value, key.GetValueKind(name));
        }

        private static void RestoreRegistry(RegistrySnapshot snapshot)
        {
            if (!snapshot.Existed)
            {
                DeleteRegistryValue(snapshot.Path, snapshot.Name);
                return;
            }
            using RegistryKey key = CreateLocalMachineKey(snapshot.Path);
            key.SetValue(snapshot.Name, snapshot.Value!, snapshot.Kind);
        }

        private static int? ReadDword(string path, string name)
        {
            using RegistryKey? key = OpenLocalMachineKey(path, writable: false);
            object? value = key?.GetValue(name);
            return value is null
                ? null
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static void SetDword(string path, string name, int value)
        {
            using RegistryKey key = CreateLocalMachineKey(path);
            key.SetValue(name, value, RegistryValueKind.DWord);
        }

        private static void DeleteRegistryValue(string path, string name)
        {
            using RegistryKey? key = OpenLocalMachineKey(path, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }

        private static RegistryKey? OpenLocalMachineKey(
            string path,
            bool writable)
        {
            RegistryView view = Environment.Is64BitOperatingSystem
                ? RegistryView.Registry64
                : RegistryView.Default;
            RegistryKey baseKey = RegistryKey.OpenBaseKey(
                RegistryHive.LocalMachine,
                view);
            RegistryKey? key = baseKey.OpenSubKey(path, writable);
            baseKey.Dispose();
            return key;
        }

        private static RegistryKey CreateLocalMachineKey(string path)
        {
            RegistryView view = Environment.Is64BitOperatingSystem
                ? RegistryView.Registry64
                : RegistryView.Default;
            RegistryKey baseKey = RegistryKey.OpenBaseKey(
                RegistryHive.LocalMachine,
                view);
            RegistryKey key = baseKey.CreateSubKey(path, writable: true);
            baseKey.Dispose();
            return key;
        }

        private readonly record struct PowerPair(uint Ac, uint Dc);

        private readonly record struct PowerSettingSnapshot(
            string Alias,
            PowerPair Pair);

        private readonly record struct RegistrySnapshot(
            string Path,
            string Name,
            bool Existed,
            object? Value,
            RegistryValueKind Kind);

        private readonly record struct ExtendedSnapshot(
            IReadOnlyList<RegistrySnapshot> Registry,
            IReadOnlyList<PowerSettingSnapshot> Power,
            string? DynamicTick,
            string? Hpet,
            IReadOnlyList<ProfileRscAdapter> Rsc,
            string PowerGuid);
    }
}
