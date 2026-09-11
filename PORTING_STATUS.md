# Native Porting Status

Last updated: 2026-09-12 (31 built-in app uninstall/restore actions; blank rendering remains unresolved)

## Current release: 31 built-in apps

See `BUILT_IN_APPS.md`. Advanced now adds a Review apps action without changing
its existing 41 tweaks. A dedicated review window exposes selection, uninstall,
restore and per-app Microsoft Store recovery. Restoring checks healthy current-user
registration, tries staged local registration, then uses verified Store IDs for
25 apps. Six entries use local + manual Store recovery; no guessed downloads.
No personal-data restore guarantee. No all-user/provisioning/ACL changes.
Per-app and overall progress uses a separate window; preflight/audit/readback
errors fail honestly, absent uninstall targets are gray, and deployment timeouts
stop subsequent mutations while the underlying gate remains held.

Debug 0 errors/warnings; 2,655 functional and 96,099 localization assertions pass.
Read-only native probe succeeded under the isolated sandbox account (zero targets),
not a normal-user installation/restore test. Native AOT and installer packaging
completed; 101 icon assertions and the stage's payload hashes pass.
Stage: `artifacts/publish/win-x64-20260912-000505-037`.
Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`.
App SHA256: `F3F55073358689325C0778F8DBADDB8AAF5260DD5E1C66F400E8AE125BC3A26B`.
Setup SHA256: `94DF7740E1C1A4C0E89A05C2D8AB10432BBF8C6AD36B4280046648CD204442DA`.
Both remain unsigned, version 8.0.0.0 / Naufal Tech's Ltd.; logo and locations
unchanged. The prior fixed-name Setup output was replaced; older dated publish
stages remain. No Windows apps were changed. Full parity is not certified.

## Previous release: Windows 10/11 WMI readiness

See `WMI_WIZARD_UPDATE_2026-09-11.md`. First-run no longer invokes/installs
WMIC. Steps 6/7 now read OS and RAM through the existing native WMI COM reader,
with bounded non-duplicating probes, explicit timeout/error details and strict
completion gating. Existing WMIC is untouched; no feature backend was removed.
Five new display keys cover all 23 languages. Skip/suppress and Schema 2 remain.

Verified: Debug 0 errors/warnings; 2,184 functional assertions; 94,029 localization
assertions. Actual read-only production WMI probes both pass on this Windows 11
PC. Windows 10 is covered by synthetic data only, not runtime certification.
Native AOT stage: `artifacts/publish/win-x64-20260911-193939-479`.
Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`.
Version/company/icon/storage locations unchanged; both artifacts unsigned.
No native app/installer GUI, repair or live settings migration was executed.
Reported blank rendering and full catalog runtime parity remain unresolved.

## Previous release: installation and AppData locations

See `STORAGE_LOCATION_UPDATE_2026-09-10.md`. Installer default is now
`C:\Program Files\Naufal Tech's Limited\Naufal Windows Powertoys`; changing
an existing registered installation's location requires normal uninstall first.
No uninstall was run. Company/version remain Naufal Tech's Ltd. / 8.0.0.0.

`AppDataPaths` groups app-owned Settings, Backups/LegacyV78, RuntimeCache,
Temp and crash.log under `%LOCALAPPDATA%\Naufal Windows Powertoys`.
Next-launch migration copies known legacy data without deleting originals or
overwriting destinations. First-run state and fallback Restore compatibility
are retained; Registry/ProgramData and Windows/vendor caches are unchanged.

