# Naufal Tech's Windows Powertoys — Changelog

Development history from **30 August 2026** through **12 September 2026**.

**Snapshot cutoff:** 12 September 2026, 00:39:41 WIB (Asia/Jakarta, UTC+07:00).

This is a reconstructed engineering changelog, not a Git commit log or a claim
that every requested feature is finished. It consolidates dated project audits,
reference-analysis records, recorded build/test results, and user-reported
problems. Dates identify documented development checkpoints; an exact time is
given only for this snapshot and identifiable build artifacts. Early work without
a reliable individual date is grouped into a date range.

Entries are newest first. **Added**, **Changed**, and **Fixed** describe recorded
source changes. **Verification** describes the evidence available at that
checkpoint, not a new execution of those tests while writing this file. A compiled
feature or passing synthetic test is not equivalent to a successful Windows
mutation, complete visual validation, or full behavioral parity.

## Current application identity

| Field | Current value |
| --- | --- |
| Product | Naufal Tech's Windows Powertoys |
| Executable | `Naufal Windows Powertoys.exe` |
| File/product version | `8.0.0.0` |
| Installer release identifier | `8.0.0` |
| Company / publisher metadata | Naufal Tech's Ltd. |
| Implementation | Native C# / WinUI 3, .NET 10, Windows x64; self-contained, unpackaged Native AOT publication |
| Intended operating systems | Windows 10 and Windows 11; actual coverage varies by feature and Windows build |
| Branding artwork | User-supplied silver NT logo, `NT-s.png` |
| Default installation directory | `C:\Program Files\Naufal Tech's Limited\Naufal Windows Powertoys` |
| App-owned per-user data root | `%LOCALAPPDATA%\Naufal Windows Powertoys` |
| Digital signing | Application and Setup are unsigned; publisher metadata is not an Authenticode signature |

The native application is compared with the original
`Naufal Windows Powertoys V7.8.exe` and `V78.ps1`. Intentional user-requested
changes—branding, simplified titles, separate progress windows, and new app
management—are retained instead of copying the original appearance literally.

## 12 September 2026 — Built-in Windows Apps and consolidated history

### Added

- Added **Built-in Windows Apps → Review apps** to Advanced Windows Tweaks &
  De-Bloat, without replacing or changing its existing 41 tweak definitions.
- Added 31 initially unchecked app rows, Select all, De-select all,
  Analyze / reload, **Uninstall selected**, **Restore selected**, and individual
  Microsoft Store recovery buttons.
- Covered the complete requested app list:

  1. AV1 Video Extension
  2. AVC Encoder Video Extension
  3. Clock
  4. Dev Home
  5. Feedback Hub
  6. Get Help
  7. HEIF Image Extension
  8. HEVC Video Extension from Device Manufacturer
  9. Media Player
  10. Microsoft Bing
  11. Microsoft Clipchamp
  12. Microsoft Family
  13. Microsoft News
  14. Microsoft Teams
  15. Microsoft To Do
  16. Mobile Devices
  17. Outlook for Windows
  18. Paint
  19. Phone Link
  20. Photos
  21. Power Automate
  22. Quick Assist
  23. Solitaire & Casual Games
  24. Sound Recorder
  25. Start Experiences App
  26. Sticky Notes
  27. VP9 Video Extensions
  28. Weather
  29. Web Media Extensions
  30. WebP Image Extension
  31. Windows Notepad

- Implemented exact package-family matching, including the two explicitly
  supported Teams families. Core shell packages, Microsoft Store, App Installer,
  frameworks, and resource packages are outside the removal list.
- Implemented current-account native uninstall and restore. Restore first checks
  for a healthy installed registration, then attempts local registration from a
  remaining staged package, followed by Microsoft Store recovery where supported.
- Added verified Store product-ID mappings for 25 entries. Dev Home, Get Help,
  HEVC OEM, Teams, Mobile Devices, and Phone Link use local restore plus explicit
  manual Store recovery rather than an invented product ID.
- Added Store-agreement consent, exact-ID/user-scope installation, source endpoint
  validation, and warnings about data loss, licensing, and app functionality.
- Added selection/inventory audit logs under
  `%LOCALAPPDATA%\Naufal Windows Powertoys\Logs\BuiltInApps`. Failure to save the
  pre-operation audit log stops the batch; these logs are not personal-data backups.
- Added this English `CHANGELOG.md` as the first consolidated, dated development
  history. Creating the changelog does not change the application binary.

### Behavior and safeguards

- Missing apps remain selectable for restore. An inventory read failure blocks
  mutation instead of being treated as an empty app list.
- Uninstall affects only the account running the application. It does not remove
  provisioning, other users' apps, classic Win32/FoD variants, or WindowsApps
  protections. This also applies when Run as different user is used.
- Both batch operations require confirmation. Uninstall can remove local app
  data; restore reinstalls the app, not deleted personal data or necessarily the
  previous version. No automatic purchase or licensing bypass is performed.
