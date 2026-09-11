# Catalog recovery and installed-baseline audit — 2026-09-09

## Result and reference identity

The installed folder supplied by the user was read without launching or changing it:
`C:\Program Files\Naufal Windows Powertoys`.

- Installed EXE: version **7.8.0.0**, company **Naufal Tech's Softwares**.
- Installed EXE SHA-256: `12A7B41D8634500389F7F18FB358DA8E3BDCDC82329F34539679671F130D419C`.
- It matches `artifacts/publish/win-x64-20260909-015841-073` exactly.
- All **160 installed application/manifest files** match that stage; no missing
  release payload was found. Inno's generated uninstaller files and deliberately
  excluded PDBs are not feature payload. This is an older intact installation,
  not evidence of mixed DLLs or an incomplete copy of that release.
- It does not contain the newer `Assets` icon files. The newer EXE, PRI, release
  manifest and branding assets must be installed to update the deployed app.
- Added `Tests/ParityAudit/Inspect-InstalledBaseline.ps1` for repeatable read-only
  metadata, binary identity and whole-payload comparison.

The other references remain the supplied original Win-PS2EXE application and
`V78.ps1`. The provided PS1 and project PS1 SHA-256 both remain
`660CD561C96FC00FBB26F15DFC94C4E03F4B4594E76C1D72A021C8E2CC18833D`.
The original V7.8 EXE SHA-256 remains
`C920ADF8AB56F644DBAA8EA470F9C4B3248A9CA1DCBD15C71AC980F9C9A01BFF`.
Neither reference was executed. There is no Git history/latest-model checkpoint
available here to attribute any missing behavior to a particular model switch.

## Confirmed findings and source corrections

1. **Essential bulk omissions.** The reference registers Icon Cache, NTFS
   Performance and SSD/NVMe Power in its Essential bulk selection/dispatch
   (`V78.ps1` functions `Test-WptEssentialBulkItemOptimized` and
   `Invoke-WptEssentialBulkItem`, with registrations near lines 25197 and
   25625–25685). Native individual buttons existed, but these three were absent
   from Select/Apply/Restore bulk processing. `EssentialBulkActionsService` now
   routes them through the real existing action backend. Individual Apply/Restore
   buttons remain on the same card; cleanup and Settings launchers are not
   silently added to bulk mutation. Icon Cache is safe-tier, storage advanced-tier.
2. **Verification information was discarded.** Catalog execution already read
   the preflight state but did not return it. `BeforeState` now survives all
   outcomes and `CatalogVerificationReport` records before/after actual values,
   backend detail and verified/failed/unavailable distinctions. This improves
   the reports requested by the original verification dialogs; it is not yet a
   reproduction of every original per-registry expected-value table.
3. **No shared report export.** The separate per-task progress window now has
   Copy and Save TXT. Reports include every queued/running/completed/failed/
   unavailable item, start time and elapsed time. Save captures one consistent
   report before showing the picker. Export errors do not replace task results.
4. **Synchronous individual actions could stall the UI.** Action Apply/Restore
   dispatch now runs off the UI thread while preserving task admission and
   progress context. Completing one of the three Essential actions refreshes
   its bulk state, toggle and selection summary.
5. **Storage power read-back was fragile.** SSD/NVMe snapshot/verification used
   optional registry overrides and unchecked text parsing of `powercfg` output.
   It now reads effective AC/DC indexes with the Windows power API and the disk
   subgroup, preserving native error codes instead of treating failed reads as
   zero. The CPU reader retains its processor subgroup behavior.
6. **Runtime pending states were conflated.** `Disable Pending` previously passed
   the broad `Contains("Pending")` enable check. Only Enabled or Enable Pending
   plus a successful DISM exit can now be accepted. Final Enable Pending is a
   restart-required warning, not READY and not a failed install. An old .NET
   registry marker cannot override DISM's pending-disable state. Failed queries
   cannot certify feature readiness or confirmed absence.
7. **Cryptographic Services could be falsely verified.** Starting `cryptsvc`
   previously ignored the result and readiness checked only startup type. Repair
   now uses the bounded service-start/wait helper and requires Automatic plus
   Running on read-back. The other explicitly manual prerequisites may stop
   normally when idle; they are not forced to stay running.
8. **Essential snapshots were insufficiently validated.** Icon Cache marked a
   capture complete before writing its payload and removed it without exact
   restore read-back. Capture now commits last, validates legacy fields/types,
   and verifies restored value/type before removing the backup. NTFS validates
   all four saved fields before any write, distinguishing an absent value from
   a missing/corrupt field. Storage rejects missing/invalid AC/DC snapshot
   indexes before restoring. Existing incomplete backups are retained, not
   silently overwritten with today's already-modified state.
9. **Save TXT used a picker incompatible with elevation.** The app manifest
   requires Administrator. Both the existing shared report dialog and new task
   export now use `Microsoft.Windows.Storage.Pickers` through
   `DesktopReportExport`, with an owner WindowId and cancellation/lifetime guards.
   The old `Windows.Storage.Pickers` code could fail in this elevated app.

## Cross-catalog audit coverage