Verified: Debug 0 errors/warnings; 2,120 functional, 93,224 localization,
18 AppData routing, 12 installer location, 80 catalog interaction, 10 publish-stage
and 101 icon assertions. Native AOT/Setup completed, 168 payload files validated.
Current stage: `artifacts/publish/win-x64-20260910-225455-440`.
Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`.
Hashes and migration boundaries are in the audit; artifacts remain unsigned.

No native app/installer or live AppData migration was executed. The user reports
blank labels/progress remain blank after moving the window. The rendering cause
is unconfirmed and not fixed by this location update; full runtime parity is
still unverified. Earlier release/installed-folder observations are historical.

## Previous release: reference buttons and catalog interaction

See `PROGRAM_BUTTON_AUDIT_2026-09-10.md`. Kept the reference's 20 Main UI
button labels/order/routes and unified flat bordered button styling across
windows. Fixed individual action exclusion during analysis/confirmation/queueing,
Restore progress context, synchronous bulk backend work on the XAML dispatcher,
and overall activity/error color/live report updates. Existing risk colors,
bottom toolbars, action backends and verification safeguards were preserved.

Verified: Debug 0 errors/warnings; 2,090 functional, 93,224 localization,
80 button/routing/progress, 10 export, 10 publish-stage and 101 icon assertions.
All 168 final payload files validated. Native AOT + Setup produced:
`artifacts/publish/win-x64-20260910-214932-569` and
`artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`.
Both are 8.0.0.0 / Naufal Tech's Ltd., NT branded and unsigned. Hashes are in
the audit. No installer or Windows mutation was executed.

The earlier candidate's Main UI rendered; catalog click testing was limited by
the app's higher integrity than the Computer Use helper. Final candidate native
UI/Apply/Restore testing and full runtime 1:1 parity remain unverified. At this
audit's read-only check, the previously supplied Program Files EXE was absent;
this audit did not uninstall or modify it. The older records below are historical.

## Previous release: catalog recovery and installed baseline

See `CATALOG_RECOVERY_AUDIT_2026-09-09.md`. The newly supplied Program Files
directory is still version 7.8.0.0 / Naufal Tech's Softwares: all 160 installed
payload/manifest files match stage `win-x64-20260909-015841-073`. It has not
received newer icon/source changes. No installation files were modified.

Restored Essential bulk selection/Apply/Restore for Icon Cache, NTFS and storage
power while preserving individual controls. Added actual before/after task
reports and Copy/Save TXT. Fixed synchronous individual action dispatch, effective
storage power reads, runtime pending-state/cryptsvc verification, corrupt
Essential snapshot handling, and elevated desktop Save dialogs across reports.

Verified: Debug 0 errors/warnings; 2,053 functional, 93,224 localization, 10
publish-stage, 10 static export-routing and 101 icon assertions. Native AOT and
Setup compiled; all 168 staged payload files validated. Latest release stage:
`artifacts/publish/win-x64-20260909-173212-481`. Setup:
`artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`.
Hashes and coverage/gaps are in the audit. Both artifacts are 8.0.0.0 /
Naufal Tech's Ltd., with silver NT branding; both remain unsigned.

No native UI/installer/repair/Apply/Restore was run. Real-device 1:1 parity is
not certified. Essential no-backup defaults, complete per-setting verification
tables, linguistic/visual coverage and native crash reproduction remain open.
Never attribute feature loss to a model switch without source history evidence.

## Previous release: silver NT branding and program audit

See `NT_BRANDING_PROGRAM_AUDIT_2026-09-09.md` and `ICON_AUDIT_2026-09-09.md`.
The latest user image `images/NT-s.png` now supplies all EXE/window/shortcut/
installer/package icon assets, preserving its aspect ratio on transparent canvases.
Fixed theme/language changes resizing tool windows, minimized/maximized geometry
baseline corruption, owner-size-based placement limits, and close-warning lifetime
exceptions. Publish-stage schema 2 now validates every payload file; packaging
also rejects stale branding assets. Old EXE-only stages require a fresh publish.

Verified: Debug 0 warnings/errors; 1,815 functional, 93,224 localization,
10 publish-stage and 101 icon assertions. Native AOT + Setup compilation passed.
All 168 staged payload files validated. Metadata remains `8.0.0.0` /
`Naufal Tech's Ltd.`; EXE and Setup are unsigned. Latest stage:
`artifacts/publish/win-x64-20260909-095927-126`. Latest installer:
`artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`.
Hashes are in the icon audit. No native app/installer/Windows mutation was run;
real UI, taskbar-pin appearance and real-device 1:1 behavior remain unverified.
The installed Program Files application was not automatically upgraded.
All release records below are historical and may reference superseded artwork.

## Previous release: Restore/default audit

See `RESTORE_DEFAULTS_AUDIT_2026-09-09.md` for corrections, Microsoft/vendor
sources, coverage and remaining unsupported defaults. Company/publisher display
name is now `Naufal Tech's Ltd.`, Windows version `8.0.0.0`; EXE name unchanged.
Backup absence is distinct from corruption/access failure. Exact Restore validates
snapshots before mutation and retains them when verification fails. DNS fallback
uses DHCP; unknown hardware/experimental defaults are not guessed.

1,815 functional assertions, 93,224 localization assertions and 5 publish-stage
checks passed. Debug builds were clean. Native Restore, app startup and installer
execution were not performed. Earlier release details below are historical.

Native AOT and Setup 8.0.0 packaging completed. The supplied Naufal Tech's
branding image is now the multi-resolution EXE, taskbar/title-bar, tool-window,
shortcut, installer, and package-asset icon. Both EXE and Setup metadata read
back `8.0.0.0` / `Naufal Tech's Ltd.`; both remain unsigned. Final icon stage:
`artifacts/publish/win-x64-20260909-032947-951`. Installer:
`artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`.
See `ICON_AUDIT_2026-09-09.md`; it records shell/shortcut coverage, the stable
taskbar AppUserModelID, existing-pin behavior, and 101 static icon assertions.
Applications/installers were not launched.
Hashes and remaining test limitations are recorded in the audit above.

## Latest Advanced catalog Restore follow-up

Evidence: `RESTORE_FOLLOWUP_2026-09-09.md`. Corrected Teredo configured-policy
verification, missing previous-app snapshot recovery for Ndu/Intel JHI, redundant
protected-service writes, and Store DACL-only restore/verification. Original
snapshots remain untouched; successful previous-snapshot restores are tracked with
per-entry receipts. Repeat Restore verifies but does not replay a consumed old
snapshot. Automatic + Running remains required when captured/required, and genuine
write/read/runtime failures are not hidden behind the gray unavailable badge.