- Each app uses the separate task-progress window. Windows-reported percentages
  are displayed when available; unknown-duration work is indeterminate. Missing
  targets are neutral, genuine failures are red, and active/successful work is green.
- Completion requires fresh package inventory/read-back. Opening a Store page
  alone is never counted as a successful restore. A timed-out deployment stops
  subsequent rows and retains the shared deployment gate until Windows finishes.
- Added the new controls, warnings, and consent strings to all 23 language catalogs.

### Verification

- Recorded Debug build: **0 errors, 0 warnings**; Native AOT publish and Inno Setup
  compilation succeeded.
- Recorded **2,655 functional regression assertions**, including 471 new app
  assertions, and **96,099 localization assertions** across 23 languages.
- Recorded checks also passed for 80 catalog/routing assertions, 18 AppData
  assertions, 12 installer-location assertions, 10 publish-stage assertions,
  101 icon assertions, and completed-stage payload hashes.
- The latest native inventory probe ran as the isolated `codexsandboxoffline`
  account and returned zero targets. An earlier real-user PowerShell inventory
  matched all 31 app identities. The isolated result is not evidence that the
  apps are absent from the user's account.
- Actual app uninstall/reinstallation, normal-user native UI operation, Setup
  execution, and Windows 10 restoration were not performed in this checkpoint.
  An auxiliary probe emitted NU1900 because vulnerability metadata was unreachable;
  this did not occur in the application's recorded clean build.

Evidence: [Built-in Windows Apps implementation and recovery map](BUILT_IN_APPS.md).

## 11 September 2026 — Windows 10/11 first-run prerequisites

### Changed and fixed

- Removed the first-run wizard's mandatory WMIC Feature-on-Demand installation.
  This addressed the reported setup timeout at approximately 60.4%, followed by
  failed WMIC verification and an unsaved first-run completion state.
- Replaced that prerequisite with native WMI checks for operating-system and
  memory information. An existing WMIC installation is left untouched.
- Updated wizard stages to verify WMI system and memory access. Reads have a
  30-second bound and reuse pending work rather than creating overlapping probes.
- Preserved honest failure handling: unreadable mandatory prerequisites do not
  produce a completed first-run state. Skip remains eligible to appear next
  launch; explicit Don't show again remains respected.
- Clarified that WMI checks do not require internet access, while WinGet setup may.
  Added five related translation keys to all 23 languages.

### Verification

- Recorded **2,184 functional** and **94,029 localization** assertions; Debug build
  had 0 errors and 0 warnings. Native AOT and Setup were produced.
- Read-only native WMI checks passed on the available Windows 11 system.
  Windows 10 behavior was covered synthetically, not by a Windows 10 runtime test.

Evidence: [WMI and first-run update](WMI_WIZARD_UPDATE_2026-09-11.md).

## 10 September 2026 — Reference buttons, installation and data locations

### Changed and fixed

- Aligned the 20 Main UI button labels, order, routing, and flat bordered styling
  with the supplied reference baseline while retaining requested risk colors.
- Corrected individual-action busy/admission handling, restore progress context,
  and bulk dispatch paths that could block the XAML UI thread.
- Preserved the bottom selection/restore toolbar, separate progress windows,
  per-action verification, and error output.
- Changed the default installer location to
  `C:\Program Files\Naufal Tech's Limited\Naufal Windows Powertoys`.
  Kept the stable installer AppId and disabled automatic reuse of an older
  installation directory. Existing installations are not silently moved.
- Consolidated app-owned Settings, Backups, RuntimeCache, Temp, Logs, and crash
  data under `%LOCALAPPDATA%\Naufal Windows Powertoys`.
- Added copy-only migration from recognized older app-data locations. Existing
  destination files are not overwritten; original backups are not deleted.
  Windows registry identities and vendor-owned/ProgramData caches are not renamed.

### Reported issue and verification

- The user reported blank text in catalog/progress windows, including labels and
  buttons that remained blank after moving the window. **This rendering issue
  remains unresolved; the folder-location changes are not a fix for it.**
- The button checkpoint recorded 2,090 functional assertions. The later location
  checkpoint recorded **2,120 functional** and **93,224 localization** assertions,
  plus AppData, installer, routing, staging, and icon checks.
- An earlier candidate rendered its Main UI, but the final binary was not fully
  interactively verified. Install/upgrade/uninstall behavior remains untested.

Evidence: [Button audit](PROGRAM_BUTTON_AUDIT_2026-09-10.md) and
[storage-location update](STORAGE_LOCATION_UPDATE_2026-09-10.md).

## 9 September 2026 — Restore recovery, version 8, branding and feature recovery

### Program-wide reliability and availability

- Corrected task-admission/queue-notice handling, first-run task lifetime and
  progress, bounded unique WinGet downloads, process-output observer failures,
  duplicate process lifecycle handling, late progress updates, and Office discovery.
