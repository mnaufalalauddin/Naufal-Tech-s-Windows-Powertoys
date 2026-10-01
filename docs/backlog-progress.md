# Optimization backlog — implementation checkpoint

**Latest continuation: [30 September 2026 report](backlog-20260930.md).** It adds
canonical batch execution, storage servicing, guarded individual HVCI controls and
a clone-only offline WIM workspace. Read that report for current limits and tests.
The checkpoint below is historical; its statement that these modules are absent
and its artifact hashes refer only to the 29 September build.

Date: **29 September 2026**. Application: **Naufal Windows Utility v8.0.0.0**.
Status: **Work in progress, local source; not pushed or published.**

This checkpoint continues the existing WinUI / .NET / Native AOT application.
It does not replace the previous catalogs, profiles, snapshots or installer.
The [earlier P0 checkpoint](performance-p0-audit.md) records the first analyzer
and execution-safety changes. This document supersedes its outstanding-work
status where explicitly noted below; historical test/artifact hashes stay historical.

## Implemented in this checkpoint

### Action inventory and effect-aware preflight

**System Info → Action Inventory / Shared Targets** exports the three existing
toggle catalogs: Essential, Gaming, and Advanced Windows Tweaks & De-Bloat.
It uses the same cached catalog factories as the actual controls, not a second
copy of the catalog. Each row includes stable ID, UI location, backend owner,
risk, elevation, restart sensitivity, descriptions, warnings and declared targets.
Missing/partial effect declarations are explicitly reported. This inventory does
not detect applicability or claim measured performance improvements.

Registry effects normalize hive aliases, path separators and case. Service Start
effects use the same registry identity, allowing service controls and registry
controls to collide correctly. Dynamic values, such as the physical-memory-derived
service-host split threshold, are identified as dynamic rather than fabricated.
Composite/filtered catalogs expand to their existing leaf owners.

Preflight rejects opposing values and incompatible definitions for the same ID.
It includes **all selected actions**, even already-applied ones, so a pending
action cannot silently contradict another selected configuration. Repeated leaf
references deduplicate in the inspection plan. Existing top-level ID deduplication
and execution-time Already Applied checks remain in place.

**Important limitation:** this is not a universal merged-write executor. Compatible
overlaps with independent backup owners are currently **blocked**, not executed
twice or silently merged. Shared primitives still need one snapshot owner and
legacy-snapshot migration before those combinations can be allowed. Existing
composite execution has not been rewritten to replay this entire inspection plan.
Undeclared effects cannot be checked, and Restore/explicit OFF do not yet have a
full cross-action conflict graph.

Concrete overlap found: Essential Telemetry writes AdvertisingInfo/Enabled and
Privacy/TailoredExperiencesWithDiagnosticDataEnabled, also owned by the advertising/
personalization children of the Advanced catalog. Those original settings and
backup IDs were preserved; no legacy backup was discarded to hide the duplication.

### Durable catalog journals and stale-view protection

Catalog toggle Apply, explicit OFF and Restore through the progress workflow save
a per-operation JSON journal in:

`%LOCALAPPDATA%\Naufal Windows Powertoys\Logs\CatalogOperations`

The Started record is durably written **before** calling the executor. Initial
logging failure blocks the operation. Completion captures configuration evidence,
before/after text, read failures and the actual result. An interrupted operation
without evidence remains Unknown. A completion-log failure reports a warning
without pretending previously executed writes were rolled back. Journals supplement,
not replace, the existing backend snapshots.

Successful configuration verification on a restart-sensitive option records
RebootRequired; it does not claim post-reboot effective-state verification.
Applied, AlreadyApplied, NotApplicable, PartiallyApplied, VerificationPending,
Failed and Unknown outcomes are distinguished where existing backend evidence
supports them. Unsupported legacy Apply is stopped by preflight. This is not yet
a single complete outcome model for every action family.

An in-process change generation invalidates open catalog scans around journaled
operations. Stale values are labeled and Apply/Restore remain disabled until
**Analyze / reload**. A queued request checks again after acquiring its mutation
lock and stops if its preview is stale. A scan overlapping a change cannot become
an authoritative fresh scan. Closing a window unsubscribes its listener.