1,735 functional assertions, 93,224 localization assertions and 5 publish-stage
checks pass. New production read-only probe confirms Default Teredo, valid old
Ndu/JHI Automatic + Running snapshots, and matching saved/current Store DACLs.
No Windows-setting mutation or native app/installer execution performed.
Debug: 0 errors/warnings. Native AOT and Setup compiled successfully. Final stage:
`artifacts/publish/win-x64-20260909-015841-073`.
EXE SHA-256: `12A7B41D8634500389F7F18FB358DA8E3BDCDC82329F34539679671F130D419C`.
Setup SHA-256: `1C636B8C4FFEA4DD11EC2CE4D45CA71A7705D7D2D5E3268E9160132810F515E1`.
Version 7.8.0.0, Company `Naufal Tech's Softwares`; both unsigned. The fixed-name
Setup was regenerated and prior publish stages retained. See the follow-up for
the untested real-device Restore workflows and outstanding 1:1/native UI limits.

## Latest whole-program audit

Evidence: `PROGRAM_REAUDIT_2026-09-09.md`. Eight defect groups corrected:
queue-notice admission blocking, first-run scheduling/lifetime/progress,
bounded unique WinGet downloads, faulty output observers blocking pipe reads,
duplicated process lifecycles, invalid/late catalog progress events, Office
installation-path discovery, and absence/error classification in catalogs.

Confirmed missing hardware/services/components now have a gray badge showing
verified/total and unavailable counts, with new resources in all 23 languages.
Unavailable is not verified/applied and not a failure. Access-denied, timeout,
missing-snapshot and failed-write outcomes remain errors; composite catalog
rows no longer silently swallow failed child reads. The independent progress
window includes the same neutral summary. See the audit for exact scope.

1,629 functional assertions, 93,224 localization assertions and 5 publish-stage
checks pass. 20/20 main-menu connections, 23 resource tables with 577 keys,
unchanged reference hashes. Debug: 0 warnings/errors. Native AOT + Setup build
successfully. No native app/installer or Windows-changing operation launched.
Old native crash, native layout/RTL/scaling validation, longer untranslated
descriptions and real-device 1:1 parity are still open, not certified by tests.

Latest completed stage: `artifacts/publish/win-x64-20260909-005946-954`.
EXE SHA-256: `103CD0EADD3C4FD3F9D3233E09D75A8AB77BADBC6D45B26D3081899FC75C7E72`.
Setup SHA-256: `B12BCD755CF52E947C22EA767A610CE81CC103B86B898C757CFB927269E98253`.
Both unsigned, version 7.8.0.0, Company `Naufal Tech's Softwares`. The fixed-name
installer was regenerated; earlier timestamped publish stages are retained.

## Previous September 7 whole-program audit

Evidence: `PROGRAM_AUDIT_2026-09-07.md`. Fixed result-window/task lifetime coupling,
SFC timeout/error classification, GPU target-version verification, repeated hung
catalog read accumulation, maintenance completion/late-progress validation, and
zero/sub-millisecond process timeouts. No Windows mutation or native app/installer
launch occurred during this audit. Read probes never retry mutations.

1,372 functional regression assertions, 92,006 localization assertions and 5
publish-stage checks pass. All 20 XAML menu routes remain connected; reference
script hash matches the project copy. Debug build: 0 warnings/errors. Native AOT
publish and Inno Setup compile successfully. These are not native UI/hardware or
full 1:1 behavioral parity tests. Prior native crash and translation gaps remain
open; see the audit coverage ledger for required VM/hardware follow-up.

Latest completed stage: `artifacts/publish/win-x64-20260907-202745-458`.
EXE SHA-256: `6E468C3D3400BA1CAF1343349043C59480FC5C54320101AE3B43EB32D8AD36FF`.
Setup SHA-256: `2101D7288580A9267D8AACB5D8881407B4DCD0688C7C733BC4BA074BD02F3E21`.
Both unsigned, version 7.8.0.0, Company `Naufal Tech's Softwares`. The same-name
installer was replaced; prior completed publish stages are retained.

## Previous language-switch follow-up

Evidence: `LANGUAGE_SWITCH_FIX_2026-09-07.md`. Fixed canonical-text accumulation
in the BitLocker status; added window-owned retention of authored control state,
cleanup of removed rows and closed windows, and shared popup/header/tooltip
traversal. Text-scale popup now belongs to its window's localization lifecycle.

92,006 localization assertions pass, including 529 language pairs using the
production adapter against XAML test doubles, dynamic updates, managed GC and
detach/reinsert/close cleanup. These are NOT native WinUI visual tests. 914 other
regression assertions and 5 publish-stage checks pass. Debug: 0 errors/warnings;
Native AOT and installer compile. Computer Use exhausted its request budget on
an attempted launch of the preceding build (outcome unknown); no language click,
Apply/Restore/repair or installer launch was performed. Actual UI verification
remains open. All prior resource-coverage gaps and the native crash remain open.

Previous completed stage: `artifacts/publish/win-x64-20260907-142857-426`.
EXE SHA-256: `ED42578D1AC38076FD7F86A25028E31512814E05BD7C68AB5F3A4E9175878BB8`.
Setup SHA-256: `FD25EEA88D15BF364D32ADFEB45CA7BFFEB26E919A66E476A91902DCAF15C860`.
Both unsigned, version 7.8.0.0, Company `Naufal Tech's Softwares`. The same-name
installer was replaced; prior publish stages are retained.

## Previous localization audit checkpoint

