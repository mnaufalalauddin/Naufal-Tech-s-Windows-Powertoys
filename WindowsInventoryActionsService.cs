using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed class WindowsInventoryActionsService : IToolActionService
{
    private readonly NativeCommandRunner _runner = new();
    private static readonly IReadOnlyList<ToolActionDefinition> Actions = new[]
    {
        Audit("StartupInventory", "Startup Inventory", "Exports machine/user Run keys, Startup folders and Win32_StartupCommand entries to an audit file.", "Export startup inventory", "inventory.startup"),
        Audit("ServiceInventory", "Service Inventory", "Exports service name, display name, state, start mode and executable path. No service is changed.", "Export services", "inventory.services"),
        Audit("ScheduledTaskInventory", "Scheduled Task Inventory", "Exports scheduled-task path, state, author, actions and triggers. No task is disabled.", "Export tasks", "inventory.tasks"),
        Audit("CapabilityInventory", "Windows Capability Inventory", "Exports DISM online capability state, including installed/not-present capabilities.", "Export capabilities", "inventory.capabilities"),
        Audit("OptionalFeatureInventory", "Optional Feature Inventory", "Exports DISM online optional-feature state.", "Export features", "inventory.features"),
        Audit("DriverStoreInventory", "Driver Store Inventory", "Exports third-party Driver Store package inventory with pnputil. No driver is removed.", "Export drivers", "inventory.drivers"),
        new ToolActionDefinition("DriverStoreExport", "Inventory & Recovery", "Export Third-Party Driver Store",
            "Exports third-party driver packages with pnputil /export-driver * to an application-owned backup directory. This is a backup action, not removal.",
            "Export driver backup", true, false,
            Risk: ToolActionRisk.Primary,
            CanonicalActionId: "recovery.drivers.export",
            Impact: ToolActionImpact.MaintenanceRepair,
            Evidence: ToolActionEvidence.NotApplicable),
        Audit("RecoveryInventory", "Recovery & Compact OS Inventory", "Exports reagentc status, Compact OS query and recovery-related volume information without changing recovery configuration.", "Export recovery status", "inventory.recovery")
    };

    public IReadOnlyList<ToolActionDefinition> GetActions() => Actions;

    public Task<ToolActionResult> RestoreAsync(ToolActionDefinition definition) =>
        Task.FromResult(new ToolActionResult(false, "Inventory/export actions do not mutate the inspected Windows setting and therefore have no Restore operation."));

    public async Task<ToolActionResult> RunAsync(ToolActionDefinition definition)
    {
        if (definition.RequiresAdministrator && !WindowsPrivilegeService.IsAdministrator())
            return new(false, "Administrator rights are required.");

        string directory = Path.Combine(AppDataPaths.LocalRoot, "Logs", "Inventory");
        Directory.CreateDirectory(directory);
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        if (definition.Id == "DriverStoreExport")
        {
            string destination = Path.Combine(AppDataPaths.LocalRoot, "Backups", "DriverStore", stamp);
            Directory.CreateDirectory(destination);
            NativeCommandResult export = await _runner.RunAsync("pnputil.exe",
                new[] { "/export-driver", "*", destination }, TimeSpan.FromMinutes(30));
            if (export.ExitCode != 0 || export.TimedOut)
                return new(false, "Driver export did not complete. " + export.CombinedOutput);
            return new(true, "Third-party driver export completed: " + destination);
        }

        (string exe, string[] args) = definition.Id switch
        {
            "StartupInventory" => PowerShell(StartupScript),
            "ServiceInventory" => PowerShell(ServiceScript),
            "ScheduledTaskInventory" => PowerShell(TaskScript),
            "CapabilityInventory" => ("dism.exe", new[] { "/English", "/Online", "/Get-Capabilities" }),
            "OptionalFeatureInventory" => ("dism.exe", new[] { "/English", "/Online", "/Get-Features", "/Format:Table" }),
            "DriverStoreInventory" => ("pnputil.exe", new[] { "/enum-drivers", "/files" }),
            "RecoveryInventory" => PowerShell(RecoveryScript),
            _ => throw new InvalidOperationException($"Unknown inventory action: {definition.Id}")
        };

        NativeCommandResult result = await _runner.RunAsync(exe, args, TimeSpan.FromMinutes(5));
        if (result.ExitCode != 0 || result.TimedOut)
            return new(false, $"Inventory command failed (exit {result.ExitCode}). {result.CombinedOutput}");

        string path = Path.Combine(directory, definition.Id + "-" + stamp + ".txt");
        string header = "Naufal Windows Utility inventory\r\nUTC: " + DateTime.UtcNow.ToString("O") +
            "\r\nAction: " + definition.Name + "\r\nRead-only inventory; not a performance recommendation.\r\n\r\n";
        await File.WriteAllTextAsync(path, header + result.StandardOutput + 
            (string.IsNullOrWhiteSpace(result.StandardError) ? "" : "\r\nSTDERR\r\n" + result.StandardError),
            new UTF8Encoding(false));
        return new(true, "Inventory exported: " + path);
    }

    private static ToolActionDefinition Audit(string id, string name, string description, string label, string canonical) =>
        new(id, "Inventory & Recovery", name, description, label, id is "CapabilityInventory" or "OptionalFeatureInventory" or "DriverStoreInventory" or "RecoveryInventory", false,
            CanonicalActionId: canonical, Impact: ToolActionImpact.MaintenanceRepair, Evidence: ToolActionEvidence.NotApplicable);

    private static (string, string[]) PowerShell(string script) =>
        ("powershell.exe", new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script });

    private const string StartupScript = @"
$ErrorActionPreference='Continue'
'=== HKCU Run ==='; Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue | Format-List
'=== HKLM Run ==='; Get-ItemProperty 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue | Format-List
'=== HKLM Run 32-bit ==='; Get-ItemProperty 'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue | Format-List
'=== Startup folders ==='; @([Environment]::GetFolderPath('Startup'),[Environment]::GetFolderPath('CommonStartup')) | ForEach-Object { $_; Get-ChildItem -Force $_ -ErrorAction SilentlyContinue | Select-Object Name,FullName,Length,LastWriteTime }
'=== Win32_StartupCommand ==='; Get-CimInstance Win32_StartupCommand -ErrorAction SilentlyContinue | Select-Object Name,Command,Location,User | Format-List
";

    private const string ServiceScript = @"
Get-CimInstance Win32_Service | Sort-Object Name | Select-Object Name,DisplayName,State,StartMode,PathName,StartName | Format-List
";

    private const string TaskScript = @"
Get-ScheduledTask | Sort-Object TaskPath,TaskName | ForEach-Object {
  [pscustomobject]@{TaskPath=$_.TaskPath;TaskName=$_.TaskName;State=$_.State;Author=$_.Author;Actions=($_.Actions | Out-String).Trim();Triggers=($_.Triggers | Out-String).Trim()}
} | Format-List
";

    private const string RecoveryScript = @"
'=== Windows RE ==='; reagentc /info
'=== Compact OS ==='; compact.exe /CompactOS:query
'=== Volumes ==='; Get-Volume | Select-Object DriveLetter,FileSystemLabel,FileSystem,HealthStatus,Size,SizeRemaining | Format-Table -AutoSize
";
}
