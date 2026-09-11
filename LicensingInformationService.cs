using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal sealed class LicensingInformationService
    {
        public async Task<IReadOnlyList<SystemReportEntry>> ReadWindowsActivationAsync()
        {
            List<SystemReportEntry> rows = new();
            AddSection(rows, "WINDOWS ACTIVATION");

            string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string cscript = Path.Combine(windowsDirectory, "System32", "cscript.exe");
            string slmgr = Path.Combine(windowsDirectory, "System32", "slmgr.vbs");

            if (!File.Exists(cscript) || !File.Exists(slmgr))
            {
                AddRow(rows, "Status", "slmgr.vbs or cscript.exe was not found.");
            }
            else
            {
                CommandResult result = await RunAsync(
                    cscript,
                    new[] { "//nologo", slmgr, "/xpr" },
                    TimeSpan.FromSeconds(20));
                string status = JoinUsefulLines(result.Output, result.Error);

                AddRow(
                    rows,
                    "Status",
                    string.IsNullOrWhiteSpace(status)
                        ? $"No activation information returned (exit code {result.ExitCode})."
                        : status);
            }

            AddSection(rows, "SYSTEM");
            const string ntPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            string productName = ReadRegistryString(ntPath, "ProductName");
            string displayVersion = ReadRegistryString(ntPath, "DisplayVersion");
            string build = ReadRegistryString(ntPath, "CurrentBuildNumber");

            AddRow(rows, "Windows", NormalizeWindowsProductName(productName, build));
            AddRow(rows, "Version", BuildWindowsVersion(build, displayVersion));
            AddRow(rows, "Build", build);
            return rows;
        }

        public async Task<IReadOnlyList<SystemReportEntry>> ReadOfficeActivationAsync()
        {
            List<SystemReportEntry> rows = new();
            AddSection(rows, "OFFICE ACTIVATION");

            IReadOnlyList<string> possiblePaths = OfficeScriptDiscovery.Candidates(
                new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }, ReadOfficeInstallRoots());
            string ospp = string.Empty;
            foreach (string path in possiblePaths)
                if (File.Exists(path)) { ospp = path; break; }
            AddRow(rows, "Scope", "Volume-license information from OSPP.VBS; Microsoft 365 subscription activation is not verified by this script.");
            if (string.IsNullOrWhiteSpace(ospp))
            {
                AddRow(rows, "Status", "OSPP.VBS was not found in the supported locations.");
                AddRow(rows, "Activation", "Office activation status could not be determined.");
                return rows;
            }

            AddRow(rows, "OSPP.VBS", ospp);

            string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string cscript = Path.Combine(windowsDirectory, "System32", "cscript.exe");
            CommandResult result = await RunAsync(
                cscript,
                new[] { "//nologo", ospp, "/dstatus" },
                TimeSpan.FromSeconds(30));

            List<string> lines = GetUsefulLines(result.Output, result.Error);
            if (lines.Count == 0)
            {
                AddRow(rows, "Status", $"No activation information returned (exit code {result.ExitCode}).");
                return rows;
            }

            int lineNumber = 1;
            foreach (string line in lines)
            {
                int separator = line.IndexOf(':');
                if (separator > 0)
                {
                    string property = line[..separator].Trim();
                    string value = line[(separator + 1)..].Trim();
                    AddRow(
                        rows,
                        string.IsNullOrWhiteSpace(property) ? $"Result {lineNumber}" : property,
                        value);
                }
                else
                {
                    AddRow(rows, $"Result {lineNumber}", line);
                }

                lineNumber++;
            }

            return rows;
        }

        private static async Task<CommandResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            TimeSpan timeoutValue)
        {
            try
            {
                NativeCommandResult result = await new NativeCommandRunner().RunAsync(executable, arguments, timeoutValue);
                return new CommandResult(result.ExitCode, result.StandardOutput, result.StandardError);
            }
            catch (Exception exception)
            {
                return new CommandResult(-1, string.Empty, exception.Message);
            }
        }

        private static IReadOnlyList<string> ReadOfficeInstallRoots()
        {
            List<string> roots = new();
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    foreach (string version in new[] { "16.0", "15.0", "14.0" })
                    {
                        using RegistryKey? install = machine.OpenSubKey($@"SOFTWARE\Microsoft\Office\{version}\Common\InstallRoot");
                        if (install?.GetValue("Path") is string root) roots.Add(root);
                    }
                    using RegistryKey? clickToRun = machine.OpenSubKey(@"SOFTWARE\Microsoft\Office\ClickToRun\Configuration");
                    if (clickToRun?.GetValue("InstallationPath") is string directory) roots.Add(directory);
                }
                catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
                { /* Continue to the other registry view and standard Program Files locations. */ }
            }
            return roots;
        }

        private static List<string> GetUsefulLines(params string[] values)
        {
            List<string> lines = new();
            foreach (string value in values)
            {
                foreach (string line in value.Split(
                             new[] { '\r', '\n' },
                             StringSplitOptions.RemoveEmptyEntries))
                {
                    string clean = line.Trim();
                    if (!string.IsNullOrWhiteSpace(clean))
                    {
                        lines.Add(clean);
                    }
                }
            }

            return lines;
        }

        private static string JoinUsefulLines(params string[] values)
        {
            return string.Join(" ", GetUsefulLines(values));
        }

        private static string ReadRegistryString(string path, string name)
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(path, writable: false);
                object? value = key?.GetValue(
                    name,
                    null,
                    RegistryValueOptions.DoNotExpandEnvironmentNames);
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string BuildWindowsVersion(string build, string displayVersion)
        {
            return int.TryParse(build, NumberStyles.Integer, CultureInfo.InvariantCulture, out int buildNumber)
                ? $"10.0.{buildNumber}"
                : string.IsNullOrWhiteSpace(displayVersion)
                    ? Environment.OSVersion.Version.ToString()
                    : displayVersion;
        }

        private static string NormalizeWindowsProductName(
            string productName,
            string build)
        {
            if (int.TryParse(
                    build,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int buildNumber) &&
                buildNumber >= 22000 &&
                productName.Contains("Windows 10", StringComparison.OrdinalIgnoreCase))
            {
                return productName.Replace(
                    "Windows 10",
                    "Windows 11",
                    StringComparison.OrdinalIgnoreCase);
            }

            return productName;
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

        private readonly record struct CommandResult(
            int ExitCode,
            string Output,
            string Error);
    }
}