Evidence: `LOCALIZATION_AUDIT_2026-09-07.md`. All 23 language resources inspected;
573 merged keys per language (baseline 396), no missing keys/blank translations/
placeholder mismatches in that resource set. 80,676 localization assertions,
914 functional regressions and 5 publish-stage checks pass. Debug is clean;
Native AOT and Inno Setup compile successfully. No GUI/repair/installer launch.

Fixed canonical-text retention, dynamic display updates, two-language wizard,
English-only dates, popup/accessibility captions and RTL numeric handling.
Added shared task/progress/status text, safety warnings, 27 simplified option
names, dashboard descriptions and 22 repair-stage captions in all 23 languages.
New maintainable resources are in `NativeUiCatalog*.cs`; pure localization core
is `UiTranslationCatalog.cs` plus `UiTranslationTemplates.cs`/`UiLocalizedValue.cs`.

NOT 100% translated: longer native option descriptions, confirmations/results,
MSI/GPU/runtime/BitLocker/Process Manager guidance still need work. Inventory:
`artifacts/localization-audit/coverage.json` (2,869 literal + 683 interpolated
candidates, including intentional technical output). Actual WinUI clipping/RTL
and native-speaker review remain untested. Do not count resource entries as
proof of full app coverage. Earlier 0xc0000005 crash is not resolved by this build.

Previous completed stage: `artifacts/publish/win-x64-20260907-065449-167`.
EXE SHA-256: `ED880D7984983F976A12EA778F4447159C073829D5C98A5B15DDB3EF48022D57`.
Setup SHA-256: `67AF4C3B77CD26C7DBE76F2C46DFF594733522E0439580B706873F5CBD30CDA2`.
Both unsigned; version 7.8.0.0, Company `Naufal Tech's Softwares`. Setup replaced
the previous same-name output; historical publish stages are retained.

## Latest repair verification follow-up

Evidence: `REPAIR_VERIFICATION_FIX_2026-09-06.md`. Store reset command completion
is separated from final current-user registration/manifest/package-health checks.
The erroneous immediate same-version registration check was removed. Real reset
failures, deployment timeouts, or failed final registration still remain visible.
Final Store metadata checks retry up to six times and allow a changed version in
the same trusted Store package family.

Windows Update preserves operational Automatic/Manual modes for BITS and DoSvc;
disabled/invalid modes still need repair. Final verification now requires Running
for all repair services, not cryptsvc alone, and for UsoSvc when restarted here.
The latest user log showed successful cache backup/reset and restart; the reported
startup warnings were exact-value comparisons, not failed cache operations.

914 regression assertions + 5 staging assertions pass. Debug: 0 errors/warnings.
Generated reset PowerShell parses without execution. Native AOT/Setup compilation
succeeded. Latest hash-verified stage: `artifacts/publish/win-x64-20260906-113350-937`.
EXE SHA-256: `BEC807829B04B855EB87750998512BA75875EACCAE0F6BDD2EAB30917186D525`.
Setup SHA-256: `E44C554E61E8A021A6056AAF74E48E3E03E6C80139FE88ABAD2165B6515F5EBB`.
Both unsigned, FileVersion 7.8.0.0. Same-name Setup replaced; old publish stages
retained. No real Windows repairs or GUI/installer launches were performed.
The separate native 0xc0000005 error remains unresolved; see the prior evidence below.

## Earlier maintenance repair follow-up (same day)

Evidence: `REPAIR_FIX_2026-09-06.md`. Store Stage 3 now uses the Windows
Reset-AppxPackage cmdlet for the validated current-user Store package, replacing
the Windows App SDK reset call that returned Not implemented. A bounded shared
deployment gate retains pending external resets; timeout/pending state aborts
this repair before cache cleanup. Error/warning and reset consent wording improved.

Windows Update no longer deletes old backups or renames SoftwareDistribution's
parent. Only DataStore, Download and catroot2 are moved to unique preserved
backups. Service stop is verified before each move/retry, UsoSvc is temporarily
paused, retries are bounded and recovery verifies runtime even after cancellation.
No ACL/ownership takeover or process killing is used to force cache access.

754 regression assertions + 5 staging assertions pass; generated reset script
parses without execution. Debug: 0 errors/warnings; Native AOT/Setup compile.
Historical hash-verified stage: `artifacts/publish/win-x64-20260906-111825-307`.
EXE SHA-256: `E274067FAD88944A524A28148098588DE84E1B9D8DDBFE4E83EA6BE99E534AA1`.
Setup SHA-256: `098EECF854EC23DACFE5D41499EE69D027D3C8E36A9DE0516DA4F319DF1B26B7`.
Both unsigned; same-name Setup replaced. No real Windows repair, package reset,
service changes, GUI startup or Setup install was performed during this pass.

IMPORTANT: the separate `Exception Processing Message 0xc0000005 - Unexpected
parameters` screenshot is **not fixed/diagnosed**. User could not recall the
preceding action. No relevant event or managed crash log was found in current
read-only checks. `Collect-CrashDiagnostics.ps1` collects bounded local evidence
for a recurrence without modifying Windows settings. Do not attribute that
native fault to a repair stage or claim runtime stability without new evidence.

## Earlier profile Apply follow-up (same day)

