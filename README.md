# Naufal Windows Utility

[![Source version](https://img.shields.io/badge/Source_Version-v8.0.0.0-0567ff?style=for-the-badge)](CHANGELOG.md)
[![Latest release](https://img.shields.io/github/v/release/mnaufalalauddin/Naufal-Windows-Utility?style=for-the-badge&label=Download)](https://github.com/mnaufalalauddin/Naufal-Windows-Utility/releases/latest)
[![Windows target](https://img.shields.io/badge/Target-Windows_10_%2F_11_x64-0078d4?style=for-the-badge)](#quick-start)
[![License](https://img.shields.io/badge/License-MIT-16803c?style=for-the-badge)](LICENSE)
[![Interface](https://img.shields.io/badge/UI-English-8250df?style=for-the-badge)](#interface--project-status)

A native Windows dashboard to **repair system components**, **inspect disk health and system information**, **manage Windows apps**, **review privacy settings**, and **configure gaming and performance options**—with live monitoring and operation progress in one place.

Developed by **Muhammad Naufal Alauddin**. Independent, open-source, and under active development. **Not affiliated with Microsoft or Microsoft PowerToys.**

![Naufal Windows Utility — dark dashboard](docs/images/dashboard-dark.png)

<details>
<summary>View the light theme</summary>

![Naufal Windows Utility — light dashboard](docs/images/dashboard-light.png)

</details>

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

### Navigation

The sidebar contains **Home**, **System Repair**, **System Info**, **Windows Security**, and **Advanced Windows Tweaks**. Home prioritizes performance profiles and current system status, with Quick Repair, System Report, shader-cache cleanup, and the Legacy Windows Panels launcher. Expand **Live telemetry** for graphs or **Technical details** for diagnostic output. All existing repair and catalog entry points remain available on their corresponding pages.

### Disk health and system reports

- **Choose each disk individually:** view device identity, firmware, temperature, SMART attributes, and health derived from the device's SMART data. The overview retains the original physical/logical disk backend and provider notes.
- **Readable transfer totals:** Total host reads/writes use **TB**, with two decimal places, in both the dashboard and Copy / Save TXT. One TB is 1,000,000,000,000 bytes; original NVMe data-unit counters remain available.
- **NVMe and ATA/SAT:** NVMe logs expose endurance, spare, critical warnings, 128-bit counters, power cycles/hours, media errors, and temperature sensors. ATA/SAT exposes available raw attributes and thresholds. Missing health evidence is **Unknown**, not an assumed Good result.
- **Targeted controller support:** ASMedia ASM2362 and Realtek RTL9210 USB NVMe adapters, an identity-checked Intel RST path, and model-scoped SATA SSD endurance rules are included. These additions are **fixture-tested, not yet verified on matching hardware**. Hidden RAID-member enumeration and full CrystalDiskInfo parity are not claimed.
- **Optional network addresses:** System Report can show local IPv4/IPv6 and MAC addresses. These are hidden by default and included in Copy / Save TXT only when selected; no public-IP lookup is performed. Other report fields can still contain identifying information.

Availability depends on the drive, controller, firmware, and Windows driver. Endurance is a write-wear estimate, not a prediction of years remaining or a guarantee against failure. See [supported paths, data sources, and limitations](docs/disk-info.md).

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

The 28 September 2026 disk/report update passed **4,549 regression assertions**,
**20,068 native UI assertions**, **307 English-only checks**, and **16 static
report-export checks**. Read-only Native AOT probes successfully read two NVMe
SSDs and 27 ATA attributes from one USB drive. The USB drive's overall SMART
health remained Unknown when the driver could not confirm it. These results do
not certify every controller, Windows version, or system-changing operation.
Application version remains **v8.0.0.0**; dated release tags distinguish builds
without replacing earlier release history.

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
