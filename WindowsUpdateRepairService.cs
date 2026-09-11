using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal readonly record struct MaintenanceOperationResult(
        bool Success,
        int WarningCount,
        string Report);

    internal sealed partial class WindowsUpdateRepairService
    {
        private static readonly string[] RepairServices = { "BITS", "wuauserv", "cryptsvc", "DoSvc" };

        private readonly NativeCommandRunner _commandRunner = new();

        public async Task<MaintenanceOperationResult> RunAsync(
            IProgress<MaintenanceProgressUpdate>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!WindowsPrivilegeService.IsAdministrator())
            {
                return new MaintenanceOperationResult(
                    false,
                    0,
                    "Administrator rights are required. Close the application and start it with Run as administrator.");
            }

            StringBuilder report = new();
            List<string> warnings = new();
            MaintenanceStageTracker stages = new(progress, () => warnings.Count);
            progress = stages;
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            List<UpdateCacheBackup> cacheBackups = new();
            bool restartUsoSvc = false;

            report.AppendLine("WINDOWS UPDATE FIX");
            report.AppendLine($"Started: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
            report.AppendLine(new string('=', 74));

            try
            {
                AddStage(report, progress, 1, 6, "Repair update-service prerequisites");
                foreach (string serviceName in RepairServices)
                {
                    await RestoreServiceConfigurationAsync(
                        serviceName,
                        report,
                        warnings,
                        cancellationToken);
                }

                AddStage(report, progress, 2, 6, "Stop Windows Update services");
                string usoState = await QueryServiceStateAsync("UsoSvc", cancellationToken);
                if (usoState == "Unknown") throw new InvalidOperationException("Cannot read UsoSvc state before pausing update coordination.");
                restartUsoSvc = usoState is not ("Stopped" or "NotFound");
                await EnsureServicesStoppedAsync(report, warnings, cancellationToken);

                AddStage(report, progress, 3, 6, "Reset SoftwareDistribution cache");
                foreach (string cache in new[] { @"SoftwareDistribution\DataStore", @"SoftwareDistribution\Download" })
                    cacheBackups.Add(await UpdateCacheReset.RunAsync(windows, cache,
                        token => EnsureServicesStoppedAsync(report, warnings, token),
                        line => report.AppendLine(line), cancellationToken));

                AddStage(report, progress, 4, 6, "Reset catroot2 cache");
                cacheBackups.Add(await UpdateCacheReset.RunAsync(windows, @"System32\catroot2",
                    token => EnsureServicesStoppedAsync(report, warnings, token),
                    line => report.AppendLine(line), cancellationToken));

                AddStage(report, progress, 5, 6, "Restart Windows Update services");
                foreach (string serviceName in new[] { "cryptsvc", "bits", "wuauserv", "DoSvc" })
                {
                    await SetServiceStateAsync(
                        serviceName,
                        running: true,
                        report,
                        warnings,
                        cancellationToken);
                }
                if (restartUsoSvc)
                    await SetServiceStateAsync("UsoSvc", true, report, warnings, cancellationToken);

                AddStage(report, progress, 6, 6, "Verify Windows Update repair");
                foreach (string serviceName in RepairServices)
                {
                    int? actualStart = ReadServiceStartValue(serviceName);
                    string runtime = await QueryServiceStateAsync(serviceName, cancellationToken);
                    report.AppendLine(
                        $"VERIFY {serviceName}: Start={(actualStart?.ToString(CultureInfo.InvariantCulture) ?? "MISSING")} " +
                        $"required={UpdateServiceStartupPolicy.RequiredStart(serviceName)}; Runtime={runtime}");
                    foreach (string issue in UpdateServiceStartupPolicy.Verify(serviceName, actualStart, runtime))
                    {
                        warnings.Add(issue);
                        report.AppendLine("WARNING: " + issue);
                    }
                }
                if (restartUsoSvc)
                {
                    string usoRuntime = await QueryServiceStateAsync("UsoSvc", cancellationToken);
                    report.AppendLine("VERIFY UsoSvc: Runtime=" + usoRuntime);
                    if (usoRuntime != "Running")
                    {
                        string issue = $"UsoSvc runtime state is {usoRuntime}, expected Running.";
                        warnings.Add(issue);
                        report.AppendLine("WARNING: " + issue);
                    }
                }

                foreach (UpdateCacheBackup cache in cacheBackups)
                {
                    if (cache.Backup is null) continue;
                    if (!Directory.Exists(cache.Backup))
                        throw new IOException("Cache backup is missing after reset: " + cache.Backup);
                    report.AppendLine("Verified preserved backup: " + cache.Backup);
                }

                AppendFinalResult(report, true, warnings, null);
                stages.Complete(true);
                return new MaintenanceOperationResult(true, warnings.Count, report.ToString().TrimEnd());
            }
            catch (Exception exception)
            {
                report.AppendLine($"ERROR: {exception.Message}");
                // Cleanup must run even if the caller cancelled the repair.
                await RecoverServicesAsync(report, warnings, restartUsoSvc);
                AppendFinalResult(report, false, warnings, exception.Message);
                stages.Complete(false);
                return new MaintenanceOperationResult(false, warnings.Count, report.ToString().TrimEnd());
            }
        }

        private async Task RestoreServiceConfigurationAsync(
            string serviceName,
            StringBuilder report,
            List<string> warnings,
            CancellationToken cancellationToken)
        {
            await RemoveLegacyServiceLockAsync(serviceName, report, cancellationToken);
            int? before = ReadServiceStartValue(serviceName);
            if (before is null)
            {
                warnings.Add($"Cannot read {serviceName} startup configuration; the service may be missing or access was denied.");
                report.AppendLine($"WARNING: {warnings[^1]}");
                return;
            }

            if (UpdateServiceStartupPolicy.IsOperationalStart(serviceName, before))
            {
                report.AppendLine($"{serviceName}: Preserved operational Start={before}; required {UpdateServiceStartupPolicy.RequiredStart(serviceName)}. Runtime will be started and verified separately.");
                return;
            }

            int startValue = UpdateServiceStartupPolicy.RepairStart(serviceName);

            string sc = GetSystemTool("sc.exe");
            string startMode = startValue == 2 ? "auto" : "demand";
            NativeCommandResult result = await _commandRunner.RunAsync(
                sc,
                new[] { "config", serviceName, "start=", startMode },
                TimeSpan.FromSeconds(20),
                cancellationToken);

            int? after = ReadServiceStartValue(serviceName);
            if (!UpdateServiceStartupPolicy.IsOperationalStart(serviceName, after))
            {
                try
                {
                    using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                        $@"SYSTEM\CurrentControlSet\Services\{serviceName}",
                        writable: true);
                    key?.SetValue("Start", startValue, RegistryValueKind.DWord);
                }
                catch (Exception exception)
                {
                    warnings.Add($"{serviceName}: {exception.Message}");
                }
                after = ReadServiceStartValue(serviceName);
            }

            if (UpdateServiceStartupPolicy.IsOperationalStart(serviceName, after))
            {
                report.AppendLine($"{serviceName}: Repaired Start {before} -> {after}; required {UpdateServiceStartupPolicy.RequiredStart(serviceName)}.");
            }
            else
            {
                string warning = $"Unable to restore {serviceName} Start={startValue}; sc.exe exit {result.ExitCode}.";
                warnings.Add(warning);
                report.AppendLine($"WARNING: {warning}");
            }
        }

        private async Task<bool> SetServiceStateAsync(
            string serviceName,
            bool running,
            StringBuilder report,
            List<string> warnings,
            CancellationToken cancellationToken)
        {
            string current = await QueryServiceStateAsync(serviceName, cancellationToken);
            string target = running ? "Running" : "Stopped";
            if (current == "NotFound" && serviceName is "DoSvc" or "UsoSvc")
            {
                report.AppendLine("Optional service not present: " + serviceName);
                return true;
            }
            if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase))
            {
                report.AppendLine($"Service already {target.ToLowerInvariant()}: {serviceName}");
                return true;
            }

            string sc = GetSystemTool("sc.exe");
            NativeCommandResult result = await _commandRunner.RunAsync(
                sc,
                new[] { running ? "start" : "stop", serviceName },
                TimeSpan.FromSeconds(20),
                cancellationToken);

            Stopwatch wait = Stopwatch.StartNew();
            do
            {
                await Task.Delay(250, cancellationToken);
                current = await QueryServiceStateAsync(serviceName, cancellationToken);
                if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase))
                {
                    report.AppendLine($"{target} service: {serviceName}");
                    return true;
                }
            }
            while (wait.Elapsed < TimeSpan.FromSeconds(30));

            string warning = $"Service {serviceName} did not reach {target} (sc.exe exit {result.ExitCode}; current: {current}).";
            warnings.Add(warning);
            report.AppendLine($"WARNING: {warning}");
            return false;
        }

        private async Task EnsureServicesStoppedAsync(StringBuilder report, List<string> warnings, CancellationToken token)
        {
            string[] services = { "UsoSvc", "DoSvc", "bits", "cryptsvc", "wuauserv" };
            foreach (string name in services)
                if (!await SetServiceStateAsync(name, false, report, warnings, token))
                    throw new InvalidOperationException($"Cache reset was not started: {name} could not be stopped.");
            // A service can restart while a later service is being stopped.
            // Never rename a cache based only on an earlier sc.exe exit code.
            foreach (string name in services)
            {
                string state = await QueryServiceStateAsync(name, token);
                if (state != "Stopped" && !(state == "NotFound" && name is "DoSvc" or "UsoSvc"))
                    throw new InvalidOperationException($"Cache reset was not started: {name} changed to {state}. Let active updates finish, then retry.");
            }
        }

        private async Task<string> QueryServiceStateAsync(
            string serviceName,
            CancellationToken cancellationToken)
        {
            if (GamingLiveStatusService.TryReadServiceState(serviceName, out string nativeState)) return nativeState;
            NativeCommandResult result = await _commandRunner.RunAsync(
                GetSystemTool("sc.exe"),
                new[] { "query", serviceName },
                TimeSpan.FromSeconds(10),
                cancellationToken);
            Match match = ServiceStatePattern().Match(result.CombinedOutput);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int state))
            {
                return result.ExitCode == 1060 ? "NotFound" : "Unknown";
            }

            return state switch
            {
                1 => "Stopped",
                2 => "StartPending",
                3 => "StopPending",
                4 => "Running",
                5 => "ContinuePending",
                6 => "PausePending",
                7 => "Paused",
                _ => $"State{state}"
            };
        }

        private async Task RecoverServicesAsync(
            StringBuilder report,
            List<string> warnings,
            bool restartUsoSvc)
        {
            report.AppendLine("Recovering Windows Update services and verifying runtime states...");
            List<string> services = new() { "cryptsvc", "bits", "wuauserv", "DoSvc" };
            if (restartUsoSvc) services.Add("UsoSvc");
            foreach (string serviceName in services)
            {
                try
                {
                    await SetServiceStateAsync(serviceName, true, report, warnings, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    string warning = $"Recovery {serviceName}: {exception.Message}";
                    warnings.Add(warning);
                    report.AppendLine("WARNING: " + warning);
                }
            }
        }

        private async Task RemoveLegacyServiceLockAsync(
            string serviceName,
            StringBuilder report,
            CancellationToken cancellationToken)
        {
            string safeName = Regex.Replace(serviceName, "[^A-Za-z0-9_.-]", "_");
            string taskName = $"WPT78_ServiceLock_{safeName}";
            try
            {
                await _commandRunner.RunAsync(
                    GetSystemTool("schtasks.exe"),
                    new[] { "/delete", "/tn", taskName, "/f" },
                    TimeSpan.FromSeconds(10),
                    cancellationToken);
            }
            catch
            {
            }

            string lockFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NaufalWindowsPowertoys",
                $"service-lock-{safeName}.cmd");
            try
            {
                if (File.Exists(lockFile))
                {
                    File.Delete(lockFile);
                    report.AppendLine($"Removed legacy service lock file: {Path.GetFileName(lockFile)}");
                }
            }
            catch (Exception exception)
            {
                report.AppendLine($"WARNING: Could not remove {Path.GetFileName(lockFile)}: {exception.Message}");
            }
        }

        private static int? ReadServiceStartValue(string serviceName)
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\{serviceName}",
                    writable: false);
                object? value = key?.GetValue("Start");
                return value is null
                    ? null
                    : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        private static string GetSystemTool(string fileName)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32",
                fileName);
        }

        private static void AddStage(
            StringBuilder report,
            IProgress<MaintenanceProgressUpdate>? progress,
            int index,
            int count,
            string name)
        {
            MaintenanceProgress.StartStage(progress, index, count, name);
            report.AppendLine();
            report.AppendLine($"[{index}/{count}] {name}");
            report.AppendLine(new string('-', 74));
        }

        private static void AppendFinalResult(
            StringBuilder report,
            bool success,
            IReadOnlyCollection<string> warnings,
            string? error)
        {
            report.AppendLine();
            report.AppendLine(new string('=', 74));
            report.AppendLine(success ? "REPAIR COMPLETED" : "REPAIR FAILED");
            report.AppendLine($"Warnings: {warnings.Count}");
            foreach (string warning in warnings)
            {
                report.AppendLine($"- {warning}");
            }
            if (!string.IsNullOrWhiteSpace(error))
            {
                report.AppendLine($"Error: {error}");
            }
        }

        [GeneratedRegex(@"(?im)STATE\s*:\s*(\d+)")]
        private static partial Regex ServiceStatePattern();
    }
}
