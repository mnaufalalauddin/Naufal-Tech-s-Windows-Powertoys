using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal readonly record struct LegacyPanelDefinition(
        string Name,
        string Executable,
        IReadOnlyList<string> Arguments);

    internal readonly record struct LegacyPanelLaunchResult(
        bool Success,
        string Message);

    internal sealed class LegacyWindowsPanelsService
    {
        public IReadOnlyList<LegacyPanelDefinition> GetPanels()
        {
            string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string system32 = Path.Combine(windowsDirectory, "System32");
            string control = Path.Combine(system32, "control.exe");
            string explorer = Path.Combine(windowsDirectory, "explorer.exe");

            return new[]
            {
                Panel("Computer Management", Path.Combine(system32, "mmc.exe"), Path.Combine(system32, "compmgmt.msc")),
                Panel("Create and format hard disk partitions", Path.Combine(system32, "mmc.exe"), Path.Combine(system32, "diskmgmt.msc")),
                Panel("Control Panel", control),
                Panel("Mouse Properties", control, "main.cpl"),
                Panel("Network Connections", control, "ncpa.cpl"),
                Panel("Power Panel", control, "powercfg.cpl"),
                Panel("Printer Panel", explorer, "shell:::{A8A91A66-3A7D-4424-8D24-04E180695C7A}"),
                Panel("Programs and Features", control, "appwiz.cpl"),
                Panel("Region", control, "intl.cpl"),
                Panel("Security and Maintenance", control, "/name", "Microsoft.ActionCenter"),
                Panel("Sound Settings", control, "mmsys.cpl"),
                Panel("System Properties", control, "sysdm.cpl"),
                Panel("Time and Date", control, "timedate.cpl"),
                Panel("Windows Defender Firewall", control, "/name", "Microsoft.WindowsFirewall"),
                Panel("Windows Restore", Path.Combine(system32, "rstrui.exe"))
            };
        }

        public LegacyPanelLaunchResult Launch(LegacyPanelDefinition panel)
        {
            if (string.IsNullOrWhiteSpace(panel.Executable) || !File.Exists(panel.Executable))
            {
                return new LegacyPanelLaunchResult(
                    false,
                    $"The Windows component for '{panel.Name}' was not found.");
            }

            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = panel.Executable,
                    UseShellExecute = true
                };

                foreach (string argument in panel.Arguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }

                Process.Start(startInfo);
                return new LegacyPanelLaunchResult(true, $"Opened: {panel.Name}");
            }
            catch (Exception exception)
            {
                return new LegacyPanelLaunchResult(
                    false,
                    $"Unable to open {panel.Name}. {exception.Message}");
            }
        }

        private static LegacyPanelDefinition Panel(
            string name,
            string executable,
            params string[] arguments)
        {
            return new LegacyPanelDefinition(name, executable, arguments);
        }
    }
}