This does not monitor external registry edits, other applications, or all older
action-style/profile operations. Automatic post-reboot reconciliation and durable
cross-session state are still outstanding. Reload is deliberate and on demand;
there is no new resident watcher or continuous background polling.

### Background Owner Finder

**System Info → Background Owner Finder — Read-only** provides an on-demand
process/service report with Copy / Save TXT through the existing report window.
It shows executable path where readable, creation time, working set, private
committed bytes, threads, handles, parent evidence and hosted-service PID matches.

Creation times reject recycled parent PIDs. Service matching is bracketed by two
process inventories and requires the same process identity across the scan.
Parent chains have cycle/depth limits. A WebView2 process shows its first observed
non-WebView2 ancestor as a **candidate**, never proof of the owning package or a
reason to remove the shared runtime. Provider failures and unreadable counters
remain Unknown. The report is a snapshot, not a process-termination interface.

The native WMI query selects only fixed necessary properties; command lines,
environment variables and credentials are not collected. Reports can contain
identifying executable paths; review before sharing. A bounded, shared read probe
prevents repeated clicks from accumulating blocked provider workers. The wait
timeout does not forcibly terminate a WMI provider operation.

Startup/task attribution, signatures/publishers, package identity, and continuous
per-app CPU/disk measurements remain unimplemented. Shared working sets are not
summed into claimed RAM savings. This report does not itself optimize resources.

### Legacy security bundle safety correction

The old `SecurityMitigationsPerformance` bundle combined a generic CPU mitigation
mask with HVCI disable. **New Apply is blocked** in both preflight and backend
until CPU/build-specific support and effective-state verification exist. The
existing stable ID, readback, old settings and original Restore path remain so
previous users do not lose access to their backups. The UI explains the restriction.

This is a safety correction, **not completion of granular Security & Mitigations**.
No security setting was changed or live-tested in this checkpoint. Registry
readback does not prove that all protections are effectively enabled or disabled.

## Full backlog status

| Priority / area | Current coverage | Still required |
| --- | --- | --- |
| P0 inventory | Three toggle catalogs, partial effect declarations, shared-target report | Complete all action families, profiles, application scopes, dependency/build metadata and A–E impact classification |
| P0 canonical engine | Stable-ID dedup, leaf inspection plan, declared conflict rejection, Already Applied checks | Unified primitive executor/snapshots, compatible-write consolidation, legacy migration and general dependency ordering |
| P0 state/logging | Journaled catalog operations, stale-view invalidation, queue recheck | All non-toggle workflows, persistent state, complete outcomes and post-reboot reconciliation |
| P0 measurement | Ten-sample before/after analyzer, context, raw export and overhead | Baseline import across restarts, wider hardware/build tests and controlled workload experiments |
| P1 apps/background | Existing built-in app catalog/uninstall/recovery; new read-only owner finder | Explicit additional AppX scopes/provisioning, dependency inventory and verified package/task/startup attribution |
| P1 startup/services/tasks | Existing selective service controls preserved | Comprehensive startup manager, publisher/signature evidence, task trigger/action inventory and task-definition rollback |
| P1 feature controls | Existing privacy, widgets, AI, connected-device and gaming controls preserved | Consolidate overlapping primitives; verify runtime effects and per-user service instances |
| P2 storage | Existing cleanup, hibernation and disk backends preserved | Complete previews, previous installation/component cleanup levels, capabilities/languages, optional features, driver export/removal, Reserved Storage, Compact OS and recovery controls |
| P2 security | Existing security entry points preserved; unsafe legacy bundle Apply blocked | Granular protection controls, effective-state checks, organization/Tamper Protection/UEFI-lock handling and hypervisor dependencies |
| P3 update/offline | Existing repair/service backends preserved | Unified servicing levels; cloned-image mount/index validation, manifest, commit/discard and build-specific offline removal with recovery testing |
| Integration/testing | Mocked regression, native read-only inventory and Native AOT layout checks | Hands-on report/stale-window testing, Windows 10/11 VM matrix, authorized Apply/rollback tests for new mutators |

No new ResetBase, driver removal, recovery removal, servicing deletion, offline
WinSxS removal or other irreversible operation is shipped in this checkpoint.
Existing uninstall/cleanup recovery limits remain unchanged. No universal RAM/FPS
benefit is asserted. No security choice is newly added to performance presets.

## Verification