- Distinguished confirmed absence from read errors, timeouts, corrupt backups,
  and failed verification. Unavailable tweaks now use a gray informational badge
  reporting verified and unavailable counts, rather than a red failure.
- Kept genuinely failed or unverified operations visible as errors. A missing
  component does not become a successful mutation, and composite read failures
  are not silently ignored.

### Restore follow-up and documented defaults

- Corrected Teredo verification to distinguish configured policy from operational
  state; an effective runtime label alone no longer proves the policy is wrong.
- Added recovery handling for legacy Intel JHI/Ndu service snapshots, avoided
  unnecessary writes to already-correct protected service values, and narrowed
  Store database ACL verification to the DACL that the application restores.
- Preserved original snapshots and added per-entry recovery receipts to avoid
  repeatedly replaying old restore data.
- Replaced error-message matching with a typed missing-backup result. Only
  genuine absence may authorize a known default; unreadable, malformed,
  incomplete, or failed backups do not authorize an automatic reset.
- Validated snapshot target/count/type/value completeness before restore and
  delayed cleanup until read-back succeeds across registry, service, BCD,
  Performance Lab, AI, Xbox, and imported-backup paths.
- Corrected saved-state comparisons: an originally enabled tweak can legitimately
  restore to enabled, and an originally stopped service can restore to stopped.
- Implemented narrowly scoped Microsoft/vendor-backed defaults where established,
  including controlled BCD overrides, DHCP DNS selection, UAC secure desktop,
  TDR override removal, long-path opt-in, and Lock pages in memory assignments.
- Strengthened DNS exact-order verification; USB/Ethernet/Wi-Fi snapshot scope
  validation; MTU adapter identity checks; and Storage Sense/Reserved Storage
  task, registry, and servicing read-back.
- Removed blanket deletion as a universal Performance Lab default. Unknown
  experimental/vendor settings and unsupported missing-backup cases remain
  explicitly unsupported instead of receiving guessed values.

### Branding and version

- Changed company/publisher metadata from **Naufal Tech's Softwares** to
  **Naufal Tech's Ltd.** and file/product version from **7.8.0.0** to **8.0.0.0**.
  The executable name remains `Naufal Windows Powertoys.exe`.
- Audited executable, window, taskbar, shortcut, package, and installer icons.
  The earlier user-supplied Windows/gear artwork was subsequently superseded by
  the requested silver **NT-s.png** logo during this day's recorded work.
- Added multi-resolution icon assets, aspect-ratio-preserving transparent
  canvases, consistent window icon assignment, and stable application identity.
- Corrected theme/language changes that could alter window geometry, minimized
  or maximized size-baseline corruption, monitor placement limits, and close-warning
  lifetime handling.
- Upgraded completed-publish validation to a full payload-hash manifest and
  rejected stale-branding/incomplete stages instead of trusting only the main EXE.

### Catalog recovery against the installed baseline

- Compared the supplied installed Program Files payload with retained artifacts.
  Its 160 files matched the older **9 September 2026, 01:58:41** version-7.8 stage,
  not the subsequently developed version-8 source. This established a stale
  installed baseline; it did not establish that a model change deleted features.
- Restored Essential bulk selection/apply/restore support for **Icon Cache**,
  **NTFS**, and **Storage Power**, while preserving their individual controls.
- Added before/after task reports with Copy and Save TXT; corrected synchronous
  individual-action dispatch, effective storage power-setting reads, runtime
  pending-state handling, Cryptographic Services verification, malformed Essential
  snapshots, and elevated report-save dialog handling.

### Verification

- Recorded functional checkpoints progressed through **1,629 → 1,735 → 1,815 →
  2,053** assertions; localization reached **93,224** assertions.
- The final catalog-recovery checkpoint also recorded report-export, publish-stage,
  icon, and payload checks. Build/publication succeeded without using Windows
  Apply/Restore or Setup execution as a test.
- Remaining default gaps and native runtime/visual parity were explicitly left
  open; these audits did not certify every service, device, or Windows edition.

Evidence: [Whole-program re-audit](PROGRAM_REAUDIT_2026-09-09.md),
[restore follow-up](RESTORE_FOLLOWUP_2026-09-09.md),
[default-restore audit and source references](RESTORE_DEFAULTS_AUDIT_2026-09-09.md),
[initial icon audit](ICON_AUDIT_2026-09-09.md),
[NT branding audit](NT_BRANDING_PROGRAM_AUDIT_2026-09-09.md), and
[catalog recovery](CATALOG_RECOVERY_AUDIT_2026-09-09.md).

## 7 September 2026 — Localization and language-switch reliability

### Added, changed and fixed

- Audited all **23 languages** and expanded the merged translation set from
  396 to **573 keys per language** at the first checkpoint. The audited set had
  no missing/blank entries or placeholder mismatches.
