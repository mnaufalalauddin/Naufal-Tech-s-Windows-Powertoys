<p align="center">
  <img src="Assets/BrandingSource.png" alt="Naufal Tech's logo" width="180">
</p>

<h1 align="center">Naufal Tech's Windows Powertoys</h1>

<p align="center">
  A native Windows dashboard for system repair, tuning, app management and live performance monitoring.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/source-8.0.0.0-2563eb?style=flat-square" alt="Source version 8.0.0.0">
  <img src="https://img.shields.io/badge/build-Windows_x64-0078d4?style=flat-square" alt="Windows x64 build">
  <img src="https://img.shields.io/badge/UI-WinUI_3-7952b3?style=flat-square" alt="WinUI 3">
  <img src="https://img.shields.io/badge/runtime-.NET_10-512bd4?style=flat-square" alt=".NET 10">
  <img src="https://img.shields.io/badge/status-in_development-d97706?style=flat-square" alt="In development">
</p>

<p align="center">
  <a href="https://github.com/mnaufalalauddin/Naufal-Tech-s-Windows-Powertoys/releases">Published releases</a>
  · <a href="#quick-start">Quick start</a>
  · <a href="#explore-the-toolbox">Features</a>
  · <a href="#documentation">Documentation</a>
  · <a href="https://github.com/mnaufalalauddin/Naufal-Tech-s-Windows-Powertoys/issues">Report an issue</a>
</p>

### Dashboard preview

| Light Mode | Dark Mode |
| :---: | :---: |
| ![Main dashboard in Light Mode, showing repair and tuning menus, performance profiles, and live CPU, memory, GPU and network graphs](docs/images/dashboard-light.png) | ![Main dashboard in Dark Mode, showing repair and tuning menus, performance profiles, and live CPU, memory, GPU and network graphs](docs/images/dashboard-dark.png) |

<p align="center"><sub>Actual application screenshots supplied on 14 September 2026. The dashboard uses live system measurements; no performance data is simulated.</sub></p>

> [!IMPORTANT]
> This is development source, not a claim of complete 1:1 parity or a stable release.
> Administrative actions can change services, registry values, boot settings,
> apps and security configuration. Read each warning and test on a backed-up
> system or disposable Windows VM first.

## Quick start

### Try a published build