- Release compilation: **0 warnings, 0 errors**.
- Pure/mocked regression: **4,634 assertions passed**, including registry scope,
  canonical references, conflicting values, independent restore owners, durable
  intent, interrupted operations, journal failure, state invalidation, PID reuse,
  missing timestamps/counters, service failures and cyclic ancestry.
- English-only UI/resource/navigation: **333 assertions passed**.
- Native AOT WinUI test host: **21,115 assertions**, **128 layout cases**,
  **8 flyouts** passed; minimum button contrast **4.84:1**. The host exercises
  authored page controls, not the real report or analyzer dialog interactions.
- Read-only native Background Owner probe: **188 processes, 294 services, 188
  creation timestamps, 1,330 report rows** on the current development PC. Process
  counts fluctuate. No processes were terminated and no settings were changed.
- Earlier read-only analyzer probe on this host: x64 Professional build
  **26300.9539**. Windows 10 and multi-device coverage are **not established**.
- `git diff --check` passed. Git reports LF/CRLF normalization warnings only.
- Native AOT publish and Inno Setup compilation succeeded. Installer:
  `artifacts/installer/Naufal-Windows-Utility-Setup-8.0.0.0-x64.exe`, **38,625,580 bytes**.
  SHA-256: `74979DCB307F224B4CBB3B0CD188CC2059B3C477AA6B640691BB0F06897FCDB2`.
  This installer has **not been installed** in this checkpoint. Source archive
  contents are the current non-ignored tracked/untracked source inputs, excluding
  build outputs, private snapshots and Git authentication/history data.

Run checks/build from the source root:

```powershell
dotnet run --project Tests/ProfileVerification/ProfileVerification.Tests.csproj -c Release
dotnet run --project Tests/Localization/Localization.Tests.csproj -c Release
# Optional read-only process/service probe:
dotnet run --project Tests/ProfileVerification/ProfileVerification.Tests.csproj -c Release -- --background-read-probe
# In an x64 Visual Studio Developer PowerShell:
.\Tests\HeaderLayout\Test-HeaderLayout.ps1 -NativeAot
.\build-installer.ps1
```

## Changed files in this continuation

- Engine/inventory: `CatalogEffectPlan.cs`, `CatalogInventoryReport.cs`,
  `CatalogOperationJournal.cs`, `CatalogStateEpoch.cs`, `CatalogPlanSafety.cs`,
  `LegacyMitigationPolicy.cs`.
- Existing backend metadata/routing: `CompositeToolToggleService.cs`,
  `DebloatCatalogService.cs`, `DebloatRegistryLabService.cs`, `DebloatService.cs`,
  `DebloatServiceGroupsService.cs`, `EssentialTweaksService.cs`, `PerformanceLabService.cs`.
- Background diagnostics: `BackgroundOwnerService.cs`, `BackgroundOwnerReport.cs`,
  `MainWindow.BackgroundOwners.cs`.
- UI: `MainWindow.CatalogInventory.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`.
- Tests: `Tests/ProfileVerification/CatalogEngineTests.cs`, `BackgroundOwnerTests.cs`,
  `GamingOwnershipTests.cs`, `WholeProgramAuditTests.cs`, `Program.cs`,
  `ProfileVerification.Tests.csproj`; `Tests/HeaderLayout/MainWindow.TestHost.cs`.
- Documentation: `README.md`, `CHANGELOG.md`, this report and the earlier P0 report.

The complete working-tree checkpoint also includes the analyzer and execution
safeguard files listed in the earlier P0 report. No unrelated user files were reset.

## Primary documentation checked

- [Win32_Process](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-process): process properties and parent PID/creation-time limitations.
- [Win32_Service](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-service): service PID association.
- [Microsoft client mitigation guidance, KB4073119](https://support.microsoft.com/en-us/topic/kb4073119-windows-client-guidance-for-it-pros-to-protect-against-silicon-based-microarchitectural-and-speculative-execution-side-channel-vulnerabilities-35820a8a-ae13-1299-88cc-357f104f5b11): hardware-specific controls and mitigation verification.
- [Memory integrity](https://learn.microsoft.com/en-us/windows/security/hardware-security/enable-virtualization-based-protection-of-code-integrity): separate HVCI/VBS configuration and runtime requirements.
