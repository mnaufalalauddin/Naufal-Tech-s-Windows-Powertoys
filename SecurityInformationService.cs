using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal readonly record struct SecuritySettingsLaunchResult(
        bool Success,
        string Message);

    internal readonly record struct BitLockerOperationResult(
        bool Success,
        string Message,
        IReadOnlyList<SystemReportEntry> Report);

    internal sealed class SecurityInformationService
    {
        public async Task<IReadOnlyList<SystemReportEntry>> ReadBitLockerStatusAsync()
        {
            List<SystemReportEntry> rows = new();
            AddSection(rows, "BITLOCKER STATUS");
            try
            {
                IReadOnlyList<BitLockerVolumeInfo> volumes = await ReadBitLockerVolumesAsync();
                foreach (BitLockerVolumeInfo volume in volumes)
                {
                    AddSection(rows, volume.MountPoint);
                    AddVolumeRows(rows, volume);
                }
                if (volumes.Count == 0) AddRow(rows, "Status", "No BitLocker volumes were returned.");
            }
            catch (Exception exception)
            {
                AddRow(rows, "Status", "Unavailable / not verified");
                AddRow(rows, "Query error", exception.Message);
            }
            return rows;
        }

        public Task<IReadOnlyList<BitLockerVolumeInfo>> ReadBitLockerVolumesAsync() =>
            Task.Run(NativeHardwareData.ReadBitLockerVolumes);

        public async Task<BitLockerVolumeInfo?> ReadBitLockerVolumeAsync(string mountPoint)
        {
            string normalized = NormalizeMountPoint(mountPoint);
            try
            {
                foreach (BitLockerVolumeInfo volume in await ReadBitLockerVolumesAsync())
                    if (string.Equals(volume.MountPoint, normalized, StringComparison.OrdinalIgnoreCase))
                        return volume;
            }
            catch { /* Read failure must never verify an operation. */ }
            return null;
        }

        public Task<BitLockerOperationResult> SuspendBitLockerAsync(string mountPoint) =>
            ChangeBitLockerAsync(
                mountPoint,
                new[] { "-protectors", "-disable", NormalizeMountPoint(mountPoint), "-RebootCount", "0" },
                expectedProtection: "Off",
                successMessage: "BitLocker protection suspended.");

        public Task<BitLockerOperationResult> ResumeBitLockerAsync(string mountPoint) =>
            ChangeBitLockerAsync(
                mountPoint,
                new[] { "-protectors", "-enable", NormalizeMountPoint(mountPoint) },
                expectedProtection: "On",
                successMessage: "BitLocker protection resumed.");

        public async Task<BitLockerOperationResult> StartBitLockerDecryptionAsync(
            string mountPoint)
        {
            string normalized = NormalizeMountPoint(mountPoint);
            string manageBde = GetManageBdePath();
            List<SystemReportEntry> rows = new();
            AddSection(rows, "BITLOCKER DECRYPTION");
            AddRow(rows, "Drive", normalized);

            if (!File.Exists(manageBde))
            {
                AddRow(rows, "Result", "BitLocker management tools are not installed.");
                return new BitLockerOperationResult(false, "BitLocker is not available.", rows);
            }

            CommandResult command = await RunAsync(
                manageBde,
                new[] { "-off", normalized },
                TimeSpan.FromSeconds(30));
            AddCommandRows(rows, command);
            BitLockerVolumeInfo? after = await ReadBitLockerVolumeAsync(normalized);
            if (after is BitLockerVolumeInfo volume)
            {
                AddVolumeRows(rows, volume);
            }

            bool accepted = BitLockerVerification.DecryptionAccepted(command.ExitCode, after);
            AddRow(rows, "Verification", accepted ? "PASS" : "NOT VERIFIED");
            return new BitLockerOperationResult(
                accepted,
                accepted
                    ? "BitLocker decryption started."
                    : "Windows did not verify that decryption started.",
                rows);
        }

        public Task<IReadOnlyList<SystemReportEntry>> ReadSmartAppControlStatusAsync()
        {
            List<SystemReportEntry> rows = new();
            AddSection(rows, "SMART APP CONTROL");

            const string policyPath = @"SYSTEM\CurrentControlSet\Control\CI\Policy";
            const string valueName = "VerifiedAndReputablePolicyState";

            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                    policyPath,
                    writable: false);
                object? rawValue = key?.GetValue(valueName);

                if (rawValue is null)
                {
                    AddRow(rows, "Status", "Unavailable");
                    AddRow(rows, "Details", "The Smart App Control policy value was not found on this Windows installation.");
                }
                else
                {
                    int state = Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
                    string status = state switch
                    {
                        0 => "Off",
                        1 => "On",
                        2 => "Evaluation",
                        _ => "Unknown"
                    };

                    AddRow(rows, "Status", status);
                    AddRow(rows, "Policy state", state.ToString(CultureInfo.InvariantCulture));
                }
            }
            catch (Exception exception)
            {
                AddRow(rows, "Status", "Unable to read status");
                AddRow(rows, "Details", exception.Message);
            }

            AddRow(
                rows,
                "Configuration",
                "Changes are handled by the official Windows Security interface.");
            return Task.FromResult<IReadOnlyList<SystemReportEntry>>(rows);
        }

        public SecuritySettingsLaunchResult OpenBitLockerSettings()
        {
            string control = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32",
                "control.exe");
            return Launch(
                control,
                new[] { "/name", "Microsoft.BitLockerDriveEncryption" },
                "Manage BitLocker");
        }

        private async Task<BitLockerOperationResult> ChangeBitLockerAsync(
            string mountPoint,
            IReadOnlyList<string> arguments,
            string expectedProtection,
            string successMessage)
        {
            string normalized = NormalizeMountPoint(mountPoint);
            List<SystemReportEntry> rows = new();
            AddSection(rows, "BITLOCKER PROTECTION");
            AddRow(rows, "Drive", normalized);
            string manageBde = GetManageBdePath();
            if (!File.Exists(manageBde))
            {
                AddRow(rows, "Result", "BitLocker management tools are not installed.");
                return new BitLockerOperationResult(false, "BitLocker is not available.", rows);
            }

            CommandResult command = await RunAsync(
                manageBde,
                arguments,
                TimeSpan.FromSeconds(30));
            AddCommandRows(rows, command);
            BitLockerVolumeInfo? after = await ReadBitLockerVolumeAsync(normalized);
            if (after is BitLockerVolumeInfo volume)
            {
                AddVolumeRows(rows, volume);
            }

            bool verified = BitLockerVerification.ProtectionSucceeded(command.ExitCode, after, expectedProtection);
            AddRow(rows, "Verification", verified ? "PASS" : "NOT VERIFIED");
            return new BitLockerOperationResult(
                verified,
                verified ? successMessage : "BitLocker read-back did not confirm the requested state.",
                rows);
        }

        private static void AddVolumeRows(
            List<SystemReportEntry> rows,
            BitLockerVolumeInfo volume)
        {
            AddRow(rows, "Drive", volume.MountPoint);
            AddRow(rows, "Volume Status", volume.VolumeStatus);
            AddRow(
                rows,
                "Encrypted",
                volume.EncryptionPercentage is int percentage
                    ? percentage.ToString(CultureInfo.InvariantCulture) + "%"
                    : "Unknown");
            AddRow(rows, "Protection", volume.ProtectionStatus);
            AddRow(rows, "Lock Status", volume.LockStatus);
        }

        private static void AddCommandRows(List<SystemReportEntry> rows, CommandResult result)
        {
            AddRow(rows, "Command exit code", result.ExitCode.ToString(CultureInfo.InvariantCulture));
            int lineNumber = 1;
            foreach (string line in GetUsefulLines(result.Output, result.Error))
            {
                AddRow(rows, $"Output {lineNumber++}", line);
            }
        }

        private static string NormalizeMountPoint(string mountPoint)
        {
            string value = (mountPoint ?? string.Empty).Trim().TrimEnd('\\');
            if (value.Length >= 2 && value[1] == ':')
            {
                return char.ToUpperInvariant(value[0]) + ":";
            }

            throw new ArgumentException("Select a valid BitLocker drive.", nameof(mountPoint));
        }

        private static string GetManageBdePath() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "manage-bde.exe");

        public SecuritySettingsLaunchResult OpenSmartAppControlSettings()
        {
            return Launch(
                "windowsdefender://appbrowser",
                Array.Empty<string>(),
                "Smart App Control settings");
        }

        private static SecuritySettingsLaunchResult Launch(
            string target,
            IReadOnlyList<string> arguments,
            string displayName)
        {
            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = target,
                    UseShellExecute = true
                };
                foreach (string argument in arguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }

                Process.Start(startInfo);
                return new SecuritySettingsLaunchResult(true, $"Opened: {displayName}");
            }
            catch (Exception exception)
            {
                return new SecuritySettingsLaunchResult(
                    false,
                    $"Unable to open {displayName}. {exception.Message}");
            }
        }

        private static async Task<CommandResult> RunAsync(
            string executable, IReadOnlyList<string> arguments, TimeSpan timeoutValue)
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