Evidence: `PROFILE_APPLY_FIX_2026-09-06.md`. Balanced current CPU values now come
from the Windows power API, shared by snapshot/apply/live verification; absent
registry overrides no longer falsely mean an unavailable setting. Actual API
errors still block Apply. RSC now distinguishes typed integer results from
NULL/EMPTY and unsupported outputs; mandatory per-adapter IPv4/IPv6 read-back
continues to gate both Apply and rollback. The exact execution-time RSC VARIANT
in the screenshot was not captured; read-only metadata is not an execution test.

717 synthetic assertions and 5 staging assertions pass; Debug has 0 errors and
0 warnings. Native read-only probes passed for five Balanced settings and two
RSC adapters. No profile/network mutation, GUI startup or Setup install was run.
Native AOT and Inno Setup compilation succeeded. Historical hash-verified stage:
`artifacts/publish/win-x64-20260906-001404-265`.
EXE SHA-256: `232999F2D8E1BB2150AF0C831A571E4D322FF206270812564A715219AC739D59`.
Setup SHA-256: `A8F571E494648789883FF5DC0DE3BB539C78E7147B4B7F21687D6B26FBB8D77F`.
Both binaries remain unsigned. The same-name installer has been replaced.

## Whole-program audit status (2026-09-05)

Current evidence is `PROGRAM_AUDIT_2026-09-05.md`. Widgets now displays as
`Taskbar Widgets`; related catalog headings are normalized centrally without
changing operation IDs or snapshots. AppX factories are gated until outstanding
deployment cancellation is observed, and Widgets inventory has a bounded
read-only wait. Partial-state restore, per-child composite fallback, exact BCD
read-back/backup retention, first-run opt-out, MSI verification, Process Manager
task lifecycle and per-stage maintenance progress were corrected. Progress
tracks terminal FAILED/SKIPPED/NOT VERIFIED states and ignores late callbacks;
verification no longer displays a fabricated 85 percent.

20/20 menu routes and the 23-language structure were rechecked against the
reference files. UI inspection via Computer Use was blocked by UAC; no prompt
was approved and no Windows tweaks or installer were executed. Do not claim
complete visual or 1:1 functional/behavior parity. Final artifact and test results
are recorded in the current audit report.

Historical verified publish stage: `artifacts/publish/win-x64-20260905-234755-858`.
EXE SHA-256: `676F278D7A0DBAC10964DF262144685356B7FCE570CDEFE8D62D825D977F2F89`.
Setup SHA-256: `AC98A966E6228B70652C487F674B904878C38C76C6387E392420CAA56228D55B`.
615 synthetic assertions and 5 staging assertions pass; Debug has 0 errors and
0 warnings; Native AOT and Inno Setup succeed. Both binaries remain unsigned.

## Previous focused status (historical, superseded where noted above)

Gaming, Essential, and Advanced now distinguish selection from ON/OFF state,
skip already-restored rows during ordinary Restore, and fall back from a missing
native snapshot to a documented Windows default when that fallback is known.
Operation progress, elapsed time, percentages, and long failure reports have
been removed from catalog cards and footers. They now live in dedicated,
resizable task-progress windows shared by all work launched from the Main UI.
Each active item receives its own determinate Windows-style bar and status row;
the overall gray-track progress bar is fixed at the bottom. Working/success uses
Windows green and failure uses red. The native title bar and visible heading
track the current action, for example `Restoring Widgets` or
`Verifying Widgets`. Full Repair, Quick Repair, Explorer/Update/Store repairs,
catalog Analyze/Apply/Restore and direct actions, Profile Apply, Defender,
BitLocker, MSI, GPU inventory, Runtime analysis, table/security reports, and
Process Manager cleanup are connected to this model. Pure navigation windows do
not display fake progress.

Unknown-duration Windows operations no longer display a synthetic fixed 15%.
Their item bar uses the native indeterminate animation until Windows returns,
then switches to the determinate Verify/Complete state. Every AppX deployment
path used by Widgets, Windows AI, Xbox, Store repair, Explorer repair, and the
first-run App Installer setup now has a shared timeout/cancellation guard.
Ordinary package operations are limited to two minutes (first-run App Installer
uses five minutes); a non-responsive Windows deployment becomes a red failure
with actionable detail instead of blocking the catalog indefinitely.

Catalog option titles contain only the option name; category/risk is shown as a
colored badge in the description area. Active Tasks details use action verbs
such as `Applying:`, `Restoring:`, and `Analyzing:`. Gaming action headings no
longer show `ONE-SHOT`. `SKIP FOR NOW` in the first-run wizard now explicitly
persists an incomplete, non-suppressed state, so the wizard returns on the next
startup; only `DONT SHOW AGAIN` suppresses it permanently.

Known continuously-running Automatic services, including SysMain, are explicitly
started and must verify `Running`; trigger-start Automatic services are still
started but are not falsely failed if Windows immediately stops them without a
trigger. Debug x64: 0 errors and 0 warnings; 519 synthetic assertions and 5
publish-stage checks pass. Focused source inspection confirms that no catalog
card/footer progress bar remains; the only `ProgressBar` declared directly in
`MainWindow` belongs to the separate BitLocker decryption monitor. Latest
Native AOT staging:
`artifacts/publish/win-x64-20260905-162246-106`; app SHA-256:
`7B59738AD4AC4DCE6DA667C4F027171EB4AB3678CFE3FAD22F03654EE0A5A169`.
Latest installer:
`artifacts/installer/Naufal-Windows-Powertoys-Setup-7.8.0-x64.exe`; SHA-256:
`7FD817464838B28119D67E553F2F9B3AF4B9609877593D187292D2AF1B02B091`.