- Extended localization to shared statuses, tasks, progress, safety warnings,
  simplified option names, dashboard text, repair stages, first-run UI, date
  formatting, popup/accessibility text, and right-to-left numeric handling.
- Introduced maintainable translation catalog/template structures and retained
  canonical authored text so language changes do not translate a translation.
- Corrected stale-language text in dynamic BitLocker/status output and tracked
  authored control text per owning window.
- Added cleanup for removed rows and closed windows, plus header, tooltip, popup,
  and font-scale popup traversal. Covered dynamic detach/reinsert behavior.
- Audited **529 source/target language pairs** with the production translation
  adapter and XAML test doubles.
- A separate reliability pass corrected result-window/task lifetimes, SFC
  timeout/error classification, GPU target-version verification, bounded catalog
  reads, maintenance terminal-state handling, and invalid/sub-millisecond process
  timeouts.

### Verification and limits

- Localization assertions progressed from **80,676** to **92,006**. Functional
  assertions reached **1,372** after the program-wide follow-up.
- Passing catalog and round-trip tests did not establish professional translation
  quality or complete coverage of all long descriptions, confirmations, backend
  messages, or device/runtime guidance. Native visual review remained incomplete.
- No separately dated **8 September 2026** checkpoint was found in the reviewed
  evidence. No changes or release are invented for that date; this does not imply
  that no work took place.

Evidence: [Localization audit](LOCALIZATION_AUDIT_2026-09-07.md),
[language-switch fixes](LANGUAGE_SWITCH_FIX_2026-09-07.md), and
[program audit](PROGRAM_AUDIT_2026-09-07.md).

## 6 September 2026 — Profile application and Windows repair corrections

### Performance profiles

- Fixed Balanced-profile preflight failures caused by looking only for registry
  overrides. Effective AC/DC processor values are now read through native power
  APIs and shared by preflight, application, and verification.
- Corrected RSC method-result handling so a typed numeric zero is not confused
  with null, empty, or unsupported output. Actual success still requires
  per-adapter IPv4/IPv6 read-back, including during rollback.
- Recorded **717 functional assertions**, including additional power and RSC cases,
  and read-only checks of effective power settings and available adapters. No
  profile was applied as part of that verification.

### Microsoft Store Fix

- Replaced the reset path that produced **Not implemented** with a bounded
  `Reset-AppxPackage` process targeting a validated current-user package.
- Stopped subsequent cleanup when a reset remains pending/timed out rather than
  running overlapping deployment operations.
- Separated reset-command completion from final Store registration/health
  verification. Removed a premature same-version check that could misreport a
  successful reset as failed; final verification allows the trusted package
  family's version to change and retries registration discovery.

### Windows Update Fix

- Replaced the failing parent SoftwareDistribution rename with scoped, uniquely
  named backups of DataStore, Download, and catroot2. Existing backups are retained.
- Included UsoSvc in service coordination, rechecked stopped state before cache
  operations, and added best-effort service recovery after failure.
- Avoided ownership/ACL takeover and forced termination as a cache-repair shortcut.
- Corrected strict startup-type verification that flagged operational BITS/DoSvc
  configurations. Supported Manual/Automatic modes are handled appropriately,
  while invalid/disabled prerequisites are repaired.
- Required restarted services—including UsoSvc—to reach **Running**; configuring
  startup type alone is not counted as successful service recovery.

### Diagnostics and verification

- Added read-only crash diagnostic collection after the reported
  **0xc0000005 / Unexpected parameters** dialog. The triggering action was unknown
  and no matching diagnostic evidence established the root cause. **This issue
  was not declared fixed.**
- Functional checkpoints progressed from **754** to **914** assertions across the
  repair follow-ups; build and publication succeeded. User-supplied repair logs
  motivated the changes but are not developer-run end-to-end tests.

Evidence: [Profile apply fixes](PROFILE_APPLY_FIX_2026-09-06.md),
[repair fixes and diagnostics](REPAIR_FIX_2026-09-06.md), and
[repair verification follow-up](REPAIR_VERIFICATION_FIX_2026-09-06.md).

## 5 September 2026 — Service restore, clearer catalogs and dedicated progress

### Advanced restore and catalog descriptions

- Audited all 22 service groups and their 33 startup-default entries against the
  behavioral reference. Corrected SysMain restore to request Automatic startup,
  actually start the service, wait for required Running state, and verify related
  prefetch settings before considering the operation complete.
- Preserved exact saved state for vendor services whose defaults cannot safely
  be inferred. Incomplete snapshots and failed read-back retain their backups.
- Expanded Essential, Gaming, and Advanced option descriptions. Added red warnings
  for options affecting Windows Update and for Printing & Fax Services, explaining
  the loss of print, queued-job, printer-discovery, and fax functionality.
- Removed the unwanted restore-explanation header and ONE-SHOT labeling. Simplified
  option titles, including **Widgets - Remove → Taskbar Widgets**, without changing
  internal IDs or backup identity.
