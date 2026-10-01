# Performance/debloat audit — 29 September 2026

Status: **Work in progress. This checkpoint completes a bounded P0 slice, not the
entire requested audit or optimization roadmap.** Existing behavior, framework,
branding, version v8.0.0.0 and backup identities are retained. No host tweaks,
security changes, cleanup, app uninstall or reboot were executed in this audit.

## Source and execution map inspected

| Area | Existing source / responsibility |
| --- | --- |
| Startup | `App.xaml.cs`: WinUI application, single-instance mutex, per-user file migration, crash logging |
| Build | `.csproj`: .NET 10, Windows App SDK 2.4, x64 Native AOT release; `build-installer.ps1` / Inno Setup |
| Navigation | `MainWindow.xaml`, `MainWindow.Navigation.cs`: five pages, catalogs and System Info |
| Catalog definitions/state | `ToggleCatalogModels.cs`, `CompositeToolToggleService.cs`, `CatalogStateReader.cs`: owner routing and read-failure vs absence distinctions |
| Selection/execution | `CatalogSelectionPlan.cs`, `CatalogActionRunner.cs`, `MainWindow.xaml.cs`: preview, task admission, backend apply/restore, verification, progress |
| Serialization | `TaskActivityService.cs`, `TaskAdmission.cs`: existing resource leases including SystemMutation for catalog Apply |
| Recovery | `RegistryRestorePlan.cs`, `RegistrySnapshotCommit.cs`, `ServiceRestoreSnapshot.cs`, `PerformanceProfileTransaction.cs`: existing snapshot/transaction mechanisms; not replaced |
| Performance profiles | `PerformanceProfileService.cs`, extended/verification services: profile-specific power, MMCSS, BCD and networking configuration |
| Essential catalog | `EssentialTweaksService.cs`, `EssentialActionsService.cs`, `EssentialBulkActionsService.cs`, `PerformanceLabService.cs` |
| Gaming | `GamingTweaksService.cs`, `GamingBcdService.cs`, `GamingCatalogOwnership.cs`, registry experiments |
| Advanced / apps | `Debloat*`, `WindowsAiService.cs`, `XboxComponentsService.cs`, `BuiltInApps*`, `OneDriveAppService.cs` |
| Existing telemetry | `SystemMonitorService.cs`: live CPU/RAM/GPU/network and processes; not a before/after analyzer |

Runtime depends on Windows desktop/WinUI deployment files, native APIs, registry,
and (for existing operations) PowerShell, WMI/CIM, servicing, Store/WinGet and
vendor/device support. Removing those facilities cannot be assumed safe for this
utility. This checkpoint adds no resident service, task, tray worker or updater.

## Concrete findings and disposition

| Finding | Evidence / effect | Disposition |
| --- | --- | --- |
| Repeated selected canonical IDs could run multiple times | Selection planner previously appended every definition | Fixed: identical definitions deduplicated, conflicting definitions rejected before writes |
| Owner lookup retained caller-supplied metadata/casing | Composite resolved service but forwarded the caller's definition | Fixed: all directions dispatch owner's definition; stable IDs unchanged |
| Queue admission could leave an already-applied action writing again | Runner re-read state but always called SetState | Fixed: return explicit AlreadyApplied result without writes; Restore remains independent |
| Hibernation/Fast Startup conflict | Essential `Hibernation` disables hibernation, while lab `FastStartupEnable` enables a dependent feature | Block combined Apply; check dependency before Fast Startup writes; do not silently choose a winner |
| Preview omitted detailed consequences | Apply confirmation listed names only | Descriptions, warnings and restart caveats now included |
| Live telemetry lacked repeatable comparison | No commit/threads/handles/baseline runs | New on-demand analyzer added to existing System Info, not a duplicate live dashboard |
| BCD profile/catalog overlap already consolidated | `GamingCatalogOwnership` excludes DynamicTick/HPET from the independent Windows-gaming controls | Preserve prior ownership rather than recreate duplicate controls |
| Icon cache/NTFS/storage-power actions already shared | `EssentialBulkActionsService` delegates to existing action service | Preserve shared executor rather than introduce another implementation |