Latest focused evidence: `ADVANCED_DEBLOAT_AUDIT_2026-09-05.md`. The Advanced
catalog was checked against the supplied reference source: 22/22 service groups
and 33/33 documented startup defaults match. SysMain smart Restore now targets
Automatic (`Start=2`), issues a real start request, waits for Running, and
verifies its Prefetch companion state before backup cleanup. Partial multi-value
snapshots are blocked, and Advanced backends retain backups until successful
read-back. Current checks: 519 synthetic assertions and 5 packaging-stage
assertions; Debug x64 has zero
errors/warnings. No Windows mutation or elevated VM matrix was executed, so full
1:1 behavioral equivalence remains uncertified.

The previous whole-program audit remains `GENERAL_AUDIT_2026-09-04.md`.

That audit addressed nine additional finding groups:
addressed: FIFO fairness, task history/results, atomic downloads, snapshot commit
ordering, checked restore/backup retention, pending-operation gates, owned-window
shutdown/reboot guards, pre-launch cancellation, and complete publish staging.

484 regression assertions plus 5 packaging-stage assertions pass. Debug x64:
0 errors, 0 warnings. Static inspection: 20 connected routes, 23 languages with
396 entries each. Final Native AOT/Setup paths and hashes are in that report.
No Windows mutation, installer execution, or final-binary interactive UI test
was performed in this audit. Full 1:1 behavioral equivalence is NOT certified.
All evidence/artifacts below this section are historical unless explicitly
repeated in the current report.

Latest catalog Apply/Restore correction: `CATALOG_BUTTON_FIX_2026-09-04.md`.
The prior nine audit corrections are tracked in `AUDIT_FIXES_2026-09-04.md`.
`PARITY_REAUDIT_2026-09-04.md` records the pre-fix findings; earlier reports are
historical. “Implemented” does not mean administrative behavior is certified 1:1.

## Earlier corrections — F01 through F09

All nine reported findings are addressed in code: structured/fail-closed
BitLocker read-back, real Store app-data reset and all-user discovery, streaming
DISM/SFC, backend-owned per-stage statuses, supplemental translations/stable keys,
fresh MSI/NDIS details, typed disk health/media, runtime action gating, and atomic
profile transaction/rollback persistence.

Verification: 373 regression assertions passed (73 existing + 300 audit);
Debug x64 zero errors/warnings. Static catalog: 23 languages, 396 entries each;
all six previously missing sample labels now present in every language.
Disk and NDIS providers were exercised read-only. BitLocker provider denied access
on the test token; no BitLocker setter, repair, driver/runtime installer, profile
Apply or other Windows mutation was executed.

Latest final staging: `artifacts/publish/win-x64-20260904-193529` (160 files).
Native AOT/Setup artifacts and exact hashes are listed in the corrections report.
Publisher remains `Naufal Tech's Softwares`; AppId/signing identity unchanged.
Computer-use observed a Main UI window on the intermediate staging, but its
higher integrity prevented reliable content verification. No UI/runtime parity
pass is claimed. Final installer execution and VM mutation matrices remain open.

## Product target

- WinUI 3 desktop application on .NET 10.
- Native AOT, self-contained, unpackaged `win-x64` release.
- Executable name: `Naufal Windows Powertoys.exe`.
- User-facing product name: `Naufal Tech's Windows Powertoys`.
- Publisher: `Naufal Tech's Softwares` (explicit user requirement).
- The legacy PowerShell implementation is the behavioral reference only; its
  version/file name must not appear in user-facing descriptions.

## Completed foundation

- Main dashboard and resizable native tool windows.
- Light/Dark toggle and eight requested UI scales: 25, 50, 75, 100, 125,
  150, 175, and 200 percent.
- Non-cumulative font and geometry scaling from captured 100% baselines.
- Fixed pixel Grid rows/columns, margins, padding, spacing and window minimums
  now follow the damped geometry scale.
- Logical item traversal covers non-realized/virtualized report and catalog
  rows, preventing dark text from reappearing after scrolling.
- Advanced/Gaming/Essential catalogs use a full-page responsive scroll model,
  so their option rows cannot collapse to zero height at 150-200 percent.
- Catalog CheckBox and ToggleSwitch columns now respect the WinUI template
  minimum widths, preventing clipped selection squares and switch circles.
- Open tool windows resize proportionally when the display scale changes,
  preserve the user's unscaled size baseline, and remain capped to the owner
  window/screen area.
- Theme and scale preferences persist under LocalAppData.
- Shared resizable maintenance progress window with stage list, progress bar,
  stage percentage, elapsed time, stage notifications, final report, and Copy log.
  DISM/SFC output streaming and backend-owned per-stage completion status were
  subsequently implemented and covered by regression tests (F03/F04).
- Progress integration for Full Repair, Quick Repair, Windows Update Fix,
  Microsoft Store Fix, and Explorer Fix.
