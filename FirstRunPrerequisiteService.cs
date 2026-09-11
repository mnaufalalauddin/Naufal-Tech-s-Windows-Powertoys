using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Management.Deployment;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal readonly record struct FirstRunPrerequisiteStatus(
        string WinGet,
        string Wmi,
        string Infrastructure);

    internal readonly record struct FirstRunPrerequisiteResult(
        bool Success,
        int WarningCount,
        string Report);

    /// <summary>
    /// Native counterpart of the reference application's schema-2 first-run
    /// prerequisite workflow. Merely opening the wizard is read-only; Windows
    /// changes begin only after the user presses Run selected.
    /// </summary>
    internal sealed class FirstRunPrerequisiteService
    {
        private const int StateSchema = 2;
        private static readonly string StateDirectory = AppDataPaths.SettingsDirectory;
        private static readonly string StatePath = Path.Combine(
            StateDirectory,
            "first-run-prerequisites.json");

        private readonly NativeCommandRunner _runner = new();
        private static readonly WmiPrerequisiteProbe WmiProbe = new();

        public bool ShouldShow()
        {
            try
            {
                if (!File.Exists(StatePath))
                {
                    return true;
                }

                return FirstRunWizardPolicy.ShouldShow(File.ReadAllText(StatePath), StateSchema);
            }
            catch
            {
                return true;
            }
        }

        public async Task<FirstRunPrerequisiteStatus> ReadStatusAsync()
        {
            NativeCommandResult winget = await ProbeWinGetAsync();
            string infrastructure = File.Exists(GetSystemToolPath("dism.exe"))
                ? "Will be verified automatically"
                : "DISM.exe is missing";
            return new FirstRunPrerequisiteStatus(
                winget.ExitCode == 0
                    ? $"WinGet {FirstLine(winget.StandardOutput)}"
                    : "WinGet / App Installer not detected",
                "Will be verified automatically",
                infrastructure);
        }

        public void Suppress()
        {
            SaveState(completed: false, suppress: true, "User selected DONT SHOW AGAIN.");
        }

        public void Defer()
        {
            // SKIP FOR NOW is deliberately session-only. Persist an explicit
            // incomplete, non-suppressed state so the wizard is offered again
            // on the next application startup. Only Suppress() may permanently
            // hide it without completing the prerequisite workflow.
            SaveState(completed: false, suppress: false, "User selected SKIP FOR NOW.");
        }

        public async Task<FirstRunPrerequisiteResult> RunAsync(
            bool createRestorePoint,
            bool setupWinGet,
            IProgress<MaintenanceProgressUpdate>? progress)
        {
            const int stageCount = 9;
            StringBuilder report = new();
            int warnings = 0;
            bool failed = false;

            void Start(int index, string name) => MaintenanceProgress.StartStage(progress, index, stageCount, name);

            void Stage(int index, string name, string detail, string status = "PASS")
            {
                progress?.Report(new MaintenanceProgressUpdate(
                    index,
                    stageCount,
                    name,
                    detail,
                    100, detail.StartsWith("SKIPPED", StringComparison.Ordinal) ? "SKIPPED" : status));
                report.AppendLine($"[{index}/{stageCount}] {name}: {detail}");
            }

            string dismPath = GetSystemToolPath("dism.exe");
            bool isAdmin = WindowsPrivilegeService.IsAdministrator();
            Stage(
                1,
                "Administrator / environment preflight",
                $"Administrator={(isAdmin ? "YES" : "NO")}; DISM={(File.Exists(dismPath) ? "PRESENT" : "MISSING")}",
                isAdmin && File.Exists(dismPath) ? "PASS" : "FAILED");
            if (!isAdmin || !File.Exists(dismPath))
            {
                return new FirstRunPrerequisiteResult(
                    false,
                    0,
                    report.AppendLine("First-run preflight failed.").ToString());
            }

            if (createRestorePoint)
            {
                Start(2, "Create System Restore Point");
                ToolActionResult restorePoint = await CreateRestorePointAsync();
                Stage(2, "Create System Restore Point", restorePoint.Message, restorePoint.Success ? "PASS" : "WARNING");
                Stage(
                    3,
                    "Verify System Restore",
                    restorePoint.Success
                        ? "Windows accepted the restore-point request."
                        : "Windows did not confirm a new checkpoint; System Protection or its frequency limit may be responsible.", restorePoint.Success ? "PASS" : "WARNING");
                if (!restorePoint.Success)
                {
                    warnings++;
                }
            }
            else
            {
                Stage(2, "Create System Restore Point", "SKIPPED - not selected by the user.");
                Stage(3, "Verify System Restore", "SKIPPED - not selected by the user.");
            }

            if (setupWinGet)
            {
                Start(4, "Install / Update WinGet + App Installer");
                (bool success, bool warning, string detail) = await EnsureWinGetAsync();
                Stage(4, "Install / Update WinGet + App Installer", detail, !success ? "FAILED" : warning ? "WARNING" : "PASS");
                Start(5, "Verify WinGet");
                NativeCommandResult verify = await ProbeWinGetAsync();
                bool verified = verify.ExitCode == 0;
                Stage(
                    5,
                    "Verify WinGet",
                    verified
                        ? $"WinGet {FirstLine(verify.StandardOutput)} is functional."
                        : "WinGet remains unavailable after setup.", verified ? "PASS" : "FAILED");
                failed |= !success || !verified;
                if (warning)
                {
                    warnings++;
                }
            }
            else
            {
                Stage(4, "Install / Update WinGet + App Installer", "SKIPPED - not selected by the user.");
                Stage(5, "Verify WinGet", "SKIPPED - not selected by the user.");
            }

            Start(6, WmiPrerequisiteProbe.SystemStage);
            WmiPrerequisiteResult system = await WmiProbe.VerifySystemAsync();
            Stage(6, WmiPrerequisiteProbe.SystemStage, system.Detail, system.Status);
            Start(7, WmiPrerequisiteProbe.MemoryStage);
            WmiPrerequisiteResult memory = await WmiProbe.VerifyMemoryAsync();
            Stage(7, WmiPrerequisiteProbe.MemoryStage, memory.Detail, memory.Status);
            failed |= !system.Verified || !memory.Verified;
            if (system.State == WmiPrerequisiteState.TimedOut) warnings++;
            if (memory.State == WmiPrerequisiteState.TimedOut) warnings++;

            Start(8, "Verify Windows Servicing / WMI / AppX");
            (bool infrastructurePassed, string infrastructureReport) =
                await VerifyInfrastructureAsync(dismPath);
            Stage(8, "Verify Windows Servicing / WMI / AppX", infrastructureReport, infrastructurePassed ? "PASS" : "FAILED");
            failed |= !infrastructurePassed;

            if (!failed)
            {
                bool saved = SaveState(
                    completed: true,
                    suppress: false,
                    $"Warnings={warnings}");
                Stage(
                    9,
                    "Save first-run setup state",
                    saved
                        ? "First-run setup completed state saved."
                        : "Setup completed, but the one-time state file could not be saved.", saved ? "PASS" : "WARNING");
                if (!saved)
                {
                    warnings++;
                }
            }
            else
            {
                Stage(
                    9,
                    "Save first-run setup state",
                    "SKIPPED - a selected prerequisite failed verification.");
            }

            return new FirstRunPrerequisiteResult(!failed, warnings, report.ToString().TrimEnd());
        }

        private async Task<ToolActionResult> CreateRestorePointAsync()
        {
            using GamingActionsService actions = new();
            ToolActionDefinition definition = actions.GetActions().First(item =>
                item.Id.Equals("RestorePoint", StringComparison.Ordinal));
            return await actions.RunAsync(definition);
        }

        private async Task<(bool Success, bool Warning, string Detail)> EnsureWinGetAsync()
        {
            NativeCommandResult before = await ProbeWinGetAsync();
            if (before.ExitCode == 0)
            {
                NativeCommandResult upgrade = await _runner.RunAsync(
                    "winget.exe",
                    new[]
                    {
                        "upgrade", "--id", "Microsoft.AppInstaller", "--exact",
                        "--source", "winget", "--accept-source-agreements",
                        "--accept-package-agreements", "--disable-interactivity",
                        "--silent", "--include-unknown"
                    },
                    TimeSpan.FromMinutes(4));
                return upgrade.ExitCode == 0
                    ? (true, false, "WinGet/App Installer update check completed.")
                    : (true, true, $"Update returned exit {upgrade.ExitCode}; functional verification will decide readiness.");
            }

            string bundlePath = Path.Combine(
                AppDataPaths.GetTemporaryDirectory(),
                "NaufalPowertoys-WinGet-" + Guid.NewGuid().ToString("N") + ".msixbundle");
            bool preserveBundle = false;
            try
            {
                using CancellationTokenSource downloadTimeout = new(TimeSpan.FromMinutes(4));
                using HttpClient client = new();
                using HttpResponseMessage response = await client.GetAsync(
                    "https://aka.ms/getwinget",
                    HttpCompletionOption.ResponseHeadersRead, downloadTimeout.Token);
                response.EnsureSuccessStatusCode();
                await using (Stream input = await response.Content.ReadAsStreamAsync(downloadTimeout.Token))
                {
                    await AtomicDownloadFile.SaveAsync(input, bundlePath,
                        response.Content.Headers.ContentLength, 1024, 512L * 1024 * 1024,
                        null, downloadTimeout.Token);
                }

                PackageManager manager = new();
                DeploymentResult install = await DeploymentOperationTimeout.AwaitAsync(
                    () => manager.AddPackageAsync(
                        new Uri(bundlePath),
                        null,
                        DeploymentOptions.ForceApplicationShutdown),
                    "Installing App Installer",
                    TimeSpan.FromMinutes(5));
                if (install.ExtendedErrorCode.HResult < 0)
                {
                    return (false, false, $"App Installer deployment failed: {install.ErrorText} (0x{install.ExtendedErrorCode.HResult:X8}).");
                }
                return (true, false, "Microsoft App Installer package installation completed.");
            }
            catch (TimeoutException exception)
            {
                // Deployment may still be consuming this file after our wait ends.
                preserveBundle = true;
                return (false, false, $"App Installer setup was not confirmed: {exception.Message} Retained package: {bundlePath}");
            }
            catch (Exception exception)
            {
                return (false, false, $"App Installer setup failed: {exception.Message}");
            }
            finally
            {
                try
                {
                    if (!preserveBundle && File.Exists(bundlePath))
                    {
                        File.Delete(bundlePath);
                    }
                }
                catch
                {
                    // Leave a locked package intact; never force-delete an active deployment.
                }
            }
        }

        private async Task<(bool Success, string Detail)> VerifyInfrastructureAsync(
            string dismPath)
        {
            List<string> details = new();
            NativeCommandResult dism = await _runner.RunAsync(
                dismPath,
                new[] { "/Online", "/Get-CurrentEdition", "/English" },
                TimeSpan.FromMinutes(2));
            bool success = !dism.TimedOut && dism.ExitCode == 0;
            details.Add($"DISM={(dism.TimedOut ? "TIMED OUT / UNVERIFIED" : success ? "PASS" : $"FAILED ({dism.ExitCode})")}");
            if (!success) details.Add(dism.CombinedOutput);

            foreach (string service in new[]
                     {
                         "winmgmt", "TrustedInstaller", "AppXSvc", "StateRepository"
                     })
            {
                NativeCommandResult query = await _runner.RunAsync(
                    "sc.exe",
                    new[] { "query", service },
                    TimeSpan.FromSeconds(15));
                bool installed = !query.TimedOut && query.ExitCode == 0;
                success &= installed;
                // PRESENT is deliberately not a claim that a demand-start service
                // is running. Access errors/timeouts are not missing services.
                string state = installed ? "PRESENT" : query.TimedOut ? "TIMED OUT / UNVERIFIED" :
                    query.ExitCode == 1060 ? "MISSING" : $"QUERY FAILED ({query.ExitCode})";
                details.Add($"{service}={state}");
                if (!installed) details.Add(query.CombinedOutput);
            }

            return (success, string.Join("; ", details));
        }

        private async Task<NativeCommandResult> ProbeWinGetAsync()
        {
            try
            {
                return await _runner.RunAsync(
                    "winget.exe",
                    new[] { "--version" },
                    TimeSpan.FromSeconds(20));
            }
            catch (Exception exception)
            {
                return new NativeCommandResult(
                    -1,
                    string.Empty,
                    exception.Message,
                    false,
                    TimeSpan.Zero);
            }
        }

        private static string GetSystemToolPath(params string[] parts)
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string[] pathParts = new[] { windows, "System32" }.Concat(parts).ToArray();
            return Path.Combine(pathParts);
        }

        private static string FirstLine(string value) =>
            value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim() ?? "functional";

        private static bool SaveState(bool completed, bool suppress, string results)
        {
            try
            {
                Directory.CreateDirectory(StateDirectory);
                string escapedResults = JsonEncodedText.Encode(results).ToString();
                string json = "{" + Environment.NewLine +
                              $"  \"Schema\": {StateSchema}," + Environment.NewLine +
                              $"  \"Completed\": {completed.ToString().ToLowerInvariant()}," + Environment.NewLine +
                              $"  \"Suppress\": {suppress.ToString().ToLowerInvariant()}," + Environment.NewLine +
                              $"  \"SavedUtc\": \"{DateTime.UtcNow:O}\"," + Environment.NewLine +
                              $"  \"Results\": \"{escapedResults}\"" + Environment.NewLine +
                              "}";
                File.WriteAllText(StatePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
