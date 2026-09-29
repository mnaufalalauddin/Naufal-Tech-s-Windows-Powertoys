# Optimization and De-bloat Engineering Audit

Date: 29 September 2026  
Product version: v8.0.0.0  
Working branch: `codex/p0-canonical-action-planner`

This document records source-level findings and implemented changes. It does not claim that destructive Windows mutations were executed during development.

## Architecture retained

- C# / WinUI application targeting .NET 10 for Windows.
- Native AOT remains a Release/publish concern.
- Existing service backends, snapshot/restore conventions, `NativeCommandRunner`, catalog UI, progress reporting, installer identity and branding are retained.
- No resident service, tray process, background updater or hidden reapply task was added.

## Canonical action/state work

`ActionPlanning.cs` introduces stable canonical action identity, dependency ordering, duplicate elimination, conflict rejection and cycle/missing-dependency rejection before execution.

Toggle and one-shot action definitions now have optional impact/evidence metadata:
- Runtime Optimization
- Storage Reduction
- Conditional / Workload-Dependent
- Preference / Cosmetic
- Advanced Security / Mitigation
- Maintenance / Repair

Evidence is explicitly Measured, Mechanism Unmeasured, Experimental or Not Applicable. Existing rows remain conservative until individually classified.

### Duplicate/overlap findings

- Dynamic Tick and HPET share BCD/timer ownership with Performance Profiles. `GamingCatalogOwnership` already filters those two rows out of the user-facing Gaming catalog while retaining legacy backends for compatibility. No second UI control was added.
- Temporary-file cleanup already belongs to Essential actions; no second cleanup action was added.
- Hibernation already belongs to Essential tweaks; no second storage toggle was added.
- Reserved Storage already belongs to the de-bloat storage backend; no duplicate storage toggle was added.
- Windows and Edge SmartScreen are different scopes and remain separate.
- Smart App Control and SmartScreen are different protections and remain separate.
- VBS, HVCI/Memory Integrity and Credential Guard are related but not aliases; they remain separate protection controls and are not applied as independent performance scripts by presets.

## Execution/result semantics

Catalog results distinguish read failure/unavailability from OFF. Reboot-sensitive configuration can now end as `VERIFICATION PENDING` rather than being reported as effective merely because a write read back.

No forced reboot was introduced.

## Before / After Analyzer

`OptimizationAnalyzer.cs` captures multiple samples of:
- physical RAM used
- commit used and commit limit
- CPU over the sample interval
- aggregate process read/write bytes per second
- system-drive free space
- Processes
- Threads
- Handles
- uptime

The Advanced page exposes a two-pass workflow: capture baseline, apply/reboot as needed, return to a comparable workload, then capture comparison. Deltas are shown even when worse. No boost score or fabricated percentage is produced.

Process I/O is an aggregate of readable process counters and is labelled/measured as such; it is not presented as physical-device throughput.

## Windows Security controls

Existing Defender behavior remains a Defender **policy** control and is labelled that way. It checks Tamper Protection before the disable-policy path. It is not relabelled as a verified full Defender removal/disable because modern Windows protection/platform state can prevent that conclusion.

Smart App Control remains its existing dedicated status/settings surface. This change does not force SAC through Microsoft's testing-only registry workflow.

New granular protection-state controls:
- Windows Firewall — Domain, Private and Public profiles; the firewall service is not removed. Effective profile state is queried through NetSecurity.
- Microsoft Defender SmartScreen — Windows scope.
- Microsoft Defender SmartScreen — Edge scope.
- UAC — `EnableLUA`, distinct from notification-level behavior. Changes require restart verification.

## Advanced Security & Mitigations

Advanced → Security & Mitigations adds opt-in:
- VBS
- Memory Integrity / HVCI
- Credential Guard
- LSA Protection

The controls describe **protection state**, not “optimization enabled”. They are not added to performance presets.

Original registry configuration is captured before the first change. VBS/HVCI/Credential Guard runtime evidence is read through `Win32_DeviceGuard`; the native WMI reader was extended for UInt32 arrays. LSA remains configuration-state only until a reliable effective-state probe is added.

