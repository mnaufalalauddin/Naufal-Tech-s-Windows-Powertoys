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
    private const string BackupRoot = @"Software\Naufal Windows Tech\Powertoys\Backups\SecurityMitigations";

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

    public async Task<ToolToggleState> ReadStateAsync(ToolToggleDefinition definition)
    {
        try
        {
            (bool configured, string actual, bool locked) = ReadConfigured(definition.Id);
            DeviceGuardRuntime? runtime = definition.Id == "LsaProtection"
                ? null
                : await Task.Run(ReadDeviceGuardRuntime).ConfigureAwait(false);

            bool isOn = configured;
            ToolEffectiveState effective = ToolEffectiveState.Unknown;
            string runtimeText = "effective runtime state unavailable";
            if (runtime is DeviceGuardRuntime deviceGuard)
            {
                (isOn, effective, runtimeText) = definition.Id switch
                {
                    "VBS" => (deviceGuard.VbsStatus == 2,
                        deviceGuard.VbsStatus == 2 ? ToolEffectiveState.Active : ToolEffectiveState.Inactive,
                        $"VBS runtime={deviceGuard.VbsStatus}"),
                    "HVCI" => (deviceGuard.Running.Contains(2u),
                        deviceGuard.Running.Contains(2u) ? ToolEffectiveState.Active : ToolEffectiveState.Inactive,
                        $"HVCI runtime={(deviceGuard.Running.Contains(2u) ? "running" : "not running")}"),
                    "CredentialGuard" => (deviceGuard.Running.Contains(1u),
                        deviceGuard.Running.Contains(1u) ? ToolEffectiveState.Active : ToolEffectiveState.Inactive,
                        $"Credential Guard runtime={(deviceGuard.Running.Contains(1u) ? "running" : "not running")}"),
                    _ => (configured, ToolEffectiveState.Unknown, runtimeText)
                };
            }

            string detail = actual + "; " + runtimeText +
                (locked ? "; physical-presence removal may be required" : "");
            return new ToolToggleState(isOn, true, detail, EffectiveState: effective);
        }
        catch (Exception exception)
        {
            return new(false, false, "Unable to read", exception.Message);
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

        if (!targetOn &&
            (definition.Id is "CredentialGuard" or "LsaProtection") &&
            before.ActualValue.Contains("UEFI lock configured", StringComparison.Ordinal))
        {
            return new(false, false,
                "This protection is configured with UEFI lock. Use the Microsoft physical-presence removal procedure; Naufal Windows Utility will not bypass or automate that lock.",
                before);
        }

        try
        {
            CaptureOriginal(definition);
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
            bool configMatches = ReadConfigured(definition.Id).Configured == targetOn;
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

    public async Task<ToolToggleOperationResult> RestoreOriginalAsync(ToolToggleDefinition definition)
    {
        if (!WindowsPrivilegeService.IsAdministrator())
        {
            ToolToggleState denied = await ReadStateAsync(definition);
            return new(false, false, "Administrator rights are required.", denied);
        }
        using RegistryKey? backup = Registry.CurrentUser.OpenSubKey(BackupRoot + "\\" + definition.Id);
        if (backup is null)
        {
            ToolToggleState state = await ReadStateAsync(definition);
            return new(false, false,
                "No captured pre-change state exists. A universal Windows security default will not be guessed.",
                state, OriginalBackupMissing: true);
        }

        string path = Convert.ToString(backup.GetValue("Path"), CultureInfo.InvariantCulture) ?? "";
        string name = Convert.ToString(backup.GetValue("Name"), CultureInfo.InvariantCulture) ?? "";
        bool existed = Convert.ToInt32(backup.GetValue("Existed", 0), CultureInfo.InvariantCulture) == 1;
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(name))
        {
            ToolToggleState invalid = await ReadStateAsync(definition);
            return new(false, false, "The captured mitigation snapshot is invalid.", invalid);
        }
        using (RegistryKey key = Registry.LocalMachine.CreateSubKey(path, writable: true))
        {
            if (existed) key.SetValue(name, Convert.ToInt32(backup.GetValue("Value", 0), CultureInfo.InvariantCulture), RegistryValueKind.DWord);
            else key.DeleteValue(name, throwOnMissingValue: false);
        }
        ToolToggleState configured = await ReadStateAsync(definition);
        ToolToggleState pending = configured with
        {
            EffectiveState = ToolEffectiveState.PendingReboot,
            ActualValue = configured.ActualValue + "; original configuration restored; pending reboot/effective-state verification"
        };
        Registry.CurrentUser.DeleteSubKeyTree(BackupRoot + "\\" + definition.Id, throwOnMissingSubKey: false);
        return new(true, false, $"{definition.Name} original configuration was restored. Effective state must be verified after restart.",
            pending, VerificationPending: true, RebootRequired: true);
    }

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

    private static (bool Configured, string Actual, bool Locked) ReadConfigured(string id) => id switch
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
        _ => throw new InvalidOperationException($"Unknown mitigation: {id}")
    };

    private static DeviceGuardRuntime ReadDeviceGuardRuntime()
    {
        DeviceGuardRuntime? found = null;
        NativeRscReader.Visit((_, instance) =>
        {
            uint vbs = uint.TryParse(NativeRscReader.ReadValue(instance, "VirtualizationBasedSecurityStatus"),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out uint value) ? value : 0;
            uint[] running = NativeRscReader.ReadUInt32Array(instance, "SecurityServicesRunning");
            uint[] configured = NativeRscReader.ReadUInt32Array(instance, "SecurityServicesConfigured");
            found = new DeviceGuardRuntime(vbs, running, configured);
        }, @"ROOT\Microsoft\Windows\DeviceGuard", "SELECT * FROM Win32_DeviceGuard");
        return found ?? throw new InvalidOperationException("Win32_DeviceGuard returned no status row.");
    }

    private readonly record struct DeviceGuardRuntime(uint VbsStatus, uint[] Running, uint[] Configured);

    private static void CaptureOriginal(ToolToggleDefinition definition)
    {
        string backupPath = BackupRoot + "\\" + definition.Id;
        using RegistryKey backup = Registry.CurrentUser.CreateSubKey(backupPath, writable: true);
        if (Convert.ToInt32(backup.GetValue("Captured", 0), CultureInfo.InvariantCulture) == 1) return;
        (string path, string name) = Target(definition.Id);
        int? value = ReadDword(path, name);
        backup.SetValue("Path", path, RegistryValueKind.String);
        backup.SetValue("Name", name, RegistryValueKind.String);
        backup.SetValue("Existed", value.HasValue ? 1 : 0, RegistryValueKind.DWord);
        if (value.HasValue) backup.SetValue("Value", value.Value, RegistryValueKind.DWord);
        backup.SetValue("Captured", 1, RegistryValueKind.DWord);
    }

    private static (string Path, string Name) Target(string id) => id switch
    {
        "VBS" => (DeviceGuard, "EnableVirtualizationBasedSecurity"),
        "HVCI" => (Hvci, "Enabled"),
        "CredentialGuard" => (Lsa, "LsaCfgFlags"),
        "LsaProtection" => (Lsa, "RunAsPPL"),
        _ => throw new InvalidOperationException($"Unknown mitigation: {id}")
    };

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