- Moved category/risk text out of option titles into colored description badges;
  high-risk warnings remain red rather than disappearing from the interface.

### Progress and task language

- Standardized action-oriented status wording such as **Applying**, **Restoring**,
  **Analyzing**, and **Ending**, rather than reusing button captions as live detail.
- Consolidated catalog work into a separate resizable progress window with a list
  of task bars, active-task header, elapsed time, bottom overall progress, and
  scrollable results. Catalog pages no longer carry the detailed result/bar block.
- Used Windows-style green working/success bars and red failure bars. Unstarted,
  skipped, failed, and unverified outcomes remain distinguishable.
- Extended progress integration across repairs, catalog operations, profiles,
  security, MSI, GPU, runtime, reports, and process work; pure navigation does not
  display fabricated work progress.
- Replaced the misleading fixed **15%** package-operation plateau with
  indeterminate waiting when Windows does not report measurable progress.
- Added bounded AppX operation waits, cancellation/grace handling, and retention
  of the deployment resource until the underlying Windows operation really ends.
  A timeout does not imply that Windows cancelled the operation immediately.
- Bounded Widgets inventory probes and reused pending probes instead of starting
  accumulating background work.

### Further reliability fixes

- Prevented late callbacks from changing completed progress, and kept failed bars
  visible even if the operation failed before reporting a positive percentage.
- Allowed restore of partially applied multi-value groups; applied documented
  fallback per child instead of resetting already-restored siblings.
- Validated complete BCD backups before writes, normalized boolean/absence
  comparisons, and retained snapshots until verified recovery.
- Corrected first-run **Skip** behavior: it does not suppress future runs unless
  **Don't show again** was explicitly selected. Corrupt settings show the wizard
  instead of silently treating setup as complete.
- Required MSI success plus configuration read-back before green completion.
  Process termination waits for exit, uses shared scheduling, and prevents unsafe
  selection changes while pending.

### Verification

- Recorded functional assertions progressed from **519** to **615**, with publish
  checks and clean builds. Protected Windows/service/device changes were not run
  merely to verify these source changes.

Evidence: [Advanced service audit](ADVANCED_DEBLOAT_AUDIT_2026-09-05.md) and
[program/progress audit](PROGRAM_AUDIT_2026-09-05.md).

## 4 September 2026 — Full profile verification and multiple audit-fix passes

### Performance Profile correctness

- Replaced MMCSS-only **CURRENT PROFILE / VERIFIED** detection with all 23
  reference checks: MMCSS, active plan, AC/DC CPU/parking policy, BCD overrides,
  per-interface TCP settings, global network/QoS settings, and both RSC protocols.
- Partial/custom profiles remain actionable; unreadable values are not certified.
  Added target-plan/GUID validation and post-apply verification with extended
  rollback, separately reporting outer MMCSS/power-plan recovery.
- Switched RSC application/restoration to per-adapter native operations, exposed
  mixed/unknown states, and stopped treating failed BCD reads as Windows defaults.
- Added profile transaction history, restart guidance, and preference migration
  that fills missing data without overwriting an existing canonical preference.

### Nine recorded findings corrected (F01–F09)

1. **BitLocker:** structured volume status/read-back; unknown/null is not PASS and
   0% alone is not proof of full decryption.
2. **Store reset:** replaced a log-only placeholder with a real reset API path.
   Its later platform limitation was addressed by the 6 September repair changes.
3. **DISM/SFC:** streamed live standard output/error, handled SFC encoding, and
   integrated timeout/cancellation and incremental progress.
4. **Repair stages:** backend-owned PASS/WARNING/SKIPPED/FAILED outcomes and
   idempotent terminal-state handling.
5. **Localization:** expanded all 23 catalogs to 396 merged keys, with stable
   aliases/case-insensitive resolution; not a claim of complete UI translation.
6. **MSI:** refreshed configuration/resource data, refined IRQ/BDF/NDIS matching,
   and avoided guessing hardware capabilities.
7. **Disk reports:** typed physical-disk size/media/health data with an IOCTL
   fallback and explicit unknown state rather than false healthy status.
8. **Runtime controls:** corrected install/repair/enable availability and busy-state
   combinations.
9. **Profile history:** atomic transaction records with duration, verification,
   restart/rollback details, and surfaced persistence warnings.

### Selection and Apply/Restore buttons

- Corrected the reported **Selected items / Pending changes 0** behavior that made
  Apply selected appear to do nothing.
- Defined checkbox selection as operation scope. **Apply selected** applies
  selected, available OFF entries; already-applied entries do not accidentally
  trigger restore. Pending counts reflect the selected applicable changes.
- Kept **Restore selected** separate from Apply and from explicit Windows-default
  commands. Saved original state may legitimately be ON.
- Made row switches dispatch a confirmed single-item Apply/Restore and return to
  the last verified state while awaiting confirmation; cancellation leaves no
  phantom applied state.
