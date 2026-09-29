using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

// Advanced protection configuration. These rows describe protection state:
// ON means protection is configured active; OFF means configured inactive.
// Reboot-sensitive writes are never reported as effective until a later boot.
internal sealed class SecurityMitigationsService : IToolToggleService
{
    private const string DeviceGuard = @"SYSTEM\CurrentControlSet\Control\DeviceGuard";
    private const string Hvci = DeviceGuard + @"\Scenarios\HypervisorEnforcedCodeIntegrity";
    private const string Lsa = @"SYSTEM\CurrentControlSet\Control\Lsa";

    private static readonly IReadOnlyList<ToolToggleDefinition> Definitions = new[]
    {
        Protection("VBS", "Virtualization-Based Security (VBS)",
            "Controls the VBS platform configuration. Hyper-V, WSL2, Windows Sandbox, Credential Guard and Memory Integrity can depend on virtualization; disabling VBS is not presented as a universal performance boost."),
        Protection("HVCI", "Memory Integrity / HVCI",
            "Controls Hypervisor-Enforced Code Integrity (Memory Integrity). This is a VBS security feature; performance impact is workload and hardware dependent."),
        Protection("CredentialGuard", "Credential Guard",
            "Controls Credential Guard configuration. UEFI-locked deployments require Microsoft's physical-presence removal procedure and are not force-disabled by this utility."),
        Protection("LsaProtection", "LSA Protection",
            "Controls protected-process protection for LSASS. UEFI-locked LSA protection is not force-disabled by this utility.")
    };

    public IReadOnlyList<ToolToggleDefinition> GetDefinitions() => Definitions;

    public Task<ToolToggleState> ReadStateAsync(ToolToggleDefinition definition)
    {
        try
        {
            (bool enabled, string actual, bool locked) = definition.Id switch
            {
                "VBS" => ReadDword(DeviceGuard, "EnableVirtualizationBasedSecurity") is int vbs
                    ? (vbs != 0, $"EnableVirtualizationBasedSecurity={vbs}", false)
                    : (false, "EnableVirtualizationBasedSecurity=<absent/default>", false),
                "HVCI" => ReadDword(Hvci, "Enabled") is int hvci
                    ? (hvci != 0, $"HVCI Enabled={hvci}", false)
                    : (false, "HVCI Enabled=<absent/default>", false),
                "CredentialGuard" => ReadDword(Lsa, "LsaCfgFlags") is int cg
                    ? (cg != 0, $"LsaCfgFlags={cg}" + (cg == 1 ? " (UEFI lock configured)" : ""), cg == 1)
                    : (false, "LsaCfgFlags=<absent/default>", false),
                "LsaProtection" => ReadDword(Lsa, "RunAsPPL") is int ppl
                    ? (ppl != 0, $"RunAsPPL={ppl}" + (ppl == 1 ? " (UEFI lock configured)" : ""), ppl == 1)
                    : (false, "RunAsPPL=<absent/default>", false),
                _ => throw new InvalidOperationException($"Unknown mitigation: {definition.Id}")
            };
            string detail = locked ? actual + "; physical-presence removal may be required." : actual;
            return Task.FromResult(new ToolToggleState(
                enabled, true, detail, EffectiveState: enabled ? ToolEffectiveState.Active : ToolEffectiveState.Inactive));
        }
        catch (Exception exception)
        {
            return Task.FromResult(new ToolToggleState(false, false, "Unable to read", exception.Message));
        }
    }

    public async Task<ToolToggleOperationResult> SetStateAsync(ToolToggleDefinition definition, bool targetOn)
    {
        if (!WindowsPrivilegeService.IsAdministrator())
        {
            ToolToggleState denied = await ReadStateAsync(definition);
            return new(false, false, "Administrator rights are required.", denied);
        }

        ToolToggleState before = await ReadStateAsync(definition);
        if (!before.IsAvailable) return new(false, false, before.Error, before);

        if (!targetOn && definition.Id is "CredentialGuard" or "LsaProtection" &&
            before.ActualValue.Contains("UEFI lock configured", StringComparison.Ordinal))
        {
            return new(false, false,
                "This protection is configured with UEFI lock. Use the Microsoft physical-presence removal procedure; Naufal Windows Utility will not bypass or automate that lock.",
                before);
        }

        try
        {
            switch (definition.Id)
            {
                case "VBS":
                    WriteDword(DeviceGuard, "EnableVirtualizationBasedSecurity", targetOn ? 1 : 0);
                    break;
                case "HVCI":
                    WriteDword(Hvci, "Enabled", targetOn ? 1 : 0);
                    break;
                case "CredentialGuard":
                    WriteDword(Lsa, "LsaCfgFlags", targetOn ? 2 : 0);
                    break;
                case "LsaProtection":
                    WriteDword(Lsa, "RunAsPPL", targetOn ? 2 : 0);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown mitigation: {definition.Id}");
            }

            ToolToggleState configured = await ReadStateAsync(definition);
            bool configMatches = configured.IsAvailable && configured.IsOn == targetOn;
            ToolToggleState pending = configured with
            {
                EffectiveState = configMatches ? ToolEffectiveState.PendingReboot : ToolEffectiveState.Unknown,
                ActualValue = configured.ActualValue + (configMatches ? "; pending reboot/effective-state verification" : "")
            };
            return new ToolToggleOperationResult(
                configMatches,
                Verified: false,
                configMatches
                    ? $"{definition.Name} configuration was written and read back. Effective protection state must be verified after restart."
                    : $"Configuration read-back did not match the requested protection state. Actual: {configured.ActualValue}",
                pending,
                VerificationPending: configMatches,
                RebootRequired: configMatches);
        }
        catch (Exception exception)
        {
            ToolToggleState state = await ReadStateAsync(definition);
            return new(false, false, exception.Message, state);
        }
    }

    public Task<ToolToggleOperationResult> RestoreOriginalAsync(ToolToggleDefinition definition) =>
        Task.FromResult(new ToolToggleOperationResult(
            false, false,
            "No pre-change snapshot exists for this new advanced control yet. Use the explicit protection state selector rather than assuming a Windows default.",
            new ToolToggleState(false, true, "Restore snapshot unavailable"),
            OriginalBackupMissing: true));

    public Task<ToolToggleOperationResult> RestoreWindowsDefaultAsync(ToolToggleDefinition definition) =>
        Task.FromResult(new ToolToggleOperationResult(
            false, false,
            "Windows security defaults vary by build, hardware, enrollment and clean-install history. This utility does not invent a universal default for this protection.",
            new ToolToggleState(false, true, "No universal default")));

    private static ToolToggleDefinition Protection(string id, string name, string description) =>
        new(id, "Advanced / Security & Mitigations", name, description, true, true,
            ToolToggleTier.Advanced,
            Warning: "Changing this protection can materially reduce Windows security. Performance benefit is not assumed or quantified.",
            IsFeatureSwitch: true,
            CanonicalActionId: "security.mitigation." + id.ToLowerInvariant(),
            Impact: ToolActionImpact.AdvancedSecurityMitigation,
            Evidence: ToolActionEvidence.MechanismUnmeasured);

    private static int? ReadDword(string path, string name)
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(path, writable: false);
        object? value = key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value is null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static void WriteDword(string path, string name, int value)
    {
        using RegistryKey key = Registry.LocalMachine.CreateSubKey(path, writable: true);
        key.SetValue(name, value, RegistryValueKind.DWord);
    }
}
