using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal enum WindowsRepairMode
    {
        Full,
        Quick
    }

    internal readonly record struct WindowsRepairResult(
        bool Success,
        bool HasWarnings,
        bool RestartRequired,
        string Report);

    internal sealed class WindowsRepairService
    {
        private readonly NativeCommandRunner _commandRunner = new();

        public async Task<WindowsRepairResult> RunAsync(
            WindowsRepairMode mode,
            IProgress<MaintenanceProgressUpdate>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!WindowsPrivilegeService.IsAdministrator())
            {
                return new WindowsRepairResult(
                    false,
                    false,
                    false,
                    "Administrator rights are required. Close the application and start it with Run as administrator.");
            }

            string system32 = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32");
            StringBuilder report = new();
            List<string> summary = new();
            bool failed = false;
            bool warnings = false;
            bool restartRequired = false;
            MaintenanceStageTracker stages = new(progress, () => warnings ? 1 : 0);
            progress = stages;
            DateTimeOffset started = DateTimeOffset.Now;

            report.AppendLine(mode == WindowsRepairMode.Full ? "FULL REPAIR" : "QUICK REPAIR");
            report.AppendLine($"Started: {started:yyyy-MM-dd HH:mm:ss zzz}");
            report.AppendLine(new string('=', 74));

            if (mode == WindowsRepairMode.Full)
            {
                MaintenanceProgress.StartStage(
                    progress,
                    1,
                    2,
                    "DISM - Restore Windows Image");
                string dism = Path.Combine(system32, "Dism.exe");
                NativeCommandResult dismResult = await RunStageAsync(
                    report,
                    "DISM - Restore Windows Image",
                    dism,
                    new[] { "/Online", "/Cleanup-Image", "/RestoreHealth" },
                    TimeSpan.FromHours(2),
                    cancellationToken, new CommandOutputProgress(progress, 1, 2, "DISM - Restore Windows Image"));

                if (RepairCommandClassification.DismSucceeded(dismResult))
                {
                    summary.Add($"DISM /RestoreHealth : PASSED (exit {dismResult.ExitCode})");
                    restartRequired = dismResult.ExitCode == 3010;
                }
                else
                {
                    summary.Add($"DISM /RestoreHealth : FAILED (exit {dismResult.ExitCode})");
                    summary.Add("SFC /scannow        : SKIPPED");
                    failed = true;
                }
            }

            if (!failed)
            {
                MaintenanceProgress.StartStage(
                    progress,
                    mode == WindowsRepairMode.Full ? 2 : 1,
                    mode == WindowsRepairMode.Full ? 2 : 1,
                    "SFC - Verify Protected System Files");
                string sfc = Path.Combine(system32, "sfc.exe");
                NativeCommandResult sfcResult = await RunStageAsync(
                    report,
                    "SFC - Verify Protected System Files",
                    sfc,
                    new[] { "/scannow" },
                    TimeSpan.FromMinutes(90),
                    cancellationToken, new CommandOutputProgress(progress, mode == WindowsRepairMode.Full ? 2 : 1,
                        mode == WindowsRepairMode.Full ? 2 : 1, "SFC - Verify Protected System Files"));

                RepairCommandOutcome outcome = RepairCommandClassification.Sfc(sfcResult);
                if (outcome == RepairCommandOutcome.Passed)
                {
                    summary.Add("SFC /scannow        : PASSED (exit 0)");
                }
                else if (outcome == RepairCommandOutcome.Failed)
                {
                    summary.Add(sfcResult.TimedOut
                        ? "SFC /scannow        : FAILED (scan timed out; completion was not verified)"
                        : $"SFC /scannow        : FAILED (exit {sfcResult.ExitCode}; scan could not complete)");
                    failed = true;
                }
                else
                {
                    summary.Add($"SFC /scannow        : WARNING (exit {sfcResult.ExitCode}; review CBS.log)");
                    warnings = true;
                }
            }

            stages.Complete(!failed);
            TimeSpan duration = DateTimeOffset.Now - started;
            report.AppendLine();
            report.AppendLine(new string('=', 74));
            report.AppendLine("FINAL RESULT");
            report.AppendLine(new string('=', 74));
            foreach (string line in summary)
            {
                report.AppendLine(line);
            }
            report.AppendLine($"Duration         : {duration:hh\\:mm\\:ss}");
            report.AppendLine($"Restart required : {(restartRequired ? "YES" : "NO")}");

            return new WindowsRepairResult(
                !failed,
                warnings,
                restartRequired,
                report.ToString().TrimEnd());
        }

        private async Task<NativeCommandResult> RunStageAsync(
            StringBuilder report,
            string displayName,
            string executable,
            IReadOnlyList<string> arguments,
            TimeSpan timeoutValue,
            CancellationToken cancellationToken, CommandOutputProgress outputProgress)
        {
            report.AppendLine();
            report.AppendLine(new string('-', 74));
            report.AppendLine($"[{DateTime.Now:HH:mm:ss}] {displayName}");
            report.AppendLine($"Command: {executable} {string.Join(' ', arguments)}");
            report.AppendLine(new string('-', 74));

            if (!File.Exists(executable))
            {
                report.AppendLine("Required Windows tool was not found.");
                return new NativeCommandResult(-1, string.Empty, "Required Windows tool was not found.", false, TimeSpan.Zero);
            }

            NativeCommandResult result = await _commandRunner.RunAsync(
                executable,
                arguments,
                timeoutValue,
                cancellationToken, outputProgress,
                Path.GetFileName(executable).Equals("sfc.exe", StringComparison.OrdinalIgnoreCase) ? Encoding.Unicode : null);
            outputProgress.Flush();
            if (!string.IsNullOrWhiteSpace(result.CombinedOutput))
            {
                report.AppendLine(result.CombinedOutput);
            }
            report.AppendLine($"[{DateTime.Now:HH:mm:ss}] Exit code: {result.ExitCode}");
            return result;
        }
    }
}