- GPU Driver Manager now enumerates active PCI display adapters through the
  display setup-class GUID (including Windows builds that omit the legacy
  `Class` registry string), auto-selects the first adapter, resolves only
  mapped official NVIDIA/AMD/Intel packages, enforces HTTPS host allow-lists,
  Authenticode/publisher checks and published SHA-256 where available, and
  performs post-install device/driver read-back in a six-stage progress flow.
- MSI Mode Utility now enumerates live PCI devices, IRQ resources, supported
  LineBased/MSI/MSI-X modes, hardware message maximums, driver INF defaults,
  MSISupported, MessageNumberLimit and DevicePriority. Its INF parser uses the
  correct unsuffixed Win32 `SetupFindNextLine` entry point, exposes detailed
  device information and Registry navigation, and applies only dirty values
  with read-back verification.
- Games Runtime & Compatibility now separates Official source, Download &
  install, and Repair / enable. Mapped external runtimes use a five-stage
  official download, integrity/publisher verification, silent install/repair,
  and post-install analysis workflow; Windows features/services use native
  servicing with their own verified progress flow.
- The header task indicator opens a resizable Active Tasks window backed by a
  real resource-lock scheduler. It lists RUNNING and QUEUED tasks, named
  resources, wait owner/detail, elapsed time and history at a 500 ms cadence.
  Non-conflicting/read-only tasks may run together; conflicting mutations are
  queued FIFO and start automatically after lock release. Duplicate active
  IDs are rejected, and close/reboot are blocked while the queue is active.
- Tool headers now contain only their window title. The long introductory
  definitions were removed from the shared tweak catalogs, Runtime, GPU, MSI,
  Legacy Panels, Active Tasks and maintenance-progress surfaces together with
  their former layout rows.
- Shared catalog toolbar buttons now use a consistent risk palette: green for
  safe/restore-default scopes, amber for advanced scopes, blue for neutral
  selection/analysis actions, and gray for de-selection.
- Text scaling exposes all eight requested values (25-200 percent) plus an
  explicit Reset to 100% command, while retaining a non-cumulative baseline.
- The complete 23-language selector is populated from one catalog; every
  language currently contains the same 396 combined translation keys (9,108 entries).
- Advanced De-Bloat now exposes the final 41-row ownership model from the
  behavioral source. Composite rows merge their underlying registry/actions,
  service groups retain their original risk tier, unavailable targets stay
  disabled, and Apply/Restore verifies every applicable child.
- Essential Windows Tweaks now owns only its final Essential definitions,
  one-shot actions and Performance Lab entries. Items owned by Advanced
  De-Bloat are no longer duplicated there.
- Gaming Tweaks now owns the final six manual toggles, the 38 Performance Lab
  definitions, the BCD editor, and all 15 direct gaming/device/network actions.
  Game DVR remains owned by the Xbox section in Advanced De-Bloat.
- Catalog Analyze/Apply/Restore operations open a separate responsive progress
  window with one row/bar per item, live action text, percentage and elapsed
  time, plus a bottom overall bar and scrollable final failure report.
- Disk Information now reports physical disks, logical drives and aggregate
  storage through the same native table-report surface as System Report.
- Existing Xbox package, low-risk de-bloat, Performance Lab, and manual Gaming
  snapshots are now recognized by native Restore paths. Imports are
  allow-listed against the native catalog, do not overwrite native backups,
  and use completion markers so a partial import cannot be replayed.
- Performance Profile now requires all 23 reference checks for VERIFIED and
  CURRENT PROFILE, not just MMCSS. Full verification participates in rollback.
  Network RSC Apply/Restore is per-adapter IPv4/IPv6 rather than global netsh.
- Preference migration preserves existing canonical V78 state while filling
  only missing files from the previous native folder or legacy V77 folder.

## Earlier profile audit evidence (2026-09-04)

- See `PARITY_AUDIT_2026-09-04.md` for the false-VERIFIED correction and explicit
  unverified mutation paths. 73 portable regression assertions passed.
- A read-only unelevated probe returned 21/23 because BCD was unreadable, not
  because either BCD value was proven incorrect. Both NIC RSC states were read.
- Previous UI route/theme/scale smoke results below are historical checks;
  they must not be interpreted as repeated certification of every later binary.
- Native AOT publication and Setup compilation succeeded. Current app SHA-256:
  `167E1820A821631E2874AB73E2B3798716FE78A274373512E9600581B191E36A`.
  Current Setup SHA-256:
  `A6CE43BFA8D9432381882C20EB7425F5B72BB1F089744CEEB85FA032182F69A0`.
  Staging: `artifacts/publish/win-x64-20260904-075647` (160 files).
- Current-binary UI startup testing is pending: computer-use app approval timed
  out. The tool boundary was respected. Setup was not installed. Both files are
  unsigned; see the dated audit for sizes and test limits.

## Previously recorded verification

- Debug x64 build: 0 errors, 0 warnings after the final catalog ownership,
  progress, Gaming and display-scale work.
- Source audit confirms progress stage counts match the native service flows:
  Windows Update 6, Microsoft Store 9, Explorer 7, Full Repair 2, Quick Repair 1.
- Native AOT startup smoke test is responsive with the correct product title
  and no application crash log.
- All eight display options were exercised through UI Automation. Main controls
  stayed onscreen at 25, 50, 75, 100, 125, 150, 175, and 200 percent.
