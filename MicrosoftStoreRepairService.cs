using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal sealed class MicrosoftStoreRepairService
    {
        private readonly NativeCommandRunner _commandRunner = new();

        public async Task<MaintenanceOperationResult> RunAsync(
            IProgress<MaintenanceProgressUpdate>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!WindowsPrivilegeService.IsAdministrator())
                return new(false, 0, "Administrator rights are required. Start the application with Run as administrator.");
            StringBuilder report = new();
            List<string> warnings = new();
            MaintenanceStageTracker stages = new(progress, () => warnings.Count);
            progress = stages;
            PackageManager packageManager = new();

            report.AppendLine("MICROSOFT STORE FIX");
            report.AppendLine($"Started: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
            report.AppendLine(new string('=', 74));

            try
            {
                AddStage(report, progress, 1, 9, "Close Microsoft Store processes");
                foreach (string processName in new[] { "WinStore.App", "StoreExperienceHost", "MicrosoftStore" })
                {
                    foreach (Process process in Process.GetProcessesByName(processName))
                    {
                        try
                        {
                            process.Kill(entireProcessTree: true);
                            await process.WaitForExitAsync(cancellationToken);
                            report.AppendLine($"Closed process: {processName}");
                        }
                        catch (Exception exception)
                        {
                            warnings.Add($"Close {processName}: {exception.Message}");
                        }
                        finally
                        {
                            process.Dispose();
                        }
                    }
                }

                AddStage(report, progress, 2, 9, "Detect Microsoft Store package");
                List<Package> storePackages = FindPackages(packageManager, "Microsoft.WindowsStore", allUsers: true);
                if (storePackages.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Microsoft Store package is not installed on this Windows installation.");
                }
                report.AppendLine($"Detected {storePackages.Count} Microsoft Store package(s).");

                AddStage(report, progress, 3, 9, "Reset Microsoft Store app data");
                Package? currentStore = FindPackages(packageManager, "Microsoft.WindowsStore").FirstOrDefault();
                string resetWarning = await StoreResetStage.RunAsync(currentStore?.Id.FullName, async package =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string script = StoreResetCommand.BuildScript(package);
                    NativeCommandResult reset = await DeploymentOperationTimeout.AwaitExternalAsync(
                        () => _commandRunner.RunAsync(GetSystemTool(@"WindowsPowerShell\v1.0\powershell.exe"),
                            new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand",
                                Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) },
                            Timeout.InfiniteTimeSpan, outputEncoding: Encoding.UTF8),
                        "Resetting Microsoft Store app data");
                    StoreResetCommand.ValidateResult(reset);
                });
                if (resetWarning.Length > 0)
                {
                    warnings.Add(resetWarning);
                    report.AppendLine("WARNING: " + resetWarning);
                }
                else report.AppendLine("Reset-AppxPackage command completed. Registration will be restored in stage 5 and verified in stage 9.");

                AddStage(report, progress, 4, 9, "Clear Microsoft Store cache");
                string storeUserRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Packages",
                    "Microsoft.WindowsStore_8wekyb3d8bbwe");
                foreach (string cacheName in new[] { "LocalCache", "TempState" })
                {
                    ClearDirectoryContents(
                        Path.Combine(storeUserRoot, cacheName),
                        cacheName,
                        report,
                        warnings);
                }

                AddStage(report, progress, 5, 9, "Re-register Microsoft Store packages");
                int registered = 0;
                foreach (string packageName in new[] { "Microsoft.WindowsStore", "Microsoft.StorePurchaseApp" })
                {
                    foreach (Package package in FindPackages(packageManager, packageName, allUsers: true))
                    {
                        string manifestPath = Path.Combine(
                            package.InstalledLocation.Path,
                            "AppXManifest.xml");
                        if (!File.Exists(manifestPath))
                        {
                            warnings.Add($"Manifest missing: {packageName}");
                            continue;
                        }

                        try
                        {
                            DeploymentResult deployment = await DeploymentOperationTimeout.AwaitAsync(
                                () => packageManager.RegisterPackageAsync(
                                    new Uri(manifestPath),
                                    null,
                                    DeploymentOptions.None),
                                $"Registering {packageName}");
                            if (deployment.ExtendedErrorCode is not null &&
                                deployment.ExtendedErrorCode.HResult < 0)
                            {
                                warnings.Add(
                                    $"Re-register {packageName}: {deployment.ErrorText} " +
                                    $"(0x{deployment.ExtendedErrorCode.HResult:X8})");
                            }
                            else
                            {
                                registered++;
                                report.AppendLine($"Registered: {packageName}");
                            }
                        }
                        catch (Exception exception)
                        {
                            warnings.Add($"Re-register {packageName}: {exception.Message}");
                        }
                    }
                }
                if (registered == 0)
                {
                    throw new InvalidOperationException(
                        "No Microsoft Store package manifest could be re-registered.");
                }

                AddStage(report, progress, 6, 9, "Check Microsoft Store services");
                foreach (string serviceName in new[] { "ClipSVC", "InstallService", "LicenseManager", "BITS", "wuauserv" })
                {
                    await TryStartServiceAsync(serviceName, report, warnings, cancellationToken);
                }

                AddStage(report, progress, 7, 9, "Synchronize Windows time");
                await TryStartServiceAsync("W32Time", report, warnings, cancellationToken);
                string w32tm = GetSystemTool("w32tm.exe");
                if (File.Exists(w32tm))
                {
                    NativeCommandResult timeResult = await _commandRunner.RunAsync(
                        w32tm,
                        new[] { "/resync", "/nowait" },
                        TimeSpan.FromSeconds(30),
                        cancellationToken);
                    if (timeResult.ExitCode == 0)
                    {
                        report.AppendLine("Windows time resynchronization requested.");
                    }
                    else
                    {
                        warnings.Add($"w32tm.exe returned exit code {timeResult.ExitCode}.");
                    }
                }

                AddStage(report, progress, 8, 9, "Reset Store cache with wsreset.exe");
                string wsreset = GetSystemTool("wsreset.exe");
                if (!File.Exists(wsreset))
                {
                    throw new FileNotFoundException("wsreset.exe was not found.", wsreset);
                }
                NativeCommandResult wsResult = await _commandRunner.RunAsync(
                    wsreset,
                    Array.Empty<string>(),
                    TimeSpan.FromMinutes(3),
                    cancellationToken);
                if (wsResult.ExitCode == 0)
                {
                    report.AppendLine("wsreset.exe completed successfully.");
                }
                else
                {
                    warnings.Add($"wsreset.exe returned exit code {wsResult.ExitCode}.");
                    TryOpenStore(report, warnings);
                }

                AddStage(report, progress, 9, 9, "Verify Microsoft Store package");
                StoreRegistrationState verifiedPackage = await StoreRegistrationVerification.VerifyAsync(() =>
                {
                    Package? package = FindPackages(packageManager, "Microsoft.WindowsStore")
                        .FirstOrDefault(candidate => !candidate.IsResourcePackage && !candidate.IsFramework &&
                            string.Equals(candidate.Id.FamilyName, StoreRegistrationVerification.StoreFamily,
                                StringComparison.OrdinalIgnoreCase));
                    return package is null ? null : new StoreRegistrationState(
                        package.Id.FamilyName, package.Id.FullName,
                        File.Exists(Path.Combine(package.InstalledLocation.Path, "AppXManifest.xml")),
                        package.Status.VerifyIsOK());
                }, line => report.AppendLine(line), cancellationToken);
                report.AppendLine($"Verified current-user package: {verifiedPackage.FullName}; manifest present; package status healthy.");

                AppendFinal(report, true, warnings, null);
                stages.Complete(true);
                return new MaintenanceOperationResult(true, warnings.Count, report.ToString().TrimEnd());
            }
            catch (Exception exception)
            {
                report.AppendLine($"ERROR: {exception.Message}");
                AppendFinal(report, false, warnings, exception.Message);
                stages.Complete(false);
                return new MaintenanceOperationResult(false, warnings.Count, report.ToString().TrimEnd());
            }
        }

        private async Task TryStartServiceAsync(
            string serviceName,
            StringBuilder report,
            List<string> warnings,
            CancellationToken cancellationToken)
        {
            NativeCommandResult query = await _commandRunner.RunAsync(
                GetSystemTool("sc.exe"),
                new[] { "query", serviceName },
                TimeSpan.FromSeconds(10),
                cancellationToken);
            if (query.ExitCode == 1060 || query.CombinedOutput.Contains("1060", StringComparison.Ordinal))
            {
                report.AppendLine($"Service not present: {serviceName}");
                return;
            }
            if (query.CombinedOutput.Contains("STATE              : 4", StringComparison.OrdinalIgnoreCase))
            {
                report.AppendLine($"Service already running: {serviceName}");
                return;
            }

            NativeCommandResult start = await _commandRunner.RunAsync(
                GetSystemTool("sc.exe"),
                new[] { "start", serviceName },
                TimeSpan.FromSeconds(30),
                cancellationToken);
            if (start.ExitCode is 0 or 1056)
            {
                report.AppendLine($"Started service: {serviceName}");
            }
            else
            {
                warnings.Add($"Unable to start {serviceName}; sc.exe exit {start.ExitCode}.");
            }
        }

        private static List<Package> FindPackages(PackageManager manager, string packageName, bool allUsers = false)
        {
            return (allUsers ? manager.FindPackages() : manager.FindPackagesForUser(string.Empty))
                .Where(package => string.Equals(
                    package.Id.Name,
                    packageName,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static void ClearDirectoryContents(
            string path,
            string displayName,
            StringBuilder report,
            List<string> warnings)
        {
            if (!Directory.Exists(path))
            {
                report.AppendLine($"Cache was not present: {displayName}");
                return;
            }

            int warningsBefore = warnings.Count;
            foreach (string entry in Directory.EnumerateFileSystemEntries(path))
            {
                try
                {
                    if (Directory.Exists(entry))
                    {
                        Directory.Delete(entry, recursive: true);
                    }
                    else
                    {
                        File.Delete(entry);
                    }
                }
                catch (Exception exception)
                {
                    warnings.Add($"Cache cleanup {displayName}: {exception.Message}");
                }
            }
            report.AppendLine(warnings.Count == warningsBefore
                ? $"Cleared cache: {displayName}"
                : $"Cache cleanup incomplete: {displayName}; see warnings.");
        }

        private static void TryOpenStore(StringBuilder report, List<string> warnings)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-windows-store:",
                    UseShellExecute = true
                });
                report.AppendLine("Requested direct Microsoft Store launch.");
            }
            catch (Exception exception)
            {
                warnings.Add($"Open Microsoft Store: {exception.Message}");
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
            report.AppendLine(success ? "MICROSOFT STORE REPAIR COMPLETED" : "MICROSOFT STORE REPAIR FAILED");
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
    }
}
