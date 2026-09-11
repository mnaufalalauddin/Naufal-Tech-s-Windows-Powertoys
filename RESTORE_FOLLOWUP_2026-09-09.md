# Advanced catalog Restore follow-up — 2026-09-09

## Scope and evidence

The supplied screenshot reports 24/29 restored, with five failed rows. The
separate 33/41 availability badge is already working. None of these five
failures has been relabeled as missing hardware or hidden as a success.

Read-only diagnostics on this PC, followed by a read-only probe using the new
production helpers, established the following:

| Row | Evidence | Source correction |
| --- | --- | --- |
| Teredo Tunneling | `netsh show state` reports an operationally disabled/offline tunnel, but both PowerShell configuration stores report Default. Native WMI also returns ActiveStore Type=0. | Read numeric `MSFT_NetTeredoConfiguration.Type`, not localized `netsh show state` text. Disabled=4, Default=0. Both Restore routes require Default specifically; another enabled mode is not accepted as the default. Missing/ambiguous/invalid/provider-failed reads remain errors. This verifies configuration, not Internet reachability or tunnel health. |
| Intel Management Engine JHI Service | The reference app's JSON contains an exact snapshot for `jhi_service`: Start=2, delayed-start absent, WasRunning=true. Native Restore did not read it. | When a service has neither a native capture nor a documented default, read and strictly validate its exact reference snapshot. Restore Automatic and require Running. No guessed vendor defaults. |
| Network Data Usage Monitoring | The same JSON contains `Ndu`: Start=2, delayed-start absent, WasRunning=true. The installed target is a kernel driver currently disabled. | Use the actual saved values, not a guessed driver default. Restoring must start it and verify Running; a real start failure/reboot requirement still fails verification. Boot/System-only modes are not newly allowed or synthesized. |
| Distributed Link Tracking | `TrkWks` is already Start=2 / Running, with no DelayedAutoStart value. The Restore code still requested write access to delete that absent value, and unnecessarily reconfigured already-correct startup. | Skip redundant SCM configuration and registry writes, but continue required Start/Stop and runtime verification. A necessary denied write, wrong value/type, or failed read-back still fails and retains the backup. The no-op guard also applies to companion registry fields and the documented-default deletion path. |
| Windows Recommendations / Store search | Saved and actual DACLs match. The old full-descriptor comparison also included owner/group/SACL bookkeeping. Reading all sections required SeSecurityPrivilege unnecessarily. | Capture, restore and verify only DACL permissions, the section this feature modifies. Legacy full SDDL is safely reduced to its DACL. Verify ordered ACEs, masks, SIDs, inheritance flags and protection; preserve unrelated ownership/audit rules. Skip writing an already-matching DACL. A denied pre-read can still attempt the saved DACL without taking ownership; the final read-back must succeed. |

The original script's Teredo Restore sets `netsh interface teredo set state
default`; it does not promise an active tunnel. Its service snapshot fields are
`RequestedName`, `Name`, `Start`, `DelayedAutoStart`, and `WasRunning` under
`__KentangSvc_<service>` in the previous app's local JSON.

## Snapshot safety

- The original JSON and original EXE/PS1 are never executed or modified.
- Native captures take precedence; partial native data is not silently replaced.
- JSON reads are capped at 8 MiB / depth 32. Service identity, duplicate fields,
  numeric kinds, supported startup values, delayed-start and running state are
  validated before any service changes.
- A separate per-entry SHA-256 receipt is written only after the complete group
  passes restoration verification. Unrelated changes elsewhere in the JSON do
  not resurrect an already-consumed snapshot. A failed attempt remains retryable.
  Repeating a successful Restore only verifies the consumed snapshot: unchanged
  state passes without another mutation; drift is reported instead of replaying
  stale values. A fresh native capture takes precedence for subsequent operations.
- Store capture now flushes Path/SDDL before writing the Captured marker. Existing
  invalid captures block Apply instead of overwriting the original permissions.
- No ownership takeover, global ACL reset, permission elevation or guessed service
  configuration was added to the repair workflows.

## Verification

- Functional regression suite: **1,735 assertions passed** (106 added here).
- Localization suite: **93,224 assertions passed across 23 languages**. This is
  regression coverage, not a new certification of every long description.
- Publish-stage validation: **5 assertions passed**.
- The new `--restore-read-probe` uses production helpers and reports:
  - Teredo native configured type: Default.
  - Ndu: saved Start=2, Delayed=absent, WasRunning=True.
  - jhi_service: saved Start=2, Delayed=absent, WasRunning=True.
  - Store saved/current DACL matches: True.
- Unit tests include ambiguous CIM rows, all eight type values, corrupt/missing/
  duplicate JSON fields, consumed/changed snapshot identity, genuine ACL
  mismatches, protected/empty/null DACL distinctions, necessary denied writes,
  redundant writes, and unchanged required runtime verification. Integration
  source guards check the production call sites.

### Completed artifacts

- Debug build: **0 errors, 0 warnings**.
- Native AOT publish (including native code generation) and Inno Setup compile
  both completed successfully. No native executable or installer was launched.
- Final stage: `artifacts/publish/win-x64-20260909-015841-073`.
- Application: `Naufal Windows Powertoys.exe`, 19,214,848 bytes.
  SHA-256: `12A7B41D8634500389F7F18FB358DA8E3BDCDC82329F34539679671F130D419C`.
- Installer: `artifacts/installer/Naufal-Windows-Powertoys-Setup-7.8.0-x64.exe`,
  36,716,866 bytes.
  SHA-256: `1C636B8C4FFEA4DD11EC2CE4D45CA71A7705D7D2D5E3268E9160132810F515E1`.
- Both report FileVersion 7.8.0.0, Company `Naufal Tech's Softwares`, and
  Authenticode `NotSigned`. The fixed-name installer was regenerated; previous
  timestamped publish stages were preserved. Use the final stage listed above.

## Limits / next real-device check

No native main application, original executable/script, installer, Restore,
Apply, Windows repair, service change, registry change or ACL mutation was run
during this follow-up. Only test-owned files/processes and build artifacts were
written. Therefore the five full mutating workflows are **not yet live-tested**.
In particular, Ndu/JHI must actually reach Running when the user restores them;
the code will not substitute Automatic for a required Running verification.
The earlier native crash, native UI layout checks and full 1:1 parity remain open.

Primary API references consulted: [netsh interface](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/netsh-interface),
[security descriptor string format](https://learn.microsoft.com/en-us/windows/win32/secauthz/security-descriptor-string-format).
Teredo enum values were additionally checked against the installed Windows
NetworkTransition `MSFT_NetTeredoConfiguration.cdxml`, not guessed from labels.
