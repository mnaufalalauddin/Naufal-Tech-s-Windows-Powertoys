using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed class OfflineImageActionsService : IToolActionService
{
    private readonly NativeCommandRunner _runner = new();
    private static readonly IReadOnlyList<ToolActionDefinition> Actions = new[]
    {
        new ToolActionDefinition("OfflineImageInfo", "Offline Windows Image", "Inspect WIM/ESD Image",
            "Prompts are intentionally not guessed: this action exports DISM usage guidance and the audit location for the supported offline workflow. Mount source/index selection remains explicit.",
            "Prepare workflow", true, false,
            CanonicalActionId: "offline.image.prepare", Impact: ToolActionImpact.MaintenanceRepair, Evidence: ToolActionEvidence.NotApplicable),
        new ToolActionDefinition("MountedImages", "Offline Windows Image", "List Mounted Images",
            "Runs DISM /Get-MountedImageInfo so stale or active mount state can be reviewed before servicing.", "List mounts", true, false,
            CanonicalActionId: "offline.image.mounts", Impact: ToolActionImpact.MaintenanceRepair, Evidence: ToolActionEvidence.NotApplicable),
        new ToolActionDefinition("CleanupMountpoints", "Offline Windows Image / RECOVERY", "Clean Stale Mount Points",
            "Runs DISM /Cleanup-Mountpoints. Use only after reviewing mounted-image state; this does not service an online Windows installation.",
            "Cleanup stale mounts", true, false,
            Confirmation: "Clean stale DISM mount points? Confirm no image servicing session that you need is active.",
            Risk: ToolActionRisk.Warning,
            CanonicalActionId: "offline.image.cleanup-mounts", Impact: ToolActionImpact.MaintenanceRepair, Evidence: ToolActionEvidence.NotApplicable)
    };

    public IReadOnlyList<ToolActionDefinition> GetActions() => Actions;
    public Task<ToolActionResult> RestoreAsync(ToolActionDefinition definition) =>
        Task.FromResult(new ToolActionResult(false, "These preparation/recovery actions do not modify a selected offline image payload."));

    public async Task<ToolActionResult> RunAsync(ToolActionDefinition definition)
    {
        if (!WindowsPrivilegeService.IsAdministrator()) return new(false, "Administrator rights are required.");
        if (definition.Id == "OfflineImageInfo")
        {
            string dir = Path.Combine(AppDataPaths.LocalRoot, "Logs", "OfflineImage");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "SUPPORTED-WORKFLOW.txt");
            string text =
                "Supported offline-image sequence\r\n" +
                "1. Inspect source: DISM /Get-WimInfo /WimFile:<install.wim-or-esd>\r\n" +
                "2. Create an empty mount directory.\r\n" +
                "3. Mount the explicitly selected index with DISM /Mount-Image.\r\n" +
                "4. Inventory packages/features/capabilities against /Image:<mount>.\r\n" +
                "5. Export a package list before removals.\r\n" +
                "6. Apply only explicitly selected servicing operations.\r\n" +
                "7. Re-run inventory and DISM health checks.\r\n" +
                "8. Commit only after verification, otherwise /Unmount-Image /Discard.\r\n\r\n" +
                "The utility does not guess a WIM/ESD path, edition index, package identity or removal list.\r\n" +
                "Deep WinSxS file deletion and manual DriverStore folder deletion are outside the supported workflow.";
            await File.WriteAllTextAsync(path, text, new UTF8Encoding(false));
            return new(true, "Offline servicing workflow written to: " + path);
        }

        string[] args = definition.Id switch
        {
            "MountedImages" => new[] { "/English", "/Get-MountedImageInfo" },
            "CleanupMountpoints" => new[] { "/English", "/Cleanup-Mountpoints" },
            _ => throw new InvalidOperationException(definition.Id)
        };
        NativeCommandResult result = await _runner.RunAsync("dism.exe", args, TimeSpan.FromMinutes(10));
        return result.ExitCode == 0 && !result.TimedOut
            ? new(true, result.StandardOutput.Trim())
            : new(false, result.CombinedOutput);
    }
}
