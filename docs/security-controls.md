# Security & Mitigations

This panel treats each protection independently. It does not apply a blanket security-off preset or claim performance improvements. Its Apply buttons require confirmation and the shared system-mutation lock. Opening or reloading the panel is read-only.

## Memory integrity / HVCI

Enable, Disable and exact-snapshot Restore remain guarded by management, policy, hardware and explicit unlocked-state evidence. Incomplete evidence blocks changes. A successful DWORD readback means **configuration verified, reboot required**, not that the running protection changed.

## Local Security Authority protection

The additional control configures LSA protection without adding a firmware lock on eligible Windows 11 22H2+ clients. It never replaces a firmware-lock configuration. Windows 10 remains read-only for this control. Organization/policy evidence and unknown states block changes.

The report separates `RunAsPPL` configuration from live LSASS process-protection metadata. The query uses limited-information access, verifies the Windows System32 image path, and never reads process memory. Access denial is Unknown, not Disabled.

Enable requires explicit acknowledgement of authentication plug-in compatibility risk. The first original value, including absence, is saved durably before changing configuration. LSA and HVCI snapshots have separate files and control identifiers; one cannot be used for the other. Restart and reload are necessary to inspect the effective result.

**Automatic LSA Disable/Restore is unavailable.** Local registry evidence cannot establish that an earlier firmware lock is absent. An original snapshot is retained for administrator-led recovery, not advertised as an automatically reversible operation. This control does not modify firmware, Secure Boot, TPM, BCD, credentials, or generic CPU mitigation masks.

## Verification and limits

`Tests/SecurityProtection` covers mock enable, denied/unknown/stale preflight, foreign snapshots, snapshot persistence failures, repeated Apply, and configuration/effective distinctions. These tests do not Apply anything to the host. VM restart, sign-in compatibility and live protection verification remain separate acceptance requirements; mock success is not evidence of deployment testing.

## Microsoft references

- [LSA configuration, compatibility and effective verification](https://learn.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/configuring-additional-lsa-protection)
- [Process protection metadata](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/ns-processthreadsapi-process_protection_level_information)
- [GetProcessInformation access requirements](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocessinformation)
- [Memory integrity configuration](https://learn.microsoft.com/en-us/windows/security/hardware-security/enable-virtualization-based-protection-of-code-integrity)