This is a source/reference review and automated regression pass, not execution
of every administrative operation on a real machine. No route was removed.

| Surface | Reviewed coverage / outcome |
|---|---|
| Essential | Base/performance ownership, individual actions, three recovered bulk options, saved-state handling, storage reads, report export; fixes above. |
| Gaming | Manual toggles, BCD/performance routes, 15 individual actions, shared selection/admission/progress/restore paths retained. |
| Advanced | 41-row composition, child routing, unavailable versus read/write failure, service/default/snapshot restore, risk warnings and separate progress retained. |
| Performance Profiles / Live Gaming | Existing full-profile verification, RSC/power reads, rollback and live metrics retained; regression suite rerun. |
| Games Runtime & Compatibility | 18-entry analyzer, native enable, installer/source controls, pending-restart and required-service verification reviewed; fixes above. |
| GPU Driver Manager | Vendor inventory/source resolution, download/signature, install/update and repair read-back routes present; no driver download/install performed. |
| MSI Mode Utility | Refresh, details/NDIS identity, dirty Apply and live read-back routes present; no device interrupt settings changed. |
| Full / Quick Repair | DISM/SFC dispatch, staged reporting and completion guards retained; no servicing operation run. |
| Windows Update / Store / Explorer Fix | Six/nine/seven-stage flows, cache/service recovery, command verification and progress retained; no repair run. |
| Defender / BitLocker / Smart App Control | Apply/Restore/status and BitLocker suspend/resume/decrypt controls retained; no security setting changed. |
| Windows / Office activation reports | Report, copy and TXT export routes retained; desktop picker corrected. No licensing change attempted. |
| Disk Info / System Report | Data providers and report routes retained; desktop picker corrected. No disk operation run. |
| Legacy Windows Panels | Fifteen registered panel launch routes retained; no external panel launched. |
| Shared UI | 23 languages, eight scales, theme, wizard, task queue, risk badges, progress window, NT branding and installer structure checked by existing suites/static inventory. |

All 20 Main UI XAML routes still resolve to defined handlers. All 23 resource
tables contain 577 entries with no missing reference keys, blank entries or
placeholder mismatches. These structural counts do not prove complete behavior
or linguistic/visual parity. Long-form diagnostic/localization candidates remain
in the localization audit backlog; exported technical reports stay canonical.

## Verification

- Debug build: **0 warnings, 0 errors** after the final picker correction.
- Functional suite: **2,053 assertions passed**, including new bulk, before/after
  reporting, runtime state, storage subgroup and corrupt snapshot regressions.
- Localization suite: **93,224 assertions passed** across 23 languages.
- Publish-stage integrity: **10 assertions passed**.
- Report export routing: **10 static assertions passed**; no dialog opened.
- Native AOT/installer and final payload/icon identity: see release section below.

## Remaining verification boundary

No native app, original EXE, installer, repair, profile, service, driver or registry
mutation was executed during this audit. Therefore UI startup/crash reproduction,
native Save dialog interaction, all eight-scale layouts, cross-language display,
and actual Apply/Restore parity on supported Windows/hardware remain unverified.
The installed 7.8 directory is unchanged; building does not upgrade it.

The three recovered Essential bulk actions use the existing exact-snapshot
restore backend. This pass does not invent a universal no-backup baseline for
NTFS/storage power, nor does it claim to reproduce all original Windows Default
controls or every detailed verification table. Those are remaining parity gaps,
not confirmed hardware-unavailability cases to hide in a gray badge.

## Technical references

- [Microsoft: effective AC power index](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerreadacvalueindex).
- [Microsoft: enable/disable features with DISM](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/enable-or-disable-windows-features-using-dism?view=windows-11): pending enable requires restart before full enablement.
- [Microsoft: desktop picker namespace](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.windows.storage.pickers?view=windows-app-sdk-2.0): supports elevated applications, unlike Windows.Storage.Pickers.
- [Microsoft: desktop Save picker usage](https://learn.microsoft.com/en-us/windows/apps/develop/files/pickers-save-file).

## Final release

Native AOT and Setup compilation succeeded after the picker correction.
Final stage: `artifacts/publish/win-x64-20260909-173212-481`.

- EXE: `Naufal Windows Powertoys.exe`, 19,563,008 bytes.
  SHA-256: `E7B88EA244EE43D34FB686FBEE74328765F963EA7D0144AFDD327C31BED9C3C8`.
- Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`,
  38,032,399 bytes.
  SHA-256: `B29225EACB824A2C5F3C391720BBDDF17D8823820CE7C9E75ACF6A8C13133DA6`.
- Both: FileVersion/ProductVersion **8.0.0.0**, company **Naufal Tech's Ltd.**,
  Authenticode **NotSigned**. Metadata is not a signing certificate.
- Post-build icon checks: **101 assertions passed**; all **168 staged payload
  files** validated against the schema-2 manifest.
- Installed baseline was rechecked at the end and remains byte-identical to the
  supplied 7.8 installation. No automatic upgrade was performed.

The fixed-name older 8.0 Setup was replaced by this new build; timestamped
application stages remain available. Do not use intermediate stage
`win-x64-20260909-172809-843` as final: it predates the elevated-picker correction.