- Acquired busy gates before confirmation, retained failed selections, and kept
  Select all/safe/advanced as selection commands rather than immediate mutations.

### Shared infrastructure and packaging

- Corrected FIFO fairness for overlapping resource requirements, retained all
  active tasks in history, and classified a lease closed without a result as
  interrupted instead of completed.
- Made downloads atomic after closing file handles, bounded sizes, used unique
  temporary files, and preserved valid previous cache files on failure.
- Committed backup markers only after complete data/flush, validated saved state,
  and delayed backup deletion until read-back succeeds.
- Closed repeated-click and shutdown/reboot races across GPU, runtime, BitLocker,
  profiles, and owned tool windows; checked cancellation before launching a process.
- Made Setup staging require a completed, version/hash-valid publish, removed stale
  fallback selection, and propagated the requested version into app and installer.
- Set company/publisher metadata to **Naufal Tech's Softwares** for the then-current
  **7.8.0.0** build; this was superseded on 9 September.

### Verification

- Functional checkpoints increased through **73 → 373 → 412 → 484** assertions;
  five installer-stage checks also passed at the last checkpoint.
- Static checks covered 20 Main UI routes and 23 × 396 translation entries.
  Read-only diagnostics exercised selected profile, adapter, disk, and MSI data.
- A diagnostic test executable's unhandled WMI access-denied path was corrected.
  This does not establish that every previously reported application crash was fixed.
- Debug, Native AOT and Setup builds succeeded. Final elevated UI interaction,
  real system mutations, and install/upgrade/uninstall were not certified.

Evidence: [Profile audit](PARITY_AUDIT_2026-09-04.md),
[nine-finding re-audit](PARITY_REAUDIT_2026-09-04.md),
[F01–F09 fixes](AUDIT_FIXES_2026-09-04.md),
[catalog button fixes](CATALOG_BUTTON_FIX_2026-09-04.md), and
[general audit](GENERAL_AUDIT_2026-09-04.md).

## 3 September 2026 — Reference identity, task scheduling and publish pipeline

### Added, changed and fixed

- Verified that the PowerShell payload embedded in the supplied original EXE was
  byte-identical to the supplied reference script (2,437,485 bytes). Established
  their hashes as the behavioral audit baseline.
- Rechecked all 20 Main UI routes and catalog ownership against the final source
  handlers rather than matching labels alone.
- Added administrator startup, duplicate-instance handling, and busy exit/reboot
  protection linked to the Active Tasks view.
- Implemented the schema-2 first-run prerequisite workflow with restore-point,
  WinGet/App Installer, servicing/WMI/AppX checks, completion state, and suppression.
  **WMIC was still a prerequisite at this historical checkpoint; that requirement
  was removed on 11 September.**
- Added saved-state import for recognized legacy Xbox, low-risk de-bloat,
  Performance Lab, and manual Gaming backups. Imports validate allowed identities
  and complete data and do not overwrite native backups.
- Added separate saved-state restore and explicit default paths, Xbox Store
  recovery where mapped, and verified Advanced restart-candidate collection.
- Added a named-resource scheduler and Active Tasks view with RUNNING/QUEUED
  states, resource names, waiting details, elapsed time, and history. Conflicting
  work queues; nonconflicting/read-only work can proceed independently.
- Corrected the Windows App SDK publish/staging pipeline associated with
  **MSB3094** (SourceFiles/DestinationFiles count mismatch). Publication is followed
  by staging the complete output for the EXE Setup compiler.

### Verification

- Recorded clean Debug and Native AOT publication plus Inno Setup compilation
  for version 7.8.0.0. The retained 21:36:57 stage contained 160 payload files.
- Route/count matching and static inspection did not certify all 649 reference
  functions, administrative operations, or cross-version behavior. Signing and
  clean-machine installation testing remained outstanding.

Evidence: [Reference parity audit](PARITY_AUDIT_2026-09-03.md) and
[reference parity notes](REFERENCE_PARITY.md).

## 1–2 September 2026 — Native feature coverage and early read-only UI validation

The static parity checkpoint is dated **1 September 2026**. Additional early
display/dashboard smoke results are preserved in the cumulative project notes,
but not every individual change has an independently preserved implementation date.

### Recorded feature coverage

- Represented the original 20 tool routes and three performance profiles.
- Matched repair workflow structure: Full Repair (2 stages), Quick Repair (1),
  Windows Update Fix (6), Microsoft Store Fix (9), and Explorer Fix (7).
- Added native Disk Information, System Report, Windows/Office activation reports,
  and guarded Defender, Smart App Control, and BitLocker workflows.
- Organized Advanced into its **41-row** ownership model. Gaming included six
  manual toggles, 38 Performance Lab definitions, the BCD editor, and 15 direct
  actions. Essential retained its own definitions/actions; Game DVR belonged
  to Advanced/Xbox rather than being duplicated in Gaming.
