# Naufal Windows Powertoys

**An open-source Windows maintenance, troubleshooting, tweaking, and gaming utility.**

Bring repair tools, system information, performance profiles, privacy controls, and app management into one native Windows dashboard—with operation progress and result verification.

Developed by **Muhammad Naufal Alauddin** · Version **8.0.0.0** · [MIT License](LICENSE)

[Features](#features) · [Getting started](#getting-started) · [Build from source](#build-from-source) · [Development status](#development-status) · [Changelog](CHANGELOG.md) · [Report an issue](https://github.com/mnaufalalauddin/Naufal-Tech-s-Windows-Powertoys/issues)

> This is an independent project, not Microsoft PowerToys, and is not affiliated with or endorsed by Microsoft. It is under active development. Advanced options can change services, security settings, boot configuration, and installed apps; review each warning before applying a change.

## Preview

### Dark theme

![Dark dashboard with repair tools, performance profiles, and live CPU, RAM, GPU, and network graphs](docs/images/dashboard-dark.png)

<details>
<summary>View the light theme</summary>

![Light dashboard with repair tools, performance profiles, and live system graphs](docs/images/dashboard-light.png)

</details>

These screenshots show an earlier development build. Labels, branding, and available controls may differ from the current source.

## What is this project?

Naufal Windows Powertoys is a native C# / WinUI desktop application for people who want to inspect, maintain, and customize their Windows PC from a single interface. It builds on the project's earlier PowerShell-based utility and is being audited for functionality, restore behavior, accessibility, and localization.

It is **not a one-click guarantee of better performance**. Results depend on the Windows version, hardware, drivers, installed components, and the settings you choose. Use individual options deliberately instead of applying every tweak.

## Features

| Area | Included tools |
| --- | --- |
| Repair and maintenance | Full Repair, Quick Repair, Windows Update Fix, Microsoft Store Fix, and Explorer Fix. |
| System information | Disk information, system reports, and Windows / Office activation status tools. A valid license is still required; this project does not provide one. |
| Performance profiles | Competitive Gaming, Optimized Gaming, and Balanced profiles, with checks for the settings each profile manages. |
| Live monitoring | CPU, RAM, GPU 3D, and network history graphs, plus process count, uptime, power-plan, and gaming-setting status. |
| Essential and Gaming tweaks | Windows behavior, Game Mode, storage, power, input, network, and other catalog options with descriptions and risk indicators. |
| Advanced Windows Tweaks & De-Bloat | Service controls, privacy and advertising policies, supported AI-related controls, and built-in app management. |
| Built-in Windows Apps | Alphabetically organized app removal / recovery options, including OneDrive, with Recommended, Optional, and Not Recommended removal guidance. Availability varies by PC. |
| Security and compatibility | BitLocker Manager, automatic device-encryption policy control, Defender controls, Smart App Control, GPU Driver Manager, runtime checks, and MSI Mode Utility. |
| Interface and workflow | Light/Dark themes, text scaling, 23 language choices, first-run setup, Task Monitoring, separate operation-progress windows, and result/log export. |

Some options require administrator privileges, a restart, Internet access, WinGet, or a supported Windows edition. A missing component is not the same as an execution failure; supported catalog paths report confirmed unavailability separately.

## Getting started

The current installer workflow targets **Windows x64**. Windows 10 and Windows 11 are intended targets, but individual features depend on the OS build, edition, hardware, and installed components. The project declares a minimum Windows build of 17763; that declaration is **not** proof of complete compatibility testing on every supported build. There are no Linux or macOS builds.

1. Check the repository's [Releases](https://github.com/mnaufalalauddin/Naufal-Tech-s-Windows-Powertoys/releases) for a maintainer-published installer. If no suitable binary is attached, use the build instructions below. **Code → Download ZIP downloads source, not an installer.**
2. Back up important files. For changes involving encryption, securely retain your BitLocker recovery key outside this application.
3. Run the installer and review first-run prerequisites. Approve elevation only when you intend to perform the requested system operation.
4. Open a catalog, read its descriptions and warnings, and start with a small number of changes. Review the progress window and verification results before proceeding.

The most recently recorded installer is **unsigned**. Windows may display publisher or reputation warnings. Do not disable system protection just to run a download; verify its source and, when provided, compare its SHA-256 hash with the maintainer's published value.

The default installation directory is:

```text
C:\Program Files\Naufal Tech's Limited\Naufal Windows Powertoys
```

Application-owned per-user files use `%LOCALAPPDATA%\Naufal Windows Powertoys`. Some restore snapshots are held in the registry or other feature-specific locations; copying this folder alone is not a complete system backup.

### Apply, OFF, and Restore

- **Apply** requests the selected change. A toggle's meaning depends on the option: ON can mean that a disabling/removal tweak is applied, not that the underlying Windows feature is enabled. Read its description.
- **OFF** and **Restore** are not interchangeable for every option. Feature switches and snapshot-based tweaks have different behavior.
- **Restore** uses saved pre-change state where available. Some options provide a documented Windows-default fallback; not every vendor setting has a safe universal default.
- **Restore all defaults** can intentionally discard saved pre-change state for applicable items. Read its confirmation carefully.
- Reinstalling a removed app does **not** recover its deleted personal data. Store availability, Internet access, and an appropriate license may be required.
- Do not delete backup data while you may still need to restore changes. A green verification result covers the checks implemented for that option, not every possible side effect.

Disabling update, printing, biometric, networking, or security components can interrupt those functions. Disabling protection or changing BitLocker policies is not recommended as a general performance optimization. Preventing automatic device encryption does not itself decrypt an already encrypted drive.

## Languages

The interface offers 23 languages:

English, Indonesian, German, French, Arabic, Tagalog, Vietnamese, Simplified Chinese, Traditional Chinese, Thai, Russian, Ukrainian, Portuguese, Japanese, Korean, Urdu, Tamil, Hindi, Malay, Javanese, Balinese, Swedish, and Spanish.

Localization includes catalog descriptions, confirmations, results, and About information, with ongoing work on remaining runtime text. **Complete translation coverage is not yet claimed.** Technical identifiers, product names, and external diagnostic output may remain in their original form. The installer's program-information page has 23 language choices; its standard setup UI is not fully localized into all 23 languages.

## Build from source

### Prerequisites

- A Windows x64 development PC and Git.
- **.NET 10 SDK**.
- **Visual Studio with Windows / WinUI build support**, the **Desktop development with C++** workload, MSVC x64 tools, and a Windows SDK. Native AOT publishing requires the native linker.
- **PowerShell 7**, with the Visual Studio **x64** developer environment loaded.
- **Inno Setup 7** for the `.exe` installer. The latest recorded packaging run used Inno Setup **7.1.0**.
- Internet access for the initial dependency restore.

Clone the repository:

```powershell
git clone https://github.com/mnaufalalauddin/Naufal-Tech-s-Windows-Powertoys.git
Set-Location Naufal-Tech-s-Windows-Powertoys
```

Build a Debug version:

```powershell
dotnet build ".\Naufal Tech's Windows Powertoys.csproj" -c Debug -p:Platform=x64
```

Build the self-contained Release Native AOT application and installer:

```powershell
.\build-installer.ps1
```

Run the packaging command from the repository root in the x64 developer environment. The script reads the version from the project, publishes the application, collects dependency notices, checks branding assets, and invokes Inno Setup. Build tools are not required on a user's PC to run the packaged self-contained application.

For the current project version, the installer is written to:

```text
artifacts\installer\Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe
```

Generated binaries, logs, and publish directories are excluded from Git. The source project keeps its historical filename even though the product's display name is now **Naufal Windows Powertoys**.

### Run automated checks

From the repository root:

```powershell
dotnet run --project .\Tests\ProfileVerification\ProfileVerification.Tests.csproj
dotnet run --project .\Tests\Localization\Localization.Tests.csproj
.\Tests\ParityAudit\Test-CatalogInteraction.ps1
.\Tests\ParityAudit\Test-ProjectIdentity.ps1
```

These checks cover functional logic, localization resources, catalog routing, and project metadata without applying Windows tweaks. A separate backend-free native UI test host checks layout, themes, text scaling, and language switching:

```powershell
.\Tests\HeaderLayout\Test-HeaderLayout.ps1 -NativeAot -Languages en,id,ar,ur
```

Passing automated tests does not prove that every system-changing operation works on every Windows configuration. Test risky changes in a disposable VM or dedicated test PC, not on a production machine.

## Source map

| Location | Purpose |
| --- | --- |
| `MainWindow.xaml` and `MainWindow*.cs` | Dashboard, dialogs, and catalog interaction. |
| `*Service.cs` and catalog / policy files | Repair, inspection, tweak, app-management, and restore behavior. |
| `UiTranslation*.cs` and `NativeUiCatalog*.cs` | Localization infrastructure, translations, and dynamic display templates. |
| `Installer/` and `build-installer.ps1` | Setup definition, packaging, icon generation, and license collection. |
| `Tests/` | Functional, localization, static, and native UI regression checks. |
| `Assets/` and `docs/images/` | Application branding and README screenshots. |
| [CHANGELOG.md](CHANGELOG.md) | Development history and dated verification checkpoints. |

## Development status

**Active development — audited in stages, not declared complete.**

The 21 September 2026 checkpoint records successful Native AOT / installer builds, 394,160 localization assertions, 4,390 functional assertions, and native layout checks across all 23 languages. These are dated results, not a claim that a live CI pipeline is currently passing.

Work still includes auditing dynamic messages and application-authored logs, completing installer localization, and live validation of Windows Photo Viewer image opening. Broader Windows-version and hardware coverage remains important. See the [changelog](CHANGELOG.md) for evidence, limitations, and previous fixes.

## Feedback and contributions

[Open an issue](https://github.com/mnaufalalauddin/Naufal-Tech-s-Windows-Powertoys/issues) for a bug or suggestion. For a reproducible report, include:

- Windows version/build, app version, language, and theme.
- The catalog and option involved, the steps taken, and expected versus actual behavior.
- Whether the operation was Apply, OFF, Restore, or Restore defaults.
- Relevant exported output or screenshots, with private information removed.

Never post recovery keys, credentials, tokens, product keys, or private backup contents. Translation corrections and focused pull requests are welcome. Preserve restore behavior, add relevant tests, and describe any change to system-modifying logic.

## License and acknowledgments

Project source is provided under the [MIT License](LICENSE), copyright © 2026 Muhammad Naufal Alauddin, without warranty. Third-party components retain their own licenses; see [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt). Packaged builds include collected dependency notices.

Windows, Microsoft product names, and other third-party trademarks belong to their respective owners. This project's license does not grant rights to those trademarks or third-party artwork.