1. Open [GitHub Releases](https://github.com/mnaufalalauddin/Naufal-Tech-s-Windows-Powertoys/releases)
   and read the release notes.
2. Download the release's **x64 Setup EXE** and verify its published hash, if supplied.
3. Run Setup, then open **Naufal Tech's Windows Powertoys** and review the first-run wizard.
4. Open a catalog and let analysis finish. Review availability, descriptions and
   warnings before selecting an action.

> [!NOTE]
> **Source and installer releases are separate.** As checked on 14 September 2026,
> the published v8.0.0.0 installer predates the latest source fixes, including the
> Light Mode header contrast correction. A matching version number alone does not
> mean two builds contain identical code. Build the current source for these changes.

Current development outputs are **unsigned**. Company metadata is
**Naufal Tech's Ltd.**; it is not a digital signature. Do not bypass a security
warning unless you have independently verified the file and trust its source.

### Build the current source

Open an **x64 Developer PowerShell** with .NET 10, Windows/XAML tooling,
the MSVC C++ toolchain and Inno Setup 7 installed:

```powershell
git clone https://github.com/mnaufalalauddin/Naufal-Tech-s-Windows-Powertoys.git
Set-Location -LiteralPath .\Naufal-Tech-s-Windows-Powertoys
powershell -ExecutionPolicy Bypass -File .\build-installer.ps1
```

The script builds a self-contained **Release x64 Native AOT** application and
creates `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`.
It does not install or run the application.

See the [build and test guide](docs/BUILDING.md) for prerequisites, Debug builds,
offline rebuilds and regression checks.

## Explore the toolbox

| Area | What's inside |
| --- | --- |
| **Repair & diagnostics** | Full/Quick Repair, Windows Update and Microsoft Store repair, Explorer Fix, disk/system reports and activation information. |
| **Windows tuning** | Essential, Gaming and Advanced catalogs with analysis, selected actions, saved-state/default restoration and read-back verification. |
| **Apps & privacy** | 140 A–Z app entries, merged duplicate app families, selected uninstall/restore, and Windows/browser privacy, AI and suggested-content controls. |
| **Gaming & performance** | Competitive Gaming, Optimized Gaming and Balanced profiles; live CPU, RAM, GPU and network history graphs. |
| **Security & devices** | BitLocker Manager, Defender and Smart App Control workflows, GPU Driver Manager, MSI Mode Utility and legacy Windows panels. |
| **Task visibility** | Task Monitoring for active work, resource-aware queuing, and separate per-task/overall progress and result windows. |
| **Personalization** | Light/Dark themes, 25–200% scaling with Ctrl+0 reset, and 23 language catalogs. |

### Review before applying

- **Analyze first:** unavailable components are identified separately from failures.
  An absent feature is not a reason to force a change.
- **Choose deliberately:** read the option's description and warning, then select
  only the changes you need. Some operations are immediate action buttons.
- **Watch the outcome:** progress windows distinguish working, completed,
  unavailable and failed/unverified results.
- **Restore with context:** saved state is preferred where supported; documented
  defaults are scoped. Restore cannot recreate deleted personal app data.

### Built-in Windows Apps: removal legend

| Badge | Meaning |
| --- | --- |
| 🟢 **Recommended** | Lower-impact removal candidates if you do not use them. Not a guarantee that removal suits every user. |
| 🟡 **Optional** | Personal-choice apps or features; check your workflow before removing. |
| 🔴 **Not recommended** | Keep by default: removal can disrupt dependent features, hardware utilities, accessibility or sign-in/gaming workflows. |

The expanded list includes optional Store, OEM and third-party apps as well as
Windows apps. Only entries found on the current PC can be acted on. Teams
generations and other duplicate identities are grouped into one option.

[Read the app catalog and restore notes →](BUILT_IN_APPS.md)

## Documentation

- [Build, publish and test](docs/BUILDING.md) — prerequisites, commands and source layout.
- [Installer guide](Installer/README.md) — packaging, deployment and signing boundaries.
- [Development changelog](CHANGELOG.md) — dated changes and verification evidence.
- [App and privacy catalog audit](CATALOG_EXPANSION_2026-09-14.md) — identities, policy scope and recovery limits.
- [Microsoft Store source consent](COPILOT_STORE_CONSENT_2026-09-14.md) — explicit agreement flow for Copilot/Windows AI.
- [Light Mode header fix](LIGHT_THEME_HEADER_FIX_2026-09-14.md) — reproduction, contrast checks and build evidence.
- [Native header regression host](Tests/HeaderLayout/README.md) — theme, language and scaling layout tests.

<details>
<summary><strong>Compatibility, restoration and known limitations</strong></summary>

- The intended platform is Windows 10/11. The maintained build/installer flow is
  x64; declared target/minimum versions do not prove every feature works on every
  Windows build, edition or hardware configuration.
- Some policies require a specific Windows/browser version or edition. New long
  descriptions may fall back to English; 23 catalogs are not a professional
  linguistic or full visual certification.
- Unknown vendor defaults are not guessed. Restoring Store apps depends on
  availability, device eligibility and licensing; retired apps may not be recoverable.
- BitLocker automatic-encryption prevention affects future automatic encryption.
  It does not decrypt existing drives or remove recovery keys.
- Legacy Photo Viewer registration supports PNG/JPG handlers; choosing a default
  app remains a user-confirmed Windows Settings action.
- The specific Light Mode header contrast defect has a regression fix. Historical
  reports of other blank UI text and an unconfirmed native error are not declared
  resolved by that fix.
- Passing builds and synthetic assertions do not certify all privileged actions,
  full original-tool parity or every install/upgrade/uninstall scenario.

</details>

## Feedback & contributions

[Open an issue](https://github.com/mnaufalalauddin/Naufal-Tech-s-Windows-Powertoys/issues)
with the build date/hash, Windows edition/build, menu/option, exact steps and
expected versus actual outcome. Screenshots and **redacted** logs help.
Do not upload recovery keys, access tokens, private file paths or personal backups.

For a source contribution, use a branch, keep the change focused, run relevant
tests and describe what was **not** tested. Never run destructive tweaks merely
to validate a documentation or UI change.

## Project & licensing

Developed by **Naufal Tech's Ltd.** This is an independent project, not Microsoft
PowerToys and not affiliated with or endorsed by Microsoft.

**No open-source license has been selected.** Public source availability does not
grant an MIT/GPL or other license. Third-party dependencies and artwork retain
their respective ownership and terms.