- Expanded Games Runtime & Compatibility to 18 analyzed entries with distinct
  analysis, official-source, download/install, and repair/enable routes.
- Implemented GPU discovery via the display setup-class GUID, automatic initial
  adapter selection, mapped official NVIDIA/AMD/Intel sources, HTTPS allow-lists,
  integrity/publisher checks, and post-install read-back.
- Implemented the seven-column MSI editor with IRQ/mode/message-limit/priority
  data, driver INF defaults, details, registry navigation, dirty-only changes,
  validation, and read-back. Corrected an audit misconception: the reference Reset
  button belonged to the font popup, not MSI.
- Restored all **23 language choices** after the initial English/Indonesian-only
  implementation; the dated checkpoint contained 289 keys per language.
- Retained 15 Legacy Windows Panels and the eight requested scaling values plus
  Reset to 100%.

### Early dashboard and display milestones

- Removed long introductory menu-header definitions and user-visible V78(91) /
  recovered-from wording while retaining option-level explanations.
- Applied green/amber/blue/gray risk-aware toolbar colors and moved bulk catalog
  toolbars below option lists, with Apply/Close in the bottom footer.
- Replaced the extra LIVE SYSTEM SNAPSHOT section with the requested
  **🔴 LIVE GAMING STATUS** heading and expanded the live detail sequence to cover
  MMCSS, power, gaming flags, CPU policy, SysMain/MPO, capture, networking, shader
  cache, and raw power-plan output. Restored a one-second refresh cadence.
- Recorded read-only startup, 20-route, 23-language, Light/Dark, and eight-scale
  smoke tests, including high-scale Advanced/GPU/MSI/runtime layouts.
- These historical smoke results applied to the tested early binaries only;
  they do not invalidate later reports of blank text or certify subsequent builds.

Evidence: the workspace's `analysis/v78-static/feature-parity-audit.md` and
`feature-migration-matrix.md` (both updated 1 September 2026), plus the historical
verification section of [Porting status](PORTING_STATUS.md).

## 30–31 August 2026 — Project foundation and initial native port

This is the earliest development period supported by the retained workspace
scripts, initial C# files, user reports, and cumulative foundation notes. The
history of the original PowerShell product before this native project is not
reconstructed here.

### Initial development

- Began migrating the PowerShell / Win-PS2EXE reference into a native C#/WinUI 3
  application, with a dashboard, live system monitoring, performance profiles,
  native reports, and shared resizable tool windows.
- Performed static PowerShell analysis: **38,482 lines, 649 functions, 135 click
  handlers, 20 primary tool-button definitions, and zero parser errors**.
  Initial analysis scripts are dated 30 August 2026; these counts describe the
  source inventory, not completed porting or runtime test coverage.
- Adopted the requested product name **Naufal Tech's Windows Powertoys** and the
  executable filename **Naufal Windows Powertoys.exe**.
- Started feature-by-feature comparison after reports that the native build lacked
  behavior available in the original compiled PowerShell application.
- Added explicit per-row Select controls and separated selection from ON/OFF
  state/target handling. This initial behavior was refined again on 4 September.
- Wired the Light/Dark button and underlined-A scaling popup for **25%, 50%, 75%,
  100%, 125%, 150%, 175%, and 200%**.
- Built non-cumulative font/geometry scaling from a 100% baseline, theme/scale
  persistence, and propagation to open/created tool windows.
- Addressed clipped checkbox/switch templates, high-scale catalog layout collapse,
  and dark-mode foregrounds on virtualized rows. Expanded shared scrolling and
  resizing behavior instead of changing font size alone.
- Introduced shared maintenance progress, elapsed time, live stage information,
  and logs; later audits extended and corrected this infrastructure.

### Reported instability

- Startup testing exposed repeated managed **0xe0434352** application errors during
  early display/startup work. Added crash logging and startup diagnostics, and
  later early builds passed limited startup smoke tests.
- A direct DLL diagnostic attempt without the Windows App Runtime bootstrap also
  produced a separate failure. These reports are recorded as development history,
  not evidence that all native application crashes were permanently eliminated.

Evidence: retained 30 August analysis scripts and initial source snapshots in the
workspace, [Porting status](PORTING_STATUS.md), and the dated reference audits above.

## Recorded verification progression

These are cumulative assertions reported by retained checkpoints, **not counts
of distinct features, real Windows mutations, or newly rerun tests**. Counts from
different suites are kept separate. Later checkpoints supersede earlier totals.

