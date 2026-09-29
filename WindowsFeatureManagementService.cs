using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed class WindowsFeatureManagementService : IToolActionService
{
    private readonly NativeCommandRunner _runner = new();
    private static readonly IReadOnlyList<ToolActionDefinition> Actions = new[]
    {
        new ToolActionDefinition("CompactOsEnable", "Storage / Windows image", "Compact OS - Enable",
            "Runs compact.exe /CompactOS:always. Windows compresses eligible OS binaries; actual storage/performance effect depends on hardware and image.",
            "Enable Compact OS", true, true,
            Confirmation: "Enable Compact OS compression? This can reduce OS storage use but adds decompression work.",
            RestoreConfirmation: "Disable Compact OS compression?",
            RestoreLabel: "Disable",
            Risk: ToolActionRisk.Warning,
            CanonicalActionId: "storage.compactos.enable",
            Impact: ToolActionImpact.StorageReduction,
            Evidence: ToolActionEvidence.MechanismUnmeasured),
        new ToolActionDefinition("WindowsReEnable", "Recovery", "Windows Recovery Environment - Enable",
            "Runs reagentc /enable and verifies status. This does not delete or recreate recovery partitions.",
            "Enable Windows RE", true, true,
            Confirmation: "Enable Windows Recovery Environment?",
            RestoreConfirmation: "Disable Windows Recovery Environment? Recovery options may become unavailable until re-enabled.",
            RestoreLabel: "Disable Windows RE",
            Risk: ToolActionRisk.Warning,
            CanonicalActionId: "recovery.winre.enable",
            Impact: ToolActionImpact.MaintenanceRepair,
            Evidence: ToolActionEvidence.NotApplicable)
    };

    public IReadOnlyList<ToolActionDefinition> GetActions() => Actions;

    public async Task<ToolActionResult> RunAsync(ToolActionDefinition definition)
    {
        if (!WindowsPrivilegeService.IsAdministrator()) return new(false, "Administrator rights are required.");
        return definition.Id switch
        {
            "CompactOsEnable" => await RunAndVerifyAsync("compact.exe", new[] { "/CompactOS:always" }, "compact.exe", new[] { "/CompactOS:query" }, "Compact OS enable"),
            "WindowsReEnable" => await RunAndVerifyAsync("reagentc.exe", new[] { "/enable" }, "reagentc.exe", new[] { "/info" }, "Windows RE enable"),
            _ => new(false, "Unknown action.")
        };
    }

    public async Task<ToolActionResult> RestoreAsync(ToolActionDefinition definition)
    {
        if (!WindowsPrivilegeService.IsAdministrator()) return new(false, "Administrator rights are required.");
        return definition.Id switch
        {
            "CompactOsEnable" => await RunAndVerifyAsync("compact.exe", new[] { "/CompactOS:never" }, "compact.exe", new[] { "/CompactOS:query" }, "Compact OS disable"),
            "WindowsReEnable" => await RunAndVerifyAsync("reagentc.exe", new[] { "/disable" }, "reagentc.exe", new[] { "/info" }, "Windows RE disable"),
            _ => new(false, "Unknown action.")
        };
    }

    private async Task<ToolActionResult> RunAndVerifyAsync(string exe, string[] args, string verifyExe, string[] verifyArgs, string label)
    {
        NativeCommandResult result = await _runner.RunAsync(exe, args, TimeSpan.FromMinutes(30));
        if (result.ExitCode != 0 || result.TimedOut)
            return new(false, $"{label} command failed. {result.CombinedOutput}");
        NativeCommandResult verify = await _runner.RunAsync(verifyExe, verifyArgs, TimeSpan.FromMinutes(2));
        if (verify.ExitCode != 0 || verify.TimedOut)
            return new(false, $"{label} command returned success but verification failed. {verify.CombinedOutput}");
        return new(true, $"{label} command completed. Verification output:\n{verify.StandardOutput.Trim()}");
    }
}
