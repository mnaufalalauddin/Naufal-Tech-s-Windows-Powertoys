using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed record SecurityMitigationSnapshot(
    int WindowsBuild, bool ClientWindows, bool Administrator, string MachineIdentity,
    int? HvciEnabled, int? HvciLocked, int? VbsLocked, int? VbsStatus,
    IReadOnlyList<int>? Configured, IReadOnlyList<int>? Running, IReadOnlyList<int>? Hardware,
    bool? Managed, bool? PolicyPresent, int? CodeIntegrityPolicy, string EvidenceError)
{
    internal LsaProtectionSnapshot? Lsa { get; init; }
}

internal sealed record LsaProtectionSnapshot(int? RunAsPpl, bool? PolicyPresent, uint? EffectiveProtectionLevel, string ReadError);
internal interface ILsaProtectionPlatform
{
    SecurityMitigationSnapshot Read();
    int? ReadLsaProtection();
    void EnableLsaProtectionWithoutFirmwareLock();
}

// Protection-strengthening only. Registry values cannot prove a previous firmware lock is absent.
internal static class LsaProtectionPolicy
{
    internal static string BlockReason(SecurityMitigationSnapshot s)
    {
        if (!s.Administrator) return "Administrator rights are required.";
        if (!s.ClientWindows || s.WindowsBuild < 22621) return "Enable without firmware lock requires Windows 11 22H2 or later. Older Windows remains read-only.";
        if (string.IsNullOrWhiteSpace(s.MachineIdentity)) return "Machine identity could not be verified.";
        if (s.Managed != false || s.Lsa?.PolicyPresent != false) return "Organization management or LSA policy is present/unknown; use the policy owner.";
        if (s.Lsa is null || s.Lsa.ReadError.Length != 0) return "LSA evidence is incomplete; reload before changing protection.";
        if (s.Lsa.EffectiveProtectionLevel is not (4 or 0xfffffffe)) return "LSASS protection level is unknown or unexpected; local changes are blocked.";
        if (s.Lsa.RunAsPpl == 1) return "LSA is configured with a firmware lock. Its configuration will not be replaced.";
        if (s.Lsa.RunAsPpl is not (null or 0 or 2)) return "Unexpected LSA configuration; manual review required.";
        return "";
    }

    internal static SecurityMitigationResult Enable(ILsaProtectionPlatform platform, ISecurityMitigationBackupStore store)
    {
        var before = platform.Read();
        string blocked = BlockReason(before);
        if (blocked.Length != 0) return new(false, false, "Blocked", blocked);
        var backup = store.Load();
        if (backup is not null && (!StringComparer.Ordinal.Equals(backup.MachineIdentity, before.MachineIdentity) || backup.OriginalValue is not (null or 0 or 2)))
            return new(false, false, "Blocked", "LSA snapshot machine identity or original value does not match this control.");
        if (platform.ReadLsaProtection() != before.Lsa!.RunAsPpl)
            return new(false, false, "Blocked", "LSA configuration changed during preflight. Reload and confirm again.");
        if (before.Lsa.RunAsPpl == 2) return new(true, false, "AlreadyConfigured", "LSA protection is already configured without adding a firmware lock. Effective protection is reported separately.");
        backup ??= new(before.MachineIdentity, before.Lsa.RunAsPpl, "Prepared", DateTimeOffset.UtcNow);
        store.Save(backup with { Outcome = "Prepared:EnableLsaProtection" });
        try
        {
            platform.EnableLsaProtectionWithoutFirmwareLock();
            bool verified = platform.ReadLsaProtection() == 2;
            string outcome = verified ? "RebootRequired" : "VerificationFailed";
            store.Save(backup with { Outcome = outcome + ":EnableLsaProtection" });
            return new(verified, true, outcome, verified
                ? "LSA configuration readback verified. Restart and reload to inspect effective protection. No firmware setting was changed. Original snapshot retained for manual recovery."
                : "LSA configuration readback did not match. Original snapshot retained; running protection was not inferred.");
        }
        catch (Exception ex)
        {
            try { store.Save(backup with { Outcome = "InterruptedOrFailed:EnableLsaProtection" }); } catch { }
            return new(false, true, "Unknown", "LSA operation could not be fully verified. Original snapshot retained. " + ex.Message);
        }
    }