No existing Windows tweak backend was copied into the analyzer. No IDs or user
snapshots were renamed. The new ID-level deduplication does **not** establish
effect-level uniqueness across differently named services, apps and profiles.

### Focused effect inventory (not an exhaustive inventory of every tweak)

| Stable action(s) | Owner / scope and target | Impact classification | Recovery / dependency |
| --- | --- | --- | --- |
| `Hibernation` | Essential; machine power configuration and hibernation/menu registry values | B Storage reduction; C boot/workload dependent; savings unmeasured | Existing snapshot + powercfg recovery; Fast Startup depends on hibernation |
| `FastStartupEnable` | Performance Lab via Essential; machine `HiberbootEnabled` | C boot-dependent; not an idle-RAM saving | Existing exact registry snapshot; hibernation prerequisite |
| `GameDVR` | Gaming; GameConfigStore/capture/current-user and machine policy values | A potential background capture reduction, workload dependent and unmeasured | Existing saved values; further overlap audit with Xbox module still required |
| `IconCache` | Essential action + bulk adapter; Explorer cache-size preference | C conditional, not measured resource savings | One shared action backend; original snapshot |
| `NtfsPerformance` | Essential action + bulk adapter; NTFS options | C workload dependent, unmeasured | Existing readback and original-state restore |
| `StoragePowerLatency` | Essential action + bulk adapter; active power-plan settings | C conditional trade-off, unmeasured | Existing captured plan values |
| DynamicTick / HPET and profile BCD options | Performance-profile owner with retained legacy backends | C experimental/workload dependent | Existing transaction/snapshot; no BCD writes tested here |
| `SecurityMitigationsPerformance` | Existing Performance Lab bundle: CPU mitigation registry mask + HVCI | E advanced security; **not a proven optimization** | Still requires redesign into granular supported/build-aware controls; no mutation tested here |

## Analyzer contract and use

1. Open **System Info → Resource Analyzer — Before / After**.
2. Describe workload/power/background apps and record baseline.
3. Make only explicitly selected changes through their existing catalog controls.
4. Return and capture After under matching conditions. Record actions and reboot
   context honestly. Copy / Save TXT retains both runs and complete raw samples.

Each run primes rate counters, then captures ten samples with a requested one-second
delay. Actual monotonic elapsed intervals and UTC timestamps are recorded. Capture
runs off the UI dispatcher. Closing the window cancels the remaining sampling;
the query/process handles are disposed. There is no permanent sampling loop.

System physical RAM uses total minus available pages. Commit and commit limit use
system page counts. This avoids summing shared process working sets. Processes,
threads and handles come from system-wide counters, not incomplete per-process
enumeration. The utility's own working set includes shared pages; private memory
is committed process memory. These subsets are never added to system totals.

CPU uses the English PDH Processor Information total counter. Disk read/write
rates use exposed PhysicalDisk totals in decimal MB/s; they are not storage freed,
per-app attribution or disk busy time. The free-space metric is total free space
on the Windows volume only, not all volumes and not cleanup attribution. RAM uses
GiB/MiB explicitly. Utility CPU is normalized to total active processor capacity.

Reports show median, range and valid/total counts. Missing, negative or nonfinite
counter values do not become zero in summaries. Deltas require at least three
valid samples in both runs. Increased/decreased/unchanged are observations, not
automatic judgments of performance benefit. Mismatched workload/build/volume/
sampling settings are flagged. Other background activity can still confound runs.

The baseline is in memory for the application session only. Save TXT exports it;
import/cross-reboot comparison is **not implemented**. Action/profile/reboot fields
are user annotations, not detection of effective Windows security or reboot state.
Only the Windows system volume is sampled for free space. Probe-test process
overhead is not the same as full GUI application overhead.

## Microsoft references checked