- Advanced De-Bloat was tested read-only at 200 percent in Dark mode: its page
  remained scrollable, full Select/ON-OFF rows became visible while scrolling,
  and the tool window grew from 1120x753 to the 1440x753 owner limit before
  returning to 1120x753 at 100 percent.
- GPU Driver Manager read-only smoke test detected and auto-selected
  `NVIDIA GeForce RTX 4070 Ti SUPER`, read NVIDIA provider/INF/version/device
  health, and enabled the three supported actions. At Dark/200 percent its
  buttons and all realized text/list nodes remained onscreen.
- MSI Mode Utility read-only smoke test loaded 11 active PCI devices, including
  IRQ allocations and LineBased/MSI/MSI-X support. At Dark/200 percent the
  realized ToggleSwitch measured 101x30 pixels; vertically virtualized controls
  were offscreen rather than clipped, and the wide table remained horizontally
  scrollable.
- Games Runtime & Compatibility read-only smoke test returned 18 analyzed
  entries. At Dark/200 percent Official source, Analyze, Repair / enable,
  Download & install and Close remained onscreen.
- No repair, installer, driver, service, registry, profile, de-bloat or reboot
  action was executed during smoke tests.
- A full read-only Main UI route smoke test opened and safely closed Disk Info,
  System Report, Windows/Office Activation, BitLocker, Smart App Control,
  Essential/Gaming/Advanced catalogs, Legacy Windows Panels, GPU, MSI and
  Games Runtime. Full/Quick Repair, Windows Update, Store, Explorer, Defender
  disable/restore and Reboot confirmation routes were opened and cancelled.
- Active Tasks was verified with a completed Disk Info record and a live
  RUNNING Essential Tweaks record while its tool window remained open.
- The final Native AOT build was smoke-tested read-only across Active Tasks,
  Games Runtime, MSI Mode Utility, GPU Driver Manager, Legacy Panels and the
  Advanced catalog. No removed introductory header text remained, and all
  eight risk-toolbar actions were present.
- Static user-facing source scan contains no legacy script/version references.
- MSI Mode Utility was compared against its final source handler: its seven
  editable/read-only columns, details pane, refresh, dirty-only apply,
  validation, read-back and registry navigation are represented. The apparent
  legacy `Reset` gap was a stale audit error; that button belongs to the font
  scale popup, not MSI Mode Utility.
- Native AOT staging and startup smoke test passed after the final ownership and
  progress changes. The application stayed responsive for the eight-second
  startup gate; Advanced De-Bloat opened with 41 Select controls plus its one
  high-risk acknowledgement control.
- Native AOT UI Automation confirmed 23 language entries (English through
  Español) and all nine text-scale commands: eight requested percentages plus
  Reset to 100%.
- Final Native AOT read-only route smoke test opened and closed all 20 Main UI
  routes (20 succeeded, 0 failed), without invoking any Apply, repair, restore,
  install, driver, policy, registry, service, BitLocker or reboot action.
- Main dashboard status now uses the requested `🔴 LIVE GAMING STATUS` title.
  Its detail output follows the complete reference sequence: MMCSS, power plan,
  Game Mode, HAGS, Windowed Optimization, Dynamic Tick, HPET, Core Parking,
  processor policy, MPO, SysMain, Game DVR, Nagle/throttling/RSC, shader cache,
  and raw `powercfg` output. The redundant `LIVE SYSTEM SNAPSHOT` heading and
  block were removed; live hardware telemetry remains in the ten summary cards.
- Shared Essential, Gaming and Advanced catalog bulk toolbars are fixed below
  the scrollable option rows. Apply/Close remains the lowest window footer.
  UI Automation measured the final visible option at Y=516, toolbar at Y=529,
  and Apply footer at Y=778.
- The final executable carries the reference gear icon and product metadata:
  `Naufal Tech's Windows Powertoys`, description
  `Windows 10/11 PowerToys for IT System-Administrator`, file/product version
  `7.8.0.0`, and an empty Company field.
- Previously recorded Native AOT executable SHA-256 (superseded above):
  `DA089F5858D48ABC5FA58C2C005AD60B0E14E68612BB1CAD728442BEFD445CC0`.

## Release validation boundary

1. Earlier Native AOT milestones passed read-only startup, theme, scale,
   localization and all 20 Main UI route smoke tests. Current-binary evidence is
   recorded in the latest dated audit; historical tests are not a full retest.
2. Exercise administrative repair/tweak operations only on a disposable
   Windows test image, because build and read-only smoke tests intentionally do
   not change the developer PC.
3. The Inno Setup packaging pipeline has been built successfully. Code signing
   remains separate release-engineering work and does not change feature
   behavior.
4. The reference named resource-lock queue is implemented and statically
   verified; its concurrent timing still needs disposable-VM validation.
   Remaining historical state schemas and their cross-version lifecycle still
   need disposable-VM validation; see the audit.

## Safety rules

- Never execute the legacy PowerShell source during porting or audit.
- Do not run repair, policy, driver, service, registry, profile, de-bloat or
  reboot actions during smoke tests.
- GPU/runtime downloads must use explicit official-host allow-lists and verify
  signatures before execution.
- Every mutating operation requires confirmation, Administrator checks, live
  progress, read-back verification, and a recoverable restore path where the
  underlying Windows feature permits one.
