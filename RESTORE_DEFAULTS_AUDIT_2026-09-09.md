# Restore defaults and cross-catalog audit — 2026-09-09

Release metadata: **8.0.0.0**, Company/Publisher display name **Naufal Tech's Ltd.**.
Executable remains `Naufal Windows Powertoys.exe`. Installer release identifier is
`8.0.0`, with four-part Windows file/product version `8.0.0.0`.
Stable Inno AppId and MSIX certificate identity are preserved; changing display
metadata does not digitally sign either executable.

## Request and safety boundary

Audited Restore paths outside the previous five-row Advanced follow-up, and
implemented fallback when a backup is genuinely absent. **Absence is not the same
as a failed read, an incomplete snapshot, or a failed restore.** No actual Windows
Restore/Apply, service commands, DNS changes, MTU changes, package operations,
installer execution, or native application launch were used for verification.

## Corrections

| Area | Finding and correction |
| --- | --- |
| Shared catalog runner | English error-string matching could authorize a default reset after a failed snapshot read. The runner now requires a typed `OriginalBackupMissing` result; backends that handle fallback themselves mark it handled. |
| Performance Lab (Gaming/Essential/Advanced) | Saved state was compared against the inverse ON/OFF state, and backups were deleted before read-back. Full snapshot target/count/type/value preflight, exact read-back and delayed backup deletion now replace that logic. A saved active value is a valid original state. |
| Performance Lab defaults | The old default routine deleted every target, including potentially a driver service's `Start`. Replaced with a verified-target allowlist. Unknown/vendor settings do not receive guessed values. |
| Gaming BCD | Already-absent BCD values could fail on non-English Windows. Read-before-write plus typed read-back avoids unnecessary commands and English error matching; boolean aliases are normalized. Missing backups use the existing Windows-default path. |
| Essential | Complete required-tag preflight replaces checking only markers that happened to exist. Service startup is configured through SCM before registry verification. Saved Stopped is restored for Location as well as Running. Classic-context Restore no longer recursively deletes a whole COM registration subtree. |
| Windows AI / Xbox | Registry restores no longer silently skip missing markers. They verify each saved value. Service restore checks startup and actually waits for required Running/Stopped. AI capture uses native stable service state instead of parsing localized `sc` output. Xbox package snapshot commit is last, and parallel arrays/allowed package names are validated before restore. |
| Imported backups | Gaming/Performance Lab/low-risk Debloat import read and JSON failures are propagated instead of silently selecting defaults. Failed imports no longer delete an existing or partially written backup. |
| Other registry catalogs | Existing-but-empty/incomplete backups cannot fall through to defaults. Shared registry restore avoids requesting write access when a protected value is already correct. |
| DNS | Exact server order and absence of static override are checked, rather than subset membership. Missing backup falls back to DHCP for active applicable physical adapters. Failed commands no longer trigger a blanket registry rewrite afterward. |
| USB / Ethernet / Wi-Fi | Snapshot schema, count, allowed path/value scope, duplicates and decoding are checked before writes. Already-correct values are not rewritten; missing device keys are not recreated. Backups remain when devices are missing or writes fail. |
| Lock Pages in Memory | Invalid/missing account fields no longer become an empty list accidentally. Real backup absence uses the documented no-assigned-accounts default; saved account tokens are validated before `secedit`. |
| MTU | New snapshots include adapter GUID. Restore resolves current identity/name/index and verifies actual MTU. Legacy snapshots require both old alias and index to match. Missing adapters retain backups and are neutral, not successful restores. |
| Storage Sense / Reserved Storage | Saved registry values are verified, task snapshots are validated, task changes require read-back, and saved reserved-storage state requires successful DISM/read-back. Saved disabled states are not falsely rejected for still being OFF. |
| Progress | Unavailable-only action outcomes now use the existing gray unavailable presentation instead of a red failure or green verified bar. Actual failures remain red. |

## Default sources and applicability

These are narrowly scoped defaults, **not a whole-PC factory reset**. Existing
domain policies, OEM choices and driver defaults can differ or reapply later.

- BCD test overrides: remove the specific boot override and verify absence, as
  described by [Microsoft BCDEdit /deletevalue](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/bcdedit--deletevalue).
  The existing boot-menu timeout fallback remains 30 seconds; this is a conventional
  baseline/reference behavior, not recovery of a lost user timeout.