- [PERFORMANCE_INFORMATION](https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-performance_information): system page/count fields.
- [PDH_FMT_COUNTERVALUE](https://learn.microsoft.com/en-us/windows/win32/api/pdh/ns-pdh-pdh_fmt_countervalue): validate counter status before using data.
- [System power states](https://learn.microsoft.com/en-us/windows/win32/power/system-power-states): Fast Startup relies on hibernation.

## Verification and build

Read-only native sampling succeeded on **x64 Professional build 26300.9539**,
with ten valid samples for all sixteen metrics. This is one Windows 11 development
host, not Windows 10 or a VM compatibility matrix. No before/after savings are claimed.

Run the pure/mocked regression suite:

```powershell
dotnet run --project Tests/ProfileVerification/ProfileVerification.Tests.csproj -c Release
dotnet run --project Tests/Localization/Localization.Tests.csproj -c Release
```

Optional read-only native probe (prints a report, does not run tweaks):

```powershell
dotnet run --project Tests/ProfileVerification/ProfileVerification.Tests.csproj -c Release -- --resource-read-probe
```

Build the installer in an x64 Visual Studio Developer PowerShell:

```powershell
.\build-installer.ps1
```

## Checkpoint results and changed files

- Pure/mocked regression: **4,581 assertions passed**; no Windows settings changed.
- English-only UI/resource/navigation: **315 assertions passed**.
- Native AOT WinUI layout host: **20,511 assertions**, **128 layout cases** and
  **8 flyouts** passed. The new launcher participates in contrast checks; these
  are not hands-on validation of the analyzer dialog/picker.
- Release compilation: **0 errors, 0 warnings** at the compilation checkpoint;
  final Native AOT publish and Inno Setup compilation succeeded.
- Installer: `artifacts/installer/Naufal-Windows-Utility-Setup-8.0.0.0-x64.exe`,
  38,569,675 bytes. SHA-256:
  `914A8AD2F0D901637BF307237A8815B0CB89C8B026B38018B83655C93145C022`.
  Installer has not been installed/end-to-end tested in this checkpoint.
- `git diff --check` passed. Changes remain local and uncommitted.

Changed/new files:

| Group | Files |
| --- | --- |
| Catalog safeguards | `CatalogSelectionPlan.cs`, `CatalogPlanSafety.cs`, `CompositeToolToggleService.cs`, `ToggleCatalogModels.cs` |
| Analyzer backend | `ResourceMeasurement.cs`, `NativeResourceProbe.cs` |
| UI and preview | `MainWindow.ResourceAnalyzer.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs` |
| Tests | `Tests/ProfileVerification/CatalogSelectionTests.cs`, `ResourceMeasurementTests.cs`, `Program.cs`, `ProfileVerification.Tests.csproj`; `Tests/HeaderLayout/MainWindow.TestHost.cs` |
| Documentation | `README.md`, `CHANGELOG.md`, `docs/performance-p0-audit.md` |

## Outstanding work / no false completion claims

This section describes the **first** P0 checkpoint. For the subsequent effect
inventory, journals, state invalidation, owner finder and legacy mitigation gate,
see the [current backlog status](backlog-progress.md). Artifact hashes above refer
to that first checkpoint and must not be used to verify a later installer.

- Full per-tweak target/scope/value/dependency inventory and registry/service/task
  effect-level deduplication across all catalogs remains unfinished.
- Universal conflict/dependency graph, canonical state for all legacy services,
  durable action journals and automatic post-reboot effective-state verification
  remain broader P0 work. New guards cover documented cases, not all conflicts.
- Existing bundled `SecurityMitigationsPerformance` still needs granular controls
  and CPU/build-specific validation. No claim is made that its registry readback
  verifies all active protections. SAC, Defender, Firewall, SmartScreen, UAC,
  VBS/HVCI, Credential Guard, LSA and other mitigation backends were not newly
  implemented, disabled or certified by this checkpoint.
- No background owner finder, comprehensive startup/task manager, new storage
  removal, offline image removal or servicing removal is implemented here.
- UI analyzer interaction, canceled-picker lifecycle and target Windows 10 build
  coverage need hands-on/VM validation beyond compilation and backend tests.
- No new irreversible action is introduced. Existing uninstall/cleanup behavior
  retains its original recovery limitations. Do not equate app removal with RAM
  savings, hidden icons with disabled features, or security reduction with speed.
- No GitHub push, release creation or remote metadata update in this checkpoint.
