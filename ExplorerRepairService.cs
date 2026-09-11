using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal sealed class ExplorerRepairService
    {
        private readonly NativeCommandRunner _commandRunner = new();

        public async Task<MaintenanceOperationResult> RunAsync(
            IProgress<MaintenanceProgressUpdate>? progress = null,
            CancellationToken cancellationToken = default)
        {
            StringBuilder report = new();
            List<string> warnings = new();
            MaintenanceStageTracker stages = new(progress, () => warnings.Count);
            progress = stages;
            ExplorerLauncher? launcher = null;
            bool explorerWasStopped = false;
            bool success = false;
            string? primaryError = null;

            report.AppendLine("EXPLORER FIX");
            report.AppendLine($"Started: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
            report.AppendLine(new string('=', 74));

            try
            {
                AddStage(report, progress, 1, 7, "Prepare Explorer recovery launcher");
                launcher = await CreateInteractiveLauncherAsync(report, cancellationToken);

                AddStage(report, progress, 2, 7, "Stop Windows Explorer");
                NativeCommandResult stopResult = await _commandRunner.RunAsync(
                    GetSystemTool("taskkill.exe"),
                    new[] { "/F", "/IM", "explorer.exe" },
                    TimeSpan.FromSeconds(10),
                    cancellationToken);
                explorerWasStopped = true;
                report.AppendLine($"Explorer termination request: exit {stopResult.ExitCode}");
                await WaitForExplorerExitAsync(TimeSpan.FromSeconds(2), cancellationToken);

                AddStage(report, progress, 3, 7, "Rebuild Explorer icon and thumbnail cache");
                int deleted = ClearExplorerCaches(warnings);
                report.AppendLine($"Removed {deleted} Explorer cache file(s).");

                AddStage(report, progress, 4, 7, "Start Windows Explorer");
                ShellState state = await EnsureExplorerStartedAsync(
                    launcher.Value,
                    attempts: 2,
                    timeoutPerAttempt: TimeSpan.FromSeconds(12),
                    report,
                    cancellationToken);
                if (state.Ready)
                {
                    report.AppendLine($"Explorer shell started. Taskbar owner PID: {state.TaskbarPid}");
                }
                else
                {
                    warnings.Add("Explorer launched, but the taskbar was not ready after the initial attempts.");
                }

                AddStage(report, progress, 5, 7, "Verify Windows taskbar");
                state = GetShellState();
                if (state.Ready)
                {
                    report.AppendLine($"Verified Shell_TrayWnd owned by explorer.exe PID {state.TaskbarPid}.");
                    report.AppendLine($"Taskbar visible: {state.TaskbarVisible}");
                }
                else
                {
                    warnings.Add("Primary taskbar verification was incomplete; shell packages will be re-registered.");
                }

                AddStage(report, progress, 6, 7, "Repair Windows shell registration");
                if (!state.Ready)
                {
                    await RegisterShellPackagesAsync(report, warnings);
                    state = await EnsureExplorerStartedAsync(
                        launcher.Value,
                        attempts: 2,
                        timeoutPerAttempt: TimeSpan.FromSeconds(15),
                        report,
                        cancellationToken);
                }
                else
                {
                    report.AppendLine("Skipped: taskbar was already restored.");
                    stages.Skip("Taskbar was already restored; shell registration was not needed.");
                }

                AddStage(report, progress, 7, 7, "Final taskbar verification");
                state = await WaitForTaskbarAsync(TimeSpan.FromSeconds(5), cancellationToken);
                if (!state.Ready)
                {
                    throw new InvalidOperationException(
                        $"Shell_TrayWnd is not owned by explorer.exe. Explorer PID(s): " +
                        $"{(state.ExplorerPids.Count == 0 ? "none" : string.Join(", ", state.ExplorerPids))}.");
                }

                report.AppendLine($"Verified primary Windows taskbar: explorer.exe PID {state.TaskbarPid}");
                await RequestIconRefreshAsync(report, warnings, cancellationToken);
                success = true;
            }
            catch (Exception exception)
            {
                primaryError = exception.Message;
                report.AppendLine($"ERROR: {primaryError}");
            }
            finally
            {
                if (explorerWasStopped && launcher.HasValue)
                {
                    ShellState rescue = GetShellState();
                    if (!rescue.Ready)
                    {
                        report.AppendLine("FAIL-SAFE: attempting Explorer shell recovery...");
                        try
                        {
                            rescue = await EnsureExplorerStartedAsync(
                                launcher.Value,
                                attempts: 3,
                                timeoutPerAttempt: TimeSpan.FromSeconds(12),
                                report,
                                cancellationToken);
                        }
                        catch (Exception exception)
                        {
                            report.AppendLine($"Scheduled rescue warning: {exception.Message}");
                        }

                        if (!rescue.Ready)
                        {
                            TryDirectExplorerRescue(report);
                            rescue = await WaitForTaskbarAsync(
                                TimeSpan.FromSeconds(10),
                                CancellationToken.None);
                        }

                        if (rescue.Ready)
                        {
                            report.AppendLine($"FAIL-SAFE taskbar recovered: explorer.exe PID {rescue.TaskbarPid}");
                        }
                        else
                        {
                            string rescueError = "Emergency recovery could not verify Shell_TrayWnd.";
                            primaryError = string.IsNullOrWhiteSpace(primaryError)
                                ? rescueError
                                : $"{primaryError} {rescueError}";
                            success = false;
                        }
                    }
                }

                if (launcher.HasValue)
                {
                    await DeleteLauncherAsync(launcher.Value, report);
                }
            }

            AppendFinal(report, success, warnings, primaryError);
            stages.Complete(success);
            return new MaintenanceOperationResult(success, warnings.Count, report.ToString().TrimEnd());
        }

        private async Task<ExplorerLauncher> CreateInteractiveLauncherAsync(
            StringBuilder report,
            CancellationToken cancellationToken)
        {
            string explorer = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "explorer.exe");
            string cmd = GetSystemTool("cmd.exe");
            if (!File.Exists(explorer) || !File.Exists(cmd))
            {
                throw new FileNotFoundException("Required Windows Explorer recovery tools were not found.");
            }

            string userName;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                userName = identity.Name;
            }
            if (string.IsNullOrWhiteSpace(userName))
            {
                throw new InvalidOperationException("Could not determine the interactive Windows user.");
            }

            string taskName = $"NaufalTech_WPT_ExplorerRestore_{Environment.ProcessId}";
            string taskCommand = $"\"{cmd}\" /d /c start \"\" \"{explorer}\"";
            string startTime = DateTime.Now.AddMinutes(5).ToString("HH:mm", CultureInfo.InvariantCulture);
            string schtasks = GetSystemTool("schtasks.exe");

            await _commandRunner.RunAsync(
                schtasks,
                new[] { "/Delete", "/TN", taskName, "/F" },
                TimeSpan.FromSeconds(10),
                cancellationToken);

            NativeCommandResult create = await _commandRunner.RunAsync(
                schtasks,
                new[]
                {
                    "/Create", "/TN", taskName,
                    "/TR", taskCommand,
                    "/SC", "ONCE", "/ST", startTime,
                    "/RU", userName,
                    "/RL", "LIMITED", "/IT", "/F"
                },
                TimeSpan.FromSeconds(20),
                cancellationToken);
            if (create.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "Could not prepare the non-elevated Explorer recovery launcher. " +
                    $"Explorer was not stopped. {create.CombinedOutput}");
            }

            NativeCommandResult query = await _commandRunner.RunAsync(
                schtasks,
                new[] { "/Query", "/TN", taskName },
                TimeSpan.FromSeconds(10),
                cancellationToken);
            if (query.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "Explorer recovery launcher could not be read back. Explorer was not stopped.");
            }

            report.AppendLine($"Interactive user: {userName}");
            report.AppendLine($"Temporary launcher: {taskName}");
            report.AppendLine("Launcher security context: interactive user / limited token");
            return new ExplorerLauncher(taskName, userName);
        }

        private async Task<ShellState> EnsureExplorerStartedAsync(
            ExplorerLauncher launcher,
            int attempts,
            TimeSpan timeoutPerAttempt,
            StringBuilder report,
            CancellationToken cancellationToken)
        {
            ShellState state = GetShellState();
            if (state.Ready)
            {
                return state;
            }

            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                NativeCommandResult run = await _commandRunner.RunAsync(
                    GetSystemTool("schtasks.exe"),
                    new[] { "/Run", "/TN", launcher.TaskName },
                    TimeSpan.FromSeconds(15),
                    cancellationToken);
                report.AppendLine($"Explorer launcher attempt {attempt}/{attempts}: exit {run.ExitCode}");
                state = await WaitForTaskbarAsync(timeoutPerAttempt, cancellationToken);
                if (state.Ready)
                {
                    return state;
                }
            }

            return state;
        }

        private static int ClearExplorerCaches(List<string> warnings)
        {
            DateTimeOffset deadline = DateTimeOffset.Now.AddSeconds(4);
            HashSet<string> targets = new(StringComparer.OrdinalIgnoreCase);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string legacy = Path.Combine(localAppData, "IconCache.db");
            if (File.Exists(legacy))
            {
                targets.Add(legacy);
            }

            string cacheRoot = Path.Combine(localAppData, "Microsoft", "Windows", "Explorer");
            if (Directory.Exists(cacheRoot))
            {
                foreach (string pattern in new[] { "thumbcache_*.db", "iconcache_*.db" })
                {
                    if (DateTimeOffset.Now >= deadline)
                    {
                        break;
                    }
                    try
                    {
                        foreach (string path in Directory.GetFiles(cacheRoot, pattern, SearchOption.TopDirectoryOnly))
                        {
                            targets.Add(path);
                        }
                    }
                    catch (Exception exception)
                    {
                        warnings.Add($"Could not enumerate Explorer cache files: {exception.Message}");
                    }
                }
            }

            int deleted = 0;
            foreach (string target in targets)
            {
                if (DateTimeOffset.Now >= deadline)
                {
                    warnings.Add("Explorer cache cleanup reached its four-second safety limit.");
                    break;
                }
                try
                {
                    File.Delete(target);
                    if (!File.Exists(target))
                    {
                        deleted++;
                    }
                }
                catch (Exception exception)
                {
                    warnings.Add($"Could not remove {Path.GetFileName(target)}: {exception.Message}");
                }
            }

            return deleted;
        }

        private static async Task RegisterShellPackagesAsync(
            StringBuilder report,
            List<string> warnings)
        {
            PackageManager manager = new();
            foreach (string packageName in new[]
                     {
                         "Microsoft.Windows.ShellExperienceHost",
                         "Microsoft.Windows.StartMenuExperienceHost",
                         "MicrosoftWindows.Client.CBS"
                     })
            {
                List<Package> packages = manager.FindPackagesForUser(string.Empty)
                    .Where(package => string.Equals(
                        package.Id.Name,
                        packageName,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (packages.Count == 0)
                {
                    report.AppendLine($"Not present: {packageName}");
                    continue;
                }

                foreach (Package package in packages)
                {
                    string manifest = Path.Combine(package.InstalledLocation.Path, "AppXManifest.xml");
                    if (!File.Exists(manifest))
                    {
                        warnings.Add($"Manifest missing: {packageName}");
                        continue;
                    }
                    try
                    {
                        DeploymentResult result = await DeploymentOperationTimeout.AwaitAsync(
                            () => manager.RegisterPackageAsync(
                                new Uri(manifest),
                                null,
                                DeploymentOptions.None),
                            $"Registering {packageName}");
                        if (result.ExtendedErrorCode is not null && result.ExtendedErrorCode.HResult < 0)
                        {
                            warnings.Add($"Registration warning {packageName}: {result.ErrorText}");
                        }
                        else
                        {
                            report.AppendLine($"Registered: {packageName}");
                        }
                    }
                    catch (Exception exception)
                    {
                        warnings.Add($"Registration warning {packageName}: {exception.Message}");
                    }
                }
            }
        }

        private async Task RequestIconRefreshAsync(
            StringBuilder report,
            List<string> warnings,
            CancellationToken cancellationToken)
        {
            string ie4uinit = GetSystemTool("ie4uinit.exe");
            if (!File.Exists(ie4uinit))
            {
                return;
            }
            NativeCommandResult result = await _commandRunner.RunAsync(
                ie4uinit,
                new[] { "-show" },
                TimeSpan.FromSeconds(4),
                cancellationToken);
            if (result.ExitCode == 0)
            {
                report.AppendLine("Requested final Explorer icon refresh.");
            }
            else
            {
                warnings.Add($"Final icon refresh returned exit code {result.ExitCode}.");
            }
        }

        private async Task DeleteLauncherAsync(ExplorerLauncher launcher, StringBuilder report)
        {
            try
            {
                NativeCommandResult result = await _commandRunner.RunAsync(
                    GetSystemTool("schtasks.exe"),
                    new[] { "/Delete", "/TN", launcher.TaskName, "/F" },
                    TimeSpan.FromSeconds(10));
                report.AppendLine($"Temporary Explorer launcher cleanup: exit {result.ExitCode}");
            }
            catch
            {
            }
        }

        private static async Task WaitForExplorerExitAsync(
            TimeSpan timeoutValue,
            CancellationToken cancellationToken)
        {
            DateTimeOffset deadline = DateTimeOffset.Now.Add(timeoutValue);
            while (DateTimeOffset.Now < deadline)
            {
                Process[] processes = Process.GetProcessesByName("explorer");
                bool explorerExited = processes.Length == 0;
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
                if (explorerExited)
                {
                    return;
                }
                await Task.Delay(100, cancellationToken);
            }
        }

        private static async Task<ShellState> WaitForTaskbarAsync(
            TimeSpan timeoutValue,
            CancellationToken cancellationToken)
        {
            DateTimeOffset deadline = DateTimeOffset.Now.Add(timeoutValue);
            ShellState state = GetShellState();
            while (!state.Ready && DateTimeOffset.Now < deadline)
            {
                await Task.Delay(200, cancellationToken);
                state = GetShellState();
            }
            return state;
        }

        private static ShellState GetShellState()
        {
            List<int> explorerPids = new();
            foreach (Process process in Process.GetProcessesByName("explorer"))
            {
                try { explorerPids.Add(process.Id); }
                finally { process.Dispose(); }
            }

            IntPtr handle = FindWindow("Shell_TrayWnd", null);
            uint processId = 0;
            string? owner = null;
            bool visible = false;
            if (handle != IntPtr.Zero && IsWindow(handle))
            {
                GetWindowThreadProcessId(handle, out processId);
                visible = IsWindowVisible(handle);
                if (processId > 0)
                {
                    try
                    {
                        using Process process = Process.GetProcessById((int)processId);
                        owner = process.ProcessName;
                    }
                    catch
                    {
                    }
                }
            }

            bool ready = handle != IntPtr.Zero &&
                         processId > 0 &&
                         string.Equals(owner, "explorer", StringComparison.OrdinalIgnoreCase);
            return new ShellState(ready, explorerPids, (int)processId, owner ?? string.Empty, visible);
        }

        private static void TryDirectExplorerRescue(StringBuilder report)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                        "explorer.exe"),
                    UseShellExecute = true
                });
                report.AppendLine("Requested direct Explorer fail-safe launch.");
            }
            catch (Exception exception)
            {
                report.AppendLine($"Direct Explorer fail-safe failed: {exception.Message}");
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

        private static void AppendFinal(
            StringBuilder report,
            bool success,
            IReadOnlyCollection<string> warnings,
            string? error)
        {
            report.AppendLine();
            report.AppendLine(new string('=', 74));
            report.AppendLine(success ? "EXPLORER FIX COMPLETED" : "EXPLORER FIX FAILED");
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

        private readonly record struct ExplorerLauncher(string TaskName, string UserName);
        private readonly record struct ShellState(
            bool Ready,
            IReadOnlyList<int> ExplorerPids,
            int TaskbarPid,
            string TaskbarOwner,
            bool TaskbarVisible);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);
    }
}