- DNS: reset static IPv4 selection to DHCP, consistent with
  [Microsoft Set-DnsClientServerAddress / ResetServerAddresses](https://learn.microsoft.com/en-us/powershell/module/dnsclient/set-dnsclientserveraddress).
  This does not promise any particular DNS server IP or change IPv6 configuration.
- UAC secure desktop: explicit `PromptOnSecureDesktop=1`, from
  [Microsoft UAC settings](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/user-account-control/settings-and-configuration).
- TDR: remove only the `TdrDelay` test override, returning to the documented driver
  default. [Microsoft TDR registry keys](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/tdr-registry-keys)
  lists a two-second default and identifies these keys as debugging controls.
- Win32 long paths: restore the disabled opt-in baseline. Microsoft describes the
  required `LongPathsEnabled=1` opt-in and per-application manifest requirement in
  [Maximum Path Length Limitation](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation).
  This is an opt-in reset, not an assertion about OEM/domain policy choices.
- Lock pages: restore no assigned accounts when no snapshot exists, consistent
  with [Microsoft Lock pages in memory](https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-10/security/threat-protection/security-policy-settings/lock-pages-in-memory).
  This can affect software that had configured the privilege independently; a
  valid backup always takes priority.
- Existing Gaming defaults return the controlled override to Windows/driver
  selection. Game DVR fallback now removes this program's explicit capture
  overrides instead of forcing background recording on. MPO vendor recovery is
  also described by [NVIDIA support](https://nvidia.custhelp.com/app/answers/detail/a_id/5157).
- Existing per-service/default tables are retained. SysMain, consumer-feature policy,
  and Delivery Optimization can use their implemented default path when the
  Essential backup key is absent. This does **not** assert every Essential option
  has a universal Microsoft-documented fallback.

## Explicit remaining limits

- Most experimental Performance Lab and GPU/vendor registry controls still require
  their saved original state. Only the UAC, TDR, and long-path Performance Lab
  fallback targets were allow-listed in this pass. The old blanket-delete fallback
  was intentionally removed.
- USB/NIC power and advanced-property defaults are driver-specific; a missing
  backup produces an actionable explanation. Microsoft's
  [Reset-NetAdapterAdvancedProperty](https://learn.microsoft.com/en-us/powershell/module/netadapter/reset-netadapteradvancedproperty)
  is a driver-default mechanism, but is **not implemented** as an automatic fallback
  here. No fabricated registry values are substituted.
- MTU is not universally 1500; missing MTU backups remain unsupported. Old MTU
  snapshots lack GUIDs, so renamed/reindexed legacy adapters cannot always be
  recovered automatically. Historical NIC class-key snapshots also lack durable
  hardware identity; scope validation is not proof the physical adapter is unchanged.
- Storage Sense missing-backup fallback clears its master override but does not
  reconstruct missing historical task settings. Removed AI feature/app payloads
  are still not automatically reinstalled. Xbox package recovery can still require
  Store/network access. These are not certified 1:1 with the reference executable.
- Late hardware disappearance, failed service starts, corrupt backups, protected
  ACLs, unavailable packages, and real install/upgrade behavior require isolated
  Windows-machine/VM testing. No claim of exhaustive native-runtime success is made.

## Verification

- Functional regression harness: **1,815 assertions passed** (pure/fake-state
  restore plans, exact comparisons, schema failures, BCD aliases, service runtime
  transitions, DNS order/default detection, and unavailable action outcome).
- Existing localization suite: **93,224 assertions across 23 languages passed**.
  This checks resource consistency; it is not a visual/linguistic audit of every
  newly added backend diagnostic sentence.
- Publish-stage regression: **5 assertions passed**.
- Debug build: **0 errors, 0 warnings** before final packaging.
- Final Native AOT publish and Setup packaging completed. Functional regression
  harness rerun after packaging: **1,815 assertions passed**, no Windows changes.

## Packaged release

- Completed publish stage: `artifacts/publish/win-x64-20260909-032947-951`.
- Application: `Naufal Windows Powertoys.exe`, 19,645,952 bytes.
  SHA-256: `E9A2787DD4E070A756807950EBBABB8E5F7D2632CEA08ACF0077FC8F2B45C739`.
- Installer: `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`,
  39,077,688 bytes.
  SHA-256: `55701108D8ED8768380B6DC6B4CD79D01E61FB856C6CEDABAE584660594C0958`.
- Both binary resources read back FileVersion / ProductVersion **8.0.0.0**
  and CompanyName **Naufal Tech's Ltd.**
- Both Authenticode results are **NotSigned**. Changing publisher display metadata
  does not create a digital signature. App identity and installer AppId were kept.
- The supplied branding image was converted without cropping to a 10-size ICO
  (16 through 256 pixels), embedded in both binaries, and copied beside the
  unpackaged executable for main and tool-window title bars. Seven package logo
  sizes are derived from the same image. **88 static icon assertions passed**.
- Binaries were inspected, not launched. Installation, startup, and real system
  mutations remain unverified in this audit.
