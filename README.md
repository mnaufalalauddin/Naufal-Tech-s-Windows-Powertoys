# Naufal Windows Utility

[![Source version](https://img.shields.io/badge/Source_Version-v8.0.0.0-0567ff?style=for-the-badge)](CHANGELOG.md)
[![Latest release](https://img.shields.io/github/v/release/mnaufalalauddin/Naufal-Windows-Utility?style=for-the-badge&label=Download)](https://github.com/mnaufalalauddin/Naufal-Windows-Utility/releases/latest)
[![Windows target](https://img.shields.io/badge/Target-Windows_10_%2F_11_x64-0078d4?style=for-the-badge)](#quick-start)
[![License](https://img.shields.io/badge/License-MIT-16803c?style=for-the-badge)](LICENSE)
[![Interface](https://img.shields.io/badge/UI-English-8250df?style=for-the-badge)](#interface--project-status)

A native Windows dashboard to **repair system components**, **inspect disk health and system information**, **manage Windows apps**, **review privacy settings**, and **configure gaming and performance options**—with live monitoring and operation progress in one place.

Developed by **Muhammad Naufal Alauddin**. Independent, open-source, and under active development. **Not affiliated with Microsoft or Microsoft PowerToys.**

> **Source update — 1 October 2026:** `main` includes the Resource Analyzer,
> action conflict checks and journals, shared privacy snapshots, Background Owner
> Finder, storage servicing, individual protection controls, and an offline WIM
> workspace. These additions are under validation and are **not all present in
> the downloadable release**. See [current verification and known limits](#current-verification--known-limits).

### Dark Mode

![Naufal Windows Utility — Dark Mode, 28 September 2026](docs/images/dashboard-dark-20260928-012955.png)

### Light Mode

![Naufal Windows Utility — Light Mode, 28 September 2026](docs/images/dashboard-light-20260928-012935.png)

*Light and Dark screenshots supplied by the maintainer on 28 September 2026 at 01:29. Home shows About, Task Monitoring, and Exit; restart-sensitive changes instead offer “Restart now” or “Later” after active tasks finish. Profile and system-status values reflect the PC when captured, not recommended settings or guaranteed results. Scroll Home for quick actions, telemetry, and technical details.*

---

## Quick Start

> **Back up important data before applying tweaks.** System-changing operations may require Administrator privileges. Review each option's warning; do not apply every tweak indiscriminately.

1. Open the [latest release](https://github.com/mnaufalalauddin/Naufal-Windows-Utility/releases/latest) and download `Naufal-Windows-Utility-Setup-8.0.0.0-x64.exe`. Compare its SHA-256 with the attached `SHA256SUMS.txt`. Alternatively, [build from source](#build--develop).
2. Run the installer, open **Naufal Windows Utility**, and review the first-run prerequisites.
3. Choose a catalog or performance profile, read its description, and apply only the changes you need.
4. Check the separate progress window and verification results. For completed changes marked restart-sensitive, the app offers **Restart now** or **Later** after active tasks finish. Save your work before accepting. Choosing Later does not schedule a reboot; restart from Windows when convenient.

**Windows 10 / 11 x64 are intended targets.** Feature availability depends on your Windows build, edition, hardware, and installed components. Not every configuration has been validated. Some downloads and app recovery operations require Internet access and WinGet.

**The current installer is unsigned.** Verify the download source and any published SHA-256 hash; do not disable system protection just to run it. GitHub's **Code → Download ZIP** downloads source code, not a ready-to-run application.

---

## What's Included

| Category | Highlights |
| --- | --- |
| **Repair** | Full Repair, Quick Repair, Windows Update Fix, Microsoft Store Fix, and Explorer Fix. |
| **System** | Disk dashboard with device-derived SMART health, NVMe endurance/read-write/error counters, ATA/SAT attributes and Windows reliability fallback; system reports with opt-in local IP/MAC addresses; Windows / Office activation status tools. A valid license is still required. |
| **Tweaks & De-Bloat** | Essential and Gaming catalogs, service controls, privacy and advertising policies, and supported AI-related settings. |
| **Built-in Windows Apps** | Alphabetical app removal / recovery, including OneDrive, with Recommended, Optional, and Not Recommended removal guidance. |
| **Security & Compatibility** | BitLocker Manager, automatic device-encryption policy, Defender controls, Smart App Control, GPU Driver Manager, runtime checks, and MSI Mode Utility. |
| **Monitoring & Interface** | CPU, RAM, GPU 3D and network graphs; Task Monitoring; per-operation progress; Light/Dark themes; text scaling; an English-only interface; and five sidebar pages. |
| **Development diagnostics** | Before/after resource measurements, read-only background ownership, catalog action inventory, conflict checks, and durable operation journals. |
| **Development servicing** | Individual feature/capability operations, guarded HVCI/LSA controls, and copy-based offline WIM workspaces. Live coverage is limited; see below. |

### Navigation

The sidebar contains **Home**, **System Repair**, **System Info**, **Windows Security**, and **Advanced Windows Tweaks**. Home prioritizes performance profiles and current system status, with Quick Repair, System Report, shader-cache cleanup, and the Legacy Windows Panels launcher. Expand **Live telemetry** for graphs or **Technical details** for diagnostic output. All existing repair and catalog entry points remain available on their corresponding pages.

### Disk health and system reports

- **Choose each disk individually:** view device identity, firmware, temperature, SMART attributes, and health derived from the device's SMART data. The overview retains the original physical/logical disk backend and provider notes.
- **Readable transfer totals:** Total host reads/writes use **TB**, with two decimal places, in both the dashboard and Copy / Save TXT. One TB is 1,000,000,000,000 bytes; original NVMe data-unit counters remain available.
- **NVMe and ATA/SAT:** NVMe logs expose endurance, spare, critical warnings, 128-bit counters, power cycles/hours, media errors, and temperature sensors. ATA/SAT exposes available raw attributes and thresholds. Missing health evidence is **Unknown**, not an assumed Good result.
- **Targeted controller support:** ASMedia ASM2362 and Realtek RTL9210 USB NVMe adapters, an identity-checked Intel RST path, and model-scoped SATA SSD endurance rules are included. These additions are **fixture-tested, not yet verified on matching hardware**. Hidden RAID-member enumeration and universal controller support are not claimed.
- **Optional network addresses:** System Report can show local IPv4/IPv6 and MAC addresses. These are hidden by default and included in Copy / Save TXT only when selected; no public-IP lookup is performed. Other report fields can still contain identifying information.

Availability depends on the drive, controller, firmware, and Windows driver. Endurance is a write-wear estimate, not a prediction of years remaining or a guarantee against failure. See [supported paths, data sources, and limitations](docs/disk-info.md).

## Resource Analyzer (development source)

Open **System Info → Resource Analyzer — Before / After**. Capture a baseline,
apply only your chosen actions through their existing controls, then capture an
after run under comparable workload conditions. Each run takes ten samples at a
requested one-second interval. Reports include physical RAM, commit/limit, CPU,
disk throughput, Windows-volume free space, processes, threads, handles and the
utility's own memory/CPU/thread/handle usage. Copy or Save TXT retains the raw
samples and median/range summaries.

This is read-only measurement, not automatic optimization or proof of causation.
Missing counters remain Unknown. The baseline lasts for the application session;
cross-reboot baseline import is not implemented. Reboot/action notes are user
annotations, not verified state. See the [P0 audit and limitations](docs/performance-p0-audit.md).

### Action audit and background diagnostics (development source)

- **System Info → Action Inventory / Shared Targets** inspects the existing three
  toggle catalogs and their declared registry/service effects. Missing coverage
  is labeled. Conflicting writes are blocked; compatible overlaps with separate
  restore owners remain blocked until their snapshots can be consolidated.
- **System Info → Background Owner Finder — Read-only** reports processes,
  parent evidence, service PID associations and WebView2 owner candidates, with
  Copy / Save TXT. No process is stopped and no shared runtime is removed. Review
  executable paths before sharing a report.
- Catalog toggle operations now write durable local journals. After a journaled
  operation, open catalog scans become stale: use **Analyze / reload** before
  another Apply/Restore. Queued operations recheck the scan generation.
- Shared canonical leaf actions execute once per selected batch, including
  Restore. Independent legacy snapshot owners are not silently merged.
- The legacy combined CPU-mitigation/HVCI override no longer accepts new Apply;
  its original backup/Restore path remains. The new individual HVCI control is
  described below; this is not completion of all requested security controls.

See the [implementation checkpoint and full remaining backlog](docs/backlog-progress.md)
for test evidence, coverage limits and work still required.

### Storage, individual protection and offline images (development source)

Under **Advanced Windows Tweaks**:

- **Storage / Windows Servicing** inventories optional features and capabilities,
  applies an individually confirmed enable/install or disable/remove, and saves
  before/after command evidence. Component-store analysis and normal cleanup are
  separate from irreversible **ResetBase**, which requires typed confirmation.
  Third-party drivers can be inventoried/exported; no driver removal is provided.
  Reserved storage, CompactOS and Windows RE are inspection-only in this panel.
- **Security & Mitigations — Individual Controls** separates configured and running
  DeviceGuard protection. HVCI / Memory integrity supports Enable, Disable and exact
  snapshot Restore only after strict eligibility checks. Unknown locks, policy,
  management indicators or incomplete evidence block writes. VBS, Credential Guard
  and stack-protection evidence is read-only here; generic CPU masks are never used.
  Disabling protection is not presented as a measured performance improvement.
  LSA protection adds a separately confirmed, protection-strengthening control on
  eligible Windows 11 clients. It separates configuration from live process
  protection; automatic LSA Disable/Restore is deliberately unavailable when a
  previous firmware lock cannot be excluded. See [security controls](docs/security-controls.md).
- **Offline Image Workspace** inspects a local WIM, validates its selected client
  index/build/edition/architecture, creates a checksum-verified copy, and mounts
  only that copy. A conservative selection of features, capabilities and provisioned
  apps can be removed. Supported component cleanup, explicit Commit/Discard,
  export to a new WIM and manifest-based recovery are included. No ISO builder,
  arbitrary WinSxS deletion or removal of servicing itself is implemented.

These are **work-in-progress source features**, not a claim that the existing
GitHub release contains them. Most new mutation tests use fakes. User-supplied
disposable-guest logs verify three profile Apply/intentional-rollback cycles, but
not post-reboot effectiveness; RSC was not applicable. HVCI Apply/rollback and image
deployment remain unverified. A guest Print-to-PDF disable/restore cycle was
verified across two reboots; this does not test printing or all storage controls.
Legacy migration on copies passed Advertising ID but blocked conflicting Tailored
Experiences originals. Keep tested backups and original images.
The two shared Advertising ID / tailored-experience registry settings now retain
one authoritative original across Essential and Advanced aliases. Conflicting or
interrupted old snapshots block writes. Other independently owned overlaps remain
blocked; this is not whole-program snapshot consolidation.

See the [1 October continuation report](docs/checkpoint-20261001.md),
[30 September baseline](docs/backlog-20260930.md), and
[disposable VM validation protocol](docs/vm-validation.md) for evidence and remaining work.

### Current verification & known limits

The following records refer to the **1 October 2026 development checkpoint**, not
certification of every feature or a new binary release. Normal regression tests
do not invoke the opt-in live mutation harnesses.

| Area | Evidence and remaining limits |
| --- | --- |
| Regression checks | 4,786 profile/action assertions; 353 English UI checks; 54 LSA, 70 security/offline, 33 storage, 22 VM-isolation and 51 WIM-readiness checks. These use pure logic, fakes or test-owned data, not a full native mutation matrix. |
| Performance profiles | All three reached 23/23 in the disposable guest and returned to the captured baseline after intentional failure. RSC was not applicable; post-reboot profile effectiveness remains unverified. |
| Shared privacy snapshots | Ownership, alternate-catalog restore and retirement checks passed, but both values were already applied: **0/2 changed**. Other modules have not been fully consolidated. |
| Legacy backup migration | Advertising ID migrated on a test-owned copy. Tailored Experiences correctly blocked conflicting original values; production backups were retained. |
| Storage | Print to PDF completed Enabled → Disabled → reboot → Restore → reboot → Enabled. This does not verify printing, other features/capabilities, cleanup or drivers. |
| Security | HVCI Apply/rollback was **not exercised** because eligibility checks blocked it. LSA remained read-only and reported live LSA-light protection. No safety gate was bypassed for coverage. |
| Offline images | Readiness tooling and mocked workflows exist. Real WIM modification/export, deployment to a separate blank virtual disk and first boot remain unverified. |

The final guest audit reached **CompletedWithSkippedControls** and requested no
further reboot. Skipped controls are not successful Apply/rollback tests.

**Known audit-report defect:** the guest summary can print
`LSA read-only baseline=False` because the internal LSA snapshot property is not
retained by the default JSON serialization. This comparison is inconclusive,
not evidence that LSA protection changed. A serialization/reporting fix and a new
comparison test remain outstanding. Do not change protection settings to make
the report pass.

Broader Windows 10/11 and controller coverage, additional snapshot migrations,
and the remaining live security/storage/image tests are still open.

## Performance Profiles

Choose a profile from the dashboard, review its confirmation, and inspect verification results after applying it.

| Profile | Purpose |
| --- | --- |
| **Competitive Gaming** | Apply the project's competitive-gaming configuration. |
| **Optimized Gaming** | Apply an alternative gaming-oriented configuration. |
| **Balanced** | Apply the project's balanced configuration using the Windows Balanced power plan. |

Profiles manage multiple settings, not just the power plan. **No profile guarantees higher FPS or lower latency** on every PC. These are application UI profiles, not command-line automation presets.

<details>
<summary><strong>Important: Apply, OFF, Restore, and safety</strong></summary>

- ON can mean a disabling/removal tweak is applied—not that the underlying Windows feature is enabled. Read the description.
- OFF and Restore have different meanings for some options. Restore uses captured state where available; some options offer documented Windows-default fallbacks. Vendor-specific defaults cannot always be inferred safely.
- Restore all defaults can intentionally discard saved pre-change state for applicable items. Read the confirmation.
- Reinstalling an app does not recover deleted personal data. Recovery can require Internet access, Store availability, and an appropriate license.
- Disabling update, printing, biometric, networking, or security components can interrupt those functions. Security reductions are not general-purpose performance recommendations.
- Retain your BitLocker recovery key securely. Preventing automatic device encryption does not decrypt an already encrypted drive.
- Keep restore backups. A green verification result covers the implemented checks, not every possible side effect.

Default installation: `C:\Program Files\Naufal Tech's Limited\Naufal Windows Utility`.

**Executable:** `Naufal Windows Utility.exe`. The installation folder now matches the application name. The per-user folder and upgrade identity remain unchanged to preserve access to existing preferences and restore backups. If an existing version is registered in the old folder, finish running tasks, close the app, uninstall that version through **Windows Settings → Apps**, then run the new installer. Setup blocks relocation until the old installation is unregistered; it does not move or recursively delete the old folder. Keep your restore backups. An old pinned shortcut may need to be unpinned and pinned again.

Per-user application files: `%LOCALAPPDATA%\Naufal Windows Powertoys`. Some snapshots live in the registry or other feature-specific locations; this folder alone is not a complete backup.

</details>

---

## Build & Develop

Built with **C# / WinUI**, **.NET 10**, **Native AOT**, and **Inno Setup 7**.

Use a Windows x64 development PC with Git, .NET 10 SDK, Visual Studio Windows / WinUI build support, the **Desktop development with C++** workload, MSVC x64 tools, a Windows SDK, and Inno Setup 7. The recorded packaging version is **7.1.0**.

From **PowerShell 7 with the Visual Studio x64 developer environment loaded**:

```powershell
git clone https://github.com/mnaufalalauddin/Naufal-Windows-Utility.git
Set-Location Naufal-Windows-Utility
.\build-installer.ps1
```

The script restores dependencies, publishes the self-contained Native AOT application, includes dependency notices, and builds the installer:

```text
artifacts\installer\Naufal-Windows-Utility-Setup-8.0.0.0-x64.exe
```

Build tools are not required to run the packaged application. Generated binaries and private runtime data are excluded from Git.

<details>
<summary>Debug build, regression checks, and source map</summary>

```powershell
dotnet build ".\Naufal Tech's Windows Powertoys.csproj" -c Debug -p:Platform=x64

dotnet run --project .\Tests\ProfileVerification\ProfileVerification.Tests.csproj
dotnet run --project .\Tests\Localization\Localization.Tests.csproj
.\Tests\ParityAudit\Test-CatalogInteraction.ps1
.\Tests\ParityAudit\Test-ProjectIdentity.ps1
```

The functional and localization checks do not apply Windows tweaks. The separate backend-free native UI test host exercises layout, scaling, theme contrast, and sidebar navigation:

```powershell
.\Tests\HeaderLayout\Test-HeaderLayout.ps1 -NativeAot -Languages en
```

| Source | Purpose |
| --- | --- |
| `MainWindow.xaml`, `MainWindow*.cs` | Dashboard, dialogs, and catalog interaction. |
| `*Service.cs`, catalog and policy files | Inspection, repair, tweaks, app management, and restore behavior. |
| `DiskInfoView.cs`, `NativeDiskSmart*.cs`, `DeviceSmartReport.cs`, `SsdEndurance.cs` | Disk dashboard, read-only SMART transports, health decoding and model-scoped endurance. |
| `NetworkReport.cs`, `SystemReportEntry.cs` | Local network report and opt-in address visibility/export. |
| `Catalog*`, `SharedPrivacySnapshot.cs` | Action planning, effect conflicts, journals, scan invalidation and the two migrated shared privacy originals. |
| `ResourceMeasurement.cs`, `NativeResourceProbe.cs`, `BackgroundOwner*.cs` | Read-only before/after measurements and process/service ownership diagnostics. |
| `StorageServicing.cs`, `SecurityMitigation*.cs`, `OfflineImage*.cs` | Guarded servicing, individual protection controls and copy-based image workflows. |
| `EnglishUiText.cs`, `UiTextKeys.cs` | Shared English application/installer copy. |
| `Installer/`, `build-installer.ps1` | Installer definition, packaging, icons, and dependency notices. |
| `Tests/` | Functional, English-only contract, static, and native UI checks. |

To check the actual packaged EXE without applying tweaks, launch it with `--capture-startup-check` from a writable publish folder. It checks startup, English framework resources, the five sidebar pages and real Home reads, exports Light/Dark PNGs plus `startup-check/result.txt`, then closes. It skips the first-run wizard for that run only and does not save the temporary display preferences. Administrator approval is still required by the application's manifest. Do not run this check concurrently with another app instance.

The project file retains its historical filename. Passing tests is not proof that every system-changing operation works on every Windows configuration; use a disposable VM or dedicated test PC for risky changes.

</details>

---

## Interface & Project Status

**English-only interface.** The language selector, saved-language behavior, runtime text replacement, and application-owned non-English catalogs have been removed. Existing theme, scaling, backup, and wizard preferences are preserved. Output returned by Windows or device drivers can retain the operating system's language.

The 27 September 2026 live audit reached **23/23 checks for each of the three performance profiles**, then exercised forced-failure rollback to the captured initial configuration after each profile. This is evidence from one development PC, not a guarantee for every Windows build or device. The opt-in mutation harness is separate from normal regression tests.

**Active development—not declared complete.** Broader device/Windows-version coverage and remaining feature-specific live audits are ongoing. Historical screenshots and guides may show the previous dashboard.

The published 28 September 2026 disk/report checkpoint recorded **4,558 regression assertions**,
**20,068 native UI assertions**, **307 English-only checks**, and **16 static
report-export checks**. Read-only Native AOT probes successfully read two NVMe
SSDs and 27 ATA attributes from one USB drive. The USB drive's overall SMART
health remained Unknown when the driver could not confirm it. These results do
not certify every controller, Windows version, or system-changing operation.
Application version remains **v8.0.0.0**; dated release tags distinguish builds
without replacing earlier release history.

The [28 September refresh build](https://github.com/mnaufalalauddin/Naufal-Windows-Utility/releases/tag/v8.0.0.0-build.20260928.2)
includes neutral SMART report wording, the latest Light/Dark README previews,
and updated documentation. Device calculations and read-only command behavior
are unchanged from the earlier disk-health build. Third-party copyright and
license notices are retained.

---

## Resources

- [Published releases](https://github.com/mnaufalalauddin/Naufal-Windows-Utility/releases)
- [Development history and known limitations](CHANGELOG.md)
- [Report a bug or request a feature](https://github.com/mnaufalalauddin/Naufal-Windows-Utility/issues)
- [Source license](LICENSE) · [Third-party notices](THIRD-PARTY-NOTICES.txt)

## Support & Contribute

If the project helps you, consider starring the repository. Bug reports, English wording improvements, reproducible test cases, and focused pull requests are welcome.

Include your Windows build, app version, Windows display language, selected option, exact steps, and relevant output when reporting a problem. **Remove private information—never upload recovery keys, credentials, product keys, or personal backups.** Preserve restore behavior and add relevant tests when changing system-modifying code.

[View contributors](https://github.com/mnaufalalauddin/Naufal-Windows-Utility/graphs/contributors) · [View pull requests](https://github.com/mnaufalalauddin/Naufal-Windows-Utility/pulls)

---

## License

[MIT License](LICENSE) · Copyright © 2026 **Muhammad Naufal Alauddin**. Provided without warranty. Dependencies retain their own licenses; see [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

Third-party names, trademarks, and artwork remain subject to their owners' rights. This project is independent of Microsoft and Microsoft PowerToys.
