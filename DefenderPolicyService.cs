using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal enum DefenderPolicyMode
    {
        Disable,
        Restore
    }

    internal readonly record struct DefenderPolicyResult(
        bool Success,
        string Message,
        IReadOnlyList<SystemReportEntry> Report);

    internal sealed class DefenderPolicyService
    {
        private const string DefenderPath =
            @"SOFTWARE\Policies\Microsoft\Windows Defender";
        private const string RealTimePath =
            @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection";
        private const string SpynetPath =
            @"SOFTWARE\Policies\Microsoft\Windows Defender\Spynet";
        private const string SignaturesPath =
            @"SOFTWARE\Policies\Microsoft\Windows Defender\Signature Updates";
        private const string DefenderFeaturesPath =
            @"SOFTWARE\Microsoft\Windows Defender\Features";

        private static readonly IReadOnlyList<DefenderPolicyTarget> Targets =
            new[]
            {
                new DefenderPolicyTarget("Windows Defender", DefenderPath, "DisableAntiSpyware"),
                new DefenderPolicyTarget("Windows Defender", DefenderPath, "DisableRealtimeMonitoring"),
                new DefenderPolicyTarget("Windows Defender", DefenderPath, "DisableAntiVirus"),
                new DefenderPolicyTarget("Windows Defender", DefenderPath, "DisableRoutinelyTakingAction"),
                new DefenderPolicyTarget("Windows Defender", DefenderPath, "DisableSpecialRunningModes"),
                new DefenderPolicyTarget("Windows Defender", DefenderPath, "ServiceKeepAlive"),
                new DefenderPolicyTarget("Real-Time Protection", RealTimePath, "DisableBehaviorMonitoring"),
                new DefenderPolicyTarget("Real-Time Protection", RealTimePath, "DisableOnAccessProtection"),
                new DefenderPolicyTarget("Real-Time Protection", RealTimePath, "DisableRealtimeMonitoring"),
                new DefenderPolicyTarget("Real-Time Protection", RealTimePath, "DisableScanOnRealtimeEnable"),
                new DefenderPolicyTarget("Spynet", SpynetPath, "DisableBlockAtFirstSeen"),
                new DefenderPolicyTarget("Signature Updates", SignaturesPath, "ForceUpdateFromMU")
            };

        public Task<IReadOnlyList<SystemReportEntry>> ReadStatusAsync()
        {
            List<SystemReportEntry> rows = BuildStatusRows(expectedValue: null);
            return Task.FromResult<IReadOnlyList<SystemReportEntry>>(rows);
        }

        public Task<DefenderPolicyResult> ApplyAsync(DefenderPolicyMode mode)
        {
            if (!WindowsPrivilegeService.IsAdministrator())
            {
                return Task.FromResult(new DefenderPolicyResult(
                    false,
                    "Administrator rights are required.",
                    BuildStatusRows(expectedValue: null)));
            }

            if (mode == DefenderPolicyMode.Disable &&
                ReadTamperProtectionState() != "Off")
            {
                List<SystemReportEntry> blocked = BuildStatusRows(expectedValue: null);
                blocked.Insert(0, new SystemReportEntry(
                    "Action",
                    "Blocked: turn Tamper Protection OFF in Windows Security first.",
                    false));
                return Task.FromResult(new DefenderPolicyResult(
                    false,
                    "Tamper Protection is ON or could not be read reliably. No Defender policy was changed.",
                    blocked));
            }

            int requested = mode == DefenderPolicyMode.Disable ? 1 : 0;
            List<string> errors = new();
            foreach (DefenderPolicyTarget target in Targets)
            {
                try
                {
                    using RegistryKey key = CreateLocalMachineKey(target.Path);
                    key.SetValue(target.Name, requested, RegistryValueKind.DWord);
                }
                catch (Exception exception)
                {
                    errors.Add($"{target.Area} > {target.Name}: {exception.Message}");
                }
            }

            List<SystemReportEntry> rows = BuildStatusRows(requested);
            bool verified = errors.Count == 0;
            foreach (DefenderPolicyTarget target in Targets)
            {
                if (ReadDword(target.Path, target.Name) != requested)
                {
                    verified = false;
                }
            }

            rows.Add(new SystemReportEntry("=== NOTES ===", string.Empty, true));
            rows.Add(new SystemReportEntry(
                "Result",
                verified ? "VERIFIED" : "NOT VERIFIED",
                false));
            rows.Add(new SystemReportEntry(
                "Restart",
                "Recommended",
                false));
            if (errors.Count > 0)
            {
                rows.Add(new SystemReportEntry(
                    "Write errors",
                    string.Join(" | ", errors),
                    false));
            }

            string message = verified
                ? mode == DefenderPolicyMode.Disable
                    ? "Defender policy targets were disabled and verified."
                    : "Defender policy targets were restored and verified."
                : "One or more Defender policy values could not be verified.";
            return Task.FromResult(new DefenderPolicyResult(
                verified,
                message,
                rows));
        }

        public SecuritySettingsLaunchResult OpenTamperProtectionSettings()
        {
            string[] targets =
            {
                "windowsdefender://threatsettings/",
                "ms-settings:windowsdefender",
                "windowsdefender:"
            };
            foreach (string target in targets)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = target,
                        UseShellExecute = true
                    });
                    return new SecuritySettingsLaunchResult(
                        true,
                        "Opened Windows Security.");
                }
                catch
                {
                    // Continue through the original fallback sequence.
                }
            }

            return new SecuritySettingsLaunchResult(
                false,
                "Unable to open Windows Security.");
        }

        private static List<SystemReportEntry> BuildStatusRows(int? expectedValue)
        {
            List<SystemReportEntry> rows = new()
            {
                new SystemReportEntry("=== MICROSOFT DEFENDER ===", string.Empty, true),
                new SystemReportEntry(
                    "Tamper Protection",
                    ReadTamperProtectionState(),
                    false)
            };

            string? currentArea = null;
            foreach (DefenderPolicyTarget target in Targets)
            {
                if (!string.Equals(currentArea, target.Area, StringComparison.Ordinal))
                {
                    currentArea = target.Area;
                    rows.Add(new SystemReportEntry(
                        $"=== {currentArea.ToUpperInvariant()} ===",
                        string.Empty,
                        true));
                }

                int? actual = ReadDword(target.Path, target.Name);
                string value = actual.HasValue
                    ? $"DWORD {actual.Value}"
                    : "Not Present";
                if (expectedValue.HasValue)
                {
                    value += actual == expectedValue
                        ? " | VERIFIED"
                        : $" | EXPECTED DWORD {expectedValue.Value}";
                }
                rows.Add(new SystemReportEntry(target.Name, value, false));
            }

            return rows;
        }

        public static string ReadTamperProtectionState()
        {
            int? value = ReadDword(DefenderFeaturesPath, "TamperProtection");
            return value switch
            {
                5 => "On",
                4 => "Off",
                _ => value.HasValue
                    ? $"Unknown (DWORD {value.Value})"
                    : "Unknown"
            };
        }

        private static int? ReadDword(string path, string name)
        {
            try
            {
                using RegistryKey? key = OpenLocalMachineKey(path, writable: false);
                object? value = key?.GetValue(name);
                return value is null
                    ? null
                    : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
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

        private readonly record struct DefenderPolicyTarget(
            string Area,
            string Path,
            string Name);
    }
}