    internal static string Report(SecurityMitigationSnapshot s)
    {
        string configured = s.Lsa?.RunAsPpl switch { 0 => "Explicitly disabled", 1 => "Enabled with firmware-lock configuration", 2 => "Enabled without adding a firmware lock (Windows 11 22H2+)", null => "Not configured / unreadable; Windows defaults may apply", _ => "Unexpected value" };
        string effective = s.Lsa?.EffectiveProtectionLevel switch { 4 => "LSA-light protected process", 0xfffffffe => "Process is not protected", null => "Unknown (not inferred from registry)", uint level => "Other process-protection level: " + level };
        string block = BlockReason(s);
        return "\nLOCAL SECURITY AUTHORITY (LSA) PROTECTION\nLocal RunAsPPL configuration: " + configured +
            "\nEffective LSASS protection (live query): " + effective +
            "\nEnable control: " + (block.Length == 0 ? "Eligible; checked again before write." : block) +
            "\nDisable / automatic rollback: unavailable. A registry snapshot cannot establish whether an earlier firmware lock persists. Review the retained original snapshot with your administrator; no firmware-unlock or protection bypass is provided." +
            "\nLSA plug-in compatibility is not established by this scan. Review Windows CodeIntegrity events before enabling.\nRead notes: " + (s.Lsa?.ReadError is { Length: > 0 } error ? error : "None") +
            "\nSource: https://learn.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/configuring-additional-lsa-protection\n";
    }
}

internal enum SecurityMitigationAction { EnableMemoryIntegrity, DisableMemoryIntegrity, RestoreMemoryIntegrity }
internal sealed record SecurityMitigationBackup(string MachineIdentity, int? OriginalValue, string Outcome, DateTimeOffset CreatedAt);
internal sealed record SecurityMitigationResult(bool Verified, bool Changed, string Outcome, string Message);

internal interface ISecurityMitigationPlatform
{
    SecurityMitigationSnapshot Read();
    int? ReadMemoryIntegrity();
    void WriteMemoryIntegrity(int? value);
}
internal interface ISecurityMitigationBackupStore
{
    SecurityMitigationBackup? Load();
    void Save(SecurityMitigationBackup backup);
}

internal static class SecurityMitigationPolicy
{
    internal static string BlockReason(SecurityMitigationSnapshot s, bool enabling)
    {
        if (!s.Administrator) return "Administrator rights are required; no automatic elevation is attempted.";
        if (!s.ClientWindows || s.WindowsBuild < 19041) return "Unsupported Windows client/build for this in-app control.";
        if (string.IsNullOrWhiteSpace(s.MachineIdentity)) return "Machine identity could not be verified.";
        if (!string.IsNullOrEmpty(s.EvidenceError)) return "Preflight evidence is incomplete: " + s.EvidenceError;
        if (s.Managed != false || s.PolicyPresent != false) return "Managed-device or policy status is present/unknown. Use your administrator's policy controls.";
        // Absence is not proof that a persisted firmware lock is absent. Require explicit unlocked evidence.
        if (s.HvciLocked != 0 || s.VbsLocked != 0) return "UEFI-lock state is locked or unknown. This utility will not bypass a lock.";
        if (s.CodeIntegrityPolicy != 0) return "Code Integrity/App Control policy is active or unknown; use the policy owner.";
        if (s.Configured is null || s.Running is null || s.Hardware is null || s.VbsStatus is null)
            return "Win32_DeviceGuard did not return complete configured/effective state.";
        if (s.HvciEnabled is not (null or 0 or 1)) return "Unexpected Memory integrity registry value; manual review required.";
        if (enabling && (s.VbsStatus != 2 || !s.Hardware.Contains(1)))
            return "Enable requires VBS already running and hardware virtualization support. Configure prerequisites in Windows Security first.";
        return "";
    }