| Checkpoint | Functional regression assertions | Localization assertions | Context |
| --- | ---: | ---: | --- |
| 4 September 2026, final general audit | 484 | — | Earlier same-day checkpoints: 73, 373 and 412 |
| 5 September 2026, final program audit | 615 | — | Earlier service-restore checkpoint: 519 |
| 6 September 2026, final repair verification | 914 | — | Earlier checkpoints: 717 and 754 |
| 7 September 2026, final program audit | 1,372 | 92,006 | Earlier localization checkpoint: 80,676 |
| 9 September 2026, final catalog recovery | 2,053 | 93,224 | Earlier functional checkpoints: 1,629, 1,735 and 1,815 |
| 10 September 2026, location update | 2,120 | 93,224 | Earlier button checkpoint: 2,090 |
| 11 September 2026, WMI wizard | 2,184 | 94,029 | Native Windows 11 read-only WMI probe; no Windows 10 runtime test |
| 12 September 2026, built-in apps | 2,655 | 96,099 | No actual app uninstall/restore or Setup execution |

Translation-entry counts such as 23 × 289 or 23 × 396 are resource inventories,
not localization assertion totals. A dash means no comparable dedicated suite
total is recorded here, not zero localization work.

## Latest recorded build artifacts

Build stage: `artifacts/publish/win-x64-20260912-000505-037`.
File modification times observed during changelog preparation are
**12 September 2026, 00:05:51 WIB** for the app and **00:06:17 WIB** for Setup.
These are artifact timestamps, not a public-release or installation timestamp.

| Artifact | Size | SHA-256 |
| --- | ---: | --- |
| `Naufal Windows Powertoys.exe` | 19,839,488 bytes | `F3F55073358689325C0778F8DBADDB8AAF5260DD5E1C66F400E8AE125BC3A26B` |
| `Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe` | 38,052,032 bytes | `94DF7740E1C1A4C0E89A05C2D8AB10432BBF8C6AD36B4280046648CD204442DA` |

The Setup is under `artifacts/installer`. Both artifacts carry version 8.0.0.0
and Naufal Tech's Ltd. metadata and remain unsigned. The fixed-name Setup was
replaced by the latest build; older timestamped publish stages were retained.
This documentation-only update does not rebuild or replace either artifact.

## Open issues and release-validation limits at the snapshot cutoff

- **Blank UI text:** the 10 September report of persistent empty labels/buttons
  in native catalog/progress windows has not been conclusively diagnosed or fixed.
- **Native error dialog:** the earlier 0xc0000005 / Unexpected parameters report
  has no confirmed reproducing action or established root cause.
- **Full reference parity:** static handler/catalog comparisons and regression
  tests do not certify 1:1 effects, timing, restore semantics, or interactions for
  every reference function. A complete Windows 10/11 and hardware test matrix is
  still outstanding.
- **Real restore/apply testing:** protected registry access, service startup,
  hardware disappearance, policy-managed machines, reboot recovery, driver/MSI
  changes, package deployment, and repair recovery need isolated-machine testing.
  Source fixes must not be described as successful real-PC mutations without logs.
- **Missing backups:** only established, scoped defaults are implemented. Some
  experimental/vendor settings, USB/NIC properties, MTU cases, and Essential
  options still have no safe automatic fallback. Corruption/access denial is not
  treated as absence; failed recovery retains usable backups.
- **Removed components:** AI feature/app payload restoration, Xbox recovery, and
  app reinstalls can still require Store/Windows servicing, network access,
  licensing, device/region eligibility, or manual action. No universal recovery
  guarantee is made.
- **31-app feature:** native normal-user smoke testing and actual uninstall/restore
  remain pending. Classic Windows 10 variants are outside the Store-package scope;
  unavailable or retired Store products are not automatically recoverable.
- **Languages and appearance:** all 23 languages have catalog/test coverage, but
  complete translation of every dynamic/backend string, native-speaker review,
  and every window × language × theme × scale combination remain unverified.
- **Distribution:** signing, clean-machine installation, upgrade/uninstall, pinned
  shortcut refresh, and current-binary native startup/interaction require further
  verification. Merely building Setup does not test these behaviors.

## Evidence and maintenance notes

- The primary chronology comes from the dated audit files linked beside each
  entry. [PORTING_STATUS.md](PORTING_STATUS.md) is cumulative and also contains
  historical statements superseded by newer checkpoints.
- Early workspace evidence is retained under
  `<private-analysis-workspace>/v78-static`, including
  `migration-map.md`, `feature-migration-matrix.md`, and `feature-parity-audit.md`.
  Initial analysis scripts and source snapshots corroborate the 30 August start.
- The 3 September audit records the original EXE SHA-256 as
  `C920ADF8AB56F644DBAA8EA470F9C4B3248A9CA1DCBD15C71AC980F9C9A01BFF`
  and reference PS1 SHA-256 as
  `660CD561C96FC00FBB26F15DFC94C4E03F4B4594E76C1D72A021C8E2CC18833D`.
  These identify the comparison inputs; they are not hashes of the current app.
- No external product availability, Microsoft default, or certificate status was
  newly researched for this documentation-only task. Implementation references
  and their qualifications remain in the linked audits.
- Future updates should add a dated checkpoint with the actual scope, tests,
  artifact identity when rebuilt, and remaining issues. Do not turn a reported
  request into a completed feature or replace historical evidence with a newer
  build's results.