Credential Guard and LSA Protection configured in their UEFI-lock form are not force-disabled. The utility reports that Microsoft's physical-presence removal procedure is required.

## Storage slimming

Existing owners are reused for temporary files, hibernation and Reserved Storage.

New unique Component Store actions:
- DISM AnalyzeComponentStore — read-only analysis.
- DISM StartComponentCleanup.
- DISM StartComponentCleanup /ResetBase — separate Danger action, explicitly irreversible for installed-update uninstallability.

Successful DISM exit does not generate a reclaimed-space claim. Free-space change belongs to before/after measurement.

## Tests added/extended

Non-mutating regression fixtures cover:
- duplicate canonical references execute once
- dependencies are inserted and ordered
- conflicts are rejected before execution
- dependency cycles are rejected
- analyzer summary arithmetic and commit-limit retention
- Firewall Domain/Private/Public parsing
- Verification Pending is terminal/neutral and not “verified”

These are model/fixture checks, not live Windows mutation tests.

## Build and validation

Expected Windows commands:

```powershell
dotnet build ".\Naufal Tech's Windows Powertoys.csproj" -c Debug -p:Platform=x64
dotnet run --project .\Tests\ProfileVerification\ProfileVerification.Tests.csproj
dotnet run --project .\Tests\Localization\Localization.Tests.csproj
.\Tests\ParityAudit\Test-CatalogInteraction.ps1
.\Tests\ParityAudit\Test-ProjectIdentity.ps1
```

For packaging:

```powershell
.\build-installer.ps1
```

This development environment cannot execute the Windows/WinUI/.NET build and no GitHub Actions run is configured for the branch. Therefore this checkpoint does **not** claim a successful new native build, installer, Windows 10/11 mutation test or VM validation.

Historical changelog entries contain earlier Windows/build validation; they are not evidence that these new changes have been validated on those builds.

## Additional implemented inventory/recovery surface

The Advanced page now exposes read-only/export actions for Startup locations, Services, Scheduled Tasks, Windows Capabilities, Optional Features, installed user languages, third-party Driver Store packages, Windows RE/Compact OS status and volume information. Driver Store packages can be exported to an application-owned backup directory before any later removal work.

Compact OS now has explicit enable/disable actions with query verification. Windows RE has explicit enable/disable actions with reagentc status verification. These are reversible controls; neither deletes the recovery partition.

Offline-image support now includes a documented supported DISM workflow, mounted-image inventory and stale-mount cleanup. It deliberately does not guess a WIM/ESD path, edition index, package identity or removal list.

## Remaining work

The following requirements remain incomplete rather than being represented by fake controls:
- full App & Background Owner Finder
- Startup disable/restore UI with per-entry publisher/signature evidence (inventory is implemented)
- generalized Service mutation UI and Scheduled Task disable/restore with task XML backup (inventory is implemented)
- Language/Capability mutation manager (inventory is implemented)
- Optional Feature mutation manager (inventory is implemented)
- general Driver Store selective removal manager (inventory + full third-party export are implemented)
- Previous Windows installation preview/removal workflow
- Recovery footprint deletion manager (status + WinRE enable/disable are implemented)
- full interactive offline-image source/index/mount/commit/discard UI (workflow/mount inventory/recovery are implemented)
- build-specific experimental deep component/WinSxS removal
- granular Exploit Protection / hardware-enforced stack protection controls
- CPU-mitigation controls with CPU/build-specific detection
- live Windows 10/11 VM verification for all new security/storage operations

Deep WinSxS deletion, DriverStore folder deletion, Windows Installer cache deletion, TPM clearing, recovery-key deletion, firmware changes, vulnerable-driver use, binary patching, security-status spoofing and organization-policy bypass are not implemented.

## Irreversible / non-Undo operations

- Component Store ResetBase restricts uninstall of installed updates and has no automatic Undo.
- Existing package/feature removals can require Store/Windows Update/install media to recover.
- UEFI-locked Credential Guard/LSA removal is not automated.
- Offline/deep servicing removal is not yet implemented and therefore no false Undo promise exists.