    internal static SecurityMitigationResult Execute(SecurityMitigationAction action, ISecurityMitigationPlatform platform, ISecurityMitigationBackupStore store)
    {
        var before = platform.Read();
        var backup = store.Load();
        if (backup is not null && (!StringComparer.Ordinal.Equals(backup.MachineIdentity, before.MachineIdentity) || backup.OriginalValue is not (null or 0 or 1)))
            return new(false, false, "Blocked", "The snapshot does not match this machine or contains an unsupported original value.");
        if (action == SecurityMitigationAction.RestoreMemoryIntegrity && backup is null)
            return new(false, false, "Blocked", "No exact snapshot exists. No Windows default is guessed.");
        int? desired = action switch
        {
            SecurityMitigationAction.EnableMemoryIntegrity => 1,
            SecurityMitigationAction.DisableMemoryIntegrity => 0,
            SecurityMitigationAction.RestoreMemoryIntegrity => backup!.OriginalValue,
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        string block = BlockReason(before, desired == 1);
        if (block.Length > 0) return new(false, false, "Blocked", block);
        if (platform.ReadMemoryIntegrity() != before.HvciEnabled)
            return new(false, false, "Blocked", "Configuration changed during preflight. Reload and try again.");
        if (before.HvciEnabled == desired)
            return new(true, false, "AlreadyConfigured", "The requested configuration is already present. Effective running state is reported separately; no write or backup overwrite occurred.");
        backup ??= new(before.MachineIdentity, before.HvciEnabled, "Prepared", DateTimeOffset.UtcNow);
        // Persist the original before ANY write; interrupted Prepared records are never treated as success.
        store.Save(backup with { Outcome = "Prepared:" + action });
        bool attempted = false;
        try
        {
            attempted = true;
            platform.WriteMemoryIntegrity(desired);
            bool verified = platform.ReadMemoryIntegrity() == desired;
            string outcome = verified ? "RebootRequired" : "VerificationFailed";
            store.Save(backup with { Outcome = outcome + ":" + action });
            return new(verified, true, outcome, verified
                ? "Configuration readback verified. Restart Windows, then reload this panel to verify effective protection. This does not claim the running protection has changed yet."
                : "Readback did not match. Original snapshot retained; effective protection is unknown. Reload before any further action.");
        }
        catch (Exception ex)
        {
            try { store.Save(backup with { Outcome = "InterruptedOrFailed:" + action }); } catch { /* Original durable Prepared snapshot remains. */ }
            return new(false, attempted, "Unknown", "The operation could not be fully verified. Original snapshot retained. " + ex.Message);
        }
    }

    internal static string Report(SecurityMitigationSnapshot s)
    {
        StringBuilder text = new();
        text.AppendLine("SECURITY & MITIGATIONS — independent protection evidence");
        text.AppendLine($"Captured {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}; Windows build {s.WindowsBuild}");
        text.AppendLine("Registry configuration is not proof of effective protection. No performance gain is assumed.");
        text.AppendLine($"VBS effective: {s.VbsStatus switch { 0 => "Not enabled", 1 => "Enabled, not running", 2 => "Running", _ => "Unknown" }}");
        text.AppendLine($"Memory integrity local configuration: {(s.HvciEnabled is null ? "Not configured (Windows/policy default may apply)" : s.HvciEnabled == 1 ? "Enabled" : s.HvciEnabled == 0 ? "Disabled" : "Unexpected value")}");
        foreach (var item in new[] { (1,"Credential Guard"),(2,"Memory integrity / HVCI"),(3,"System Guard Secure Launch"),(4,"SMM firmware measurement"),(5,"Kernel-mode hardware-enforced stack protection"),(6,"Kernel stack protection audit mode"),(7,"Hypervisor-enforced paging translation") })
            text.AppendLine($"{item.Item2}: configured={Member(s.Configured,item.Item1)}; running={Member(s.Running,item.Item1)}");
        text.AppendLine("DeviceGuard service lists can vary by Windows version; absent identifiers mean not reported configured/running, not hardware unsupported.");
        text.AppendLine($"Hardware property IDs: {(s.Hardware is null ? "Unknown" : string.Join(", ", s.Hardware))} (1 virtualization; 2 Secure Boot capability; 3 DMA; 5 NX; 7 MBEC/GMET). Capable does not mean enabled.");
        text.AppendLine($"Organization-management indicators: {Known(s.Managed)}; DeviceGuard policy present: {Known(s.PolicyPresent)}");
        text.AppendLine("Management indicators are conservative registry/domain evidence, not proof of a currently active enrollment; unresolved indicators block local changes.");
        text.AppendLine($"HVCI lock: {Lock(s.HvciLocked)}; VBS lock: {Lock(s.VbsLocked)}; Code Integrity policy: {s.CodeIntegrityPolicy switch { 0 => "Off", 1 => "Audit", 2 => "Enforced", _ => "Unknown" }}");
        text.AppendLine("CPU speculative-execution mitigations: Not measured. This panel never changes FeatureSettingsOverride masks.");
        text.AppendLine("Defender, Smart App Control and BitLocker remain in their existing independent managers. Firmware, TPM and Secure Boot are not changed here.");
        text.AppendLine("Enable/Disable here only changes the HVCI Enabled DWORD after strict preflight. Driver compatibility is not established by this scan; review incompatible drivers in Windows Security first.");
        text.AppendLine("Memory integrity helps protect kernel code. Disabling reduces that protection and is not recommended as a generic performance optimization.");
        string block = BlockReason(s, false);
        text.AppendLine(block.Length == 0 ? "Local HVCI controls: preflight eligible (checked again immediately before a write)." : "Local HVCI controls blocked: " + block);
        if (s.EvidenceError.Length > 0) text.AppendLine("Read notes: " + s.EvidenceError);
        text.AppendLine("Sources: https://learn.microsoft.com/en-us/windows/security/hardware-security/enable-virtualization-based-protection-of-code-integrity");
        text.AppendLine("https://learn.microsoft.com/en-us/windows/security/identity-protection/credential-guard/configure");
        text.Append(LsaProtectionPolicy.Report(s));
        return text.ToString();
    }
    private static string Member(IReadOnlyList<int>? values, int id) => values is null ? "Unknown" : values.Contains(id) ? "Yes" : "Not reported";
    private static string Known(bool? value) => value is null ? "Unknown" : value.Value ? "Yes" : "No";
    private static string Lock(int? value) => value switch { 0 => "Explicitly unlocked", 1 => "Locked", _ => "Unknown" };
}
