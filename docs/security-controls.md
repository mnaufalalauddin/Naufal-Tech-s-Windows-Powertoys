# Security & Mitigations

Open **Windows Security → Security status / LSA protection**. Results appear inline in the main window, with Copy and Save TXT. Opening/reloading is read-only. The retained LSA Enable action still requires confirmation and the shared mutation lock; it does not change other protections.

## Memory integrity / HVCI

HVCI Enable, Disable and exact-snapshot Restore were removed from the application
on **1 October 2026**. The report still distinguishes registry configuration from
running DeviceGuard protection. Use **Open Windows Security** for configuration
and driver compatibility review; the utility never forces policy or firmware
restrictions aside.

Existing HVCI snapshot files are left untouched. No setting is restored, deleted
or changed merely because these controls were removed. Historical VM harnesses
and tests may still reference the former controls; these are not UI entry points.

Management evidence uses domain membership and native MDM/Entra registration
queries. Legacy enrollment registry contents alone are not proof of management.
Unknown results remain unknown; no enrollment or policy record is removed.

## Local Security Authority protection

The additional control configures LSA protection without adding a firmware lock on eligible Windows 11 22H2+ clients. It never replaces a firmware-lock configuration. Windows 10 remains read-only for this control. Organization/policy evidence and unknown states block changes.

The report separates `RunAsPPL` configuration from live LSASS process-protection metadata. The query uses limited-information access, verifies the Windows System32 image path, and never reads process memory. Access denial is Unknown, not Disabled.

Enable requires explicit acknowledgement of authentication plug-in compatibility risk. The first original value, including absence, is saved durably before changing configuration. LSA and HVCI snapshots have separate files and control identifiers; one cannot be used for the other. Restart and reload are necessary to inspect the effective result.

**Automatic LSA Disable/Restore is unavailable.** Local registry evidence cannot establish that an earlier firmware lock is absent. An original snapshot is retained for administrator-led recovery, not advertised as an automatically reversible operation. This control does not modify firmware, Secure Boot, TPM, BCD, credentials, or generic CPU mitigation masks.

## Verification and limits

`Tests/SecurityProtection` covers mock enable, denied/unknown/stale preflight, foreign snapshots, snapshot persistence failures, repeated Apply, and configuration/effective distinctions. These tests do not Apply anything to the host. VM restart, sign-in compatibility and live protection verification remain separate acceptance requirements; mock success is not evidence of deployment testing.

`Tests/BacklogModules` also covers legacy-registry false positives, API failure,
domain/MDM/Entra registration, workplace-only ambiguity, HRESULT decoding, all
remaining blockers and direction-specific Restore eligibility. The updated suite
passes 103 assertions (33 added); no security writes or real DISM commands run.
The current application has no separate security-analysis window or HVCI action.
Live LSA mutation/sign-in testing remains outside these regression checks.

## Microsoft references

- [LSA configuration, compatibility and effective verification](https://learn.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/configuring-additional-lsa-protection)
- [Process protection metadata](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/ns-processthreadsapi-process_protection_level_information)
- [GetProcessInformation access requirements](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocessinformation)
- [Memory integrity configuration](https://learn.microsoft.com/en-us/windows/security/hardware-security/enable-virtualization-based-protection-of-code-integrity)
- [Read MDM registration without collecting a UPN](https://learn.microsoft.com/en-us/windows/win32/api/mdmregistration/nf-mdmregistration-isdeviceregisteredwithmanagement)
- [Read Entra join information](https://learn.microsoft.com/en-us/windows/win32/api/lmjoin/nf-lmjoin-netgetaadjoininformation)
- [Device join versus work-account registration](https://learn.microsoft.com/en-us/windows/win32/api/lmjoin/ne-lmjoin-dsreg_join_type)
- [HRESULT success and S_FALSE](https://learn.microsoft.com/en-us/windows/win32/learnwin32/error-handling-in-com)
