# Naufal Tech's Windows Powertoys

![Naufal Tech's logo](Assets/BrandingSource.png)

A native Windows system-management and tuning application built with C#, WinUI 3,
and .NET 10. This repository contains the development source for **8.0.0.0**,
published under the company metadata **Naufal Tech's Ltd.**

**Development snapshot: 12 September 2026.** This is not a declaration of a
stable release or complete behavioral parity with the original PowerShell tool.
Read the [known limitations](#status-and-safety) before using administrative actions.

## Features represented in this source

- Full/Quick Repair, Windows Update Fix, Microsoft Store Fix, and Explorer Fix.
- Disk/system reports, activation information, security and BitLocker workflows.
- Essential Windows Tweaks, Gaming Tweaks, and Advanced Windows Tweaks & De-Bloat.
- Performance profiles, live gaming/system status, and resource-aware task queuing.
- GPU Driver Manager, MSI Mode Utility, runtime compatibility, and legacy panels.
- Separate per-task/overall progress windows with elapsed time and verified outcomes.
- Light/Dark themes, eight scaling choices from 25% to 200%, and 23 language catalogs.
- Review, selected uninstall, and restore for 31 built-in Store apps. See
  [scope, package identities, and recovery limits](BUILT_IN_APPS.md).

## Build prerequisites

Build on **Windows x64**. The source includes other platform configurations, but
the maintained installer flow and recorded release verification use x64.

- .NET SDK **10.0.400** or a later patch in its feature band (`global.json`).
- Visual Studio / Build Tools compatible with that SDK, including the Windows
  app development/XAML tooling and **Desktop development with C++** for Native AOT.
  See [Microsoft's Native AOT prerequisites](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot).
- A Windows SDK and access to NuGet for the first restore. The project pins
  `Microsoft.WindowsAppSDK` **2.4.0** and
  `Microsoft.Windows.SDK.BuildTools` **10.0.28000.2705**.
- **Inno Setup 7** for the EXE installer; `ISCC.exe` must be installed in a location
  recognized by `build-installer.ps1`, or available on PATH.

The project targets `net10.0-windows10.0.19041.0` and declares a platform minimum
of `10.0.17763.0`. These declarations do not prove support for every old Windows
build, driver, runtime dependency, or tweak. Windows 10/11 runtime testing remains
necessary. Do not change package versions solely to make restore succeed without
checking compatibility and regression results.

## Build the application

Open a Developer PowerShell in this repository directory. Do not copy `bin` or
`obj` from another computer; restore dependencies on the new machine.

```powershell
dotnet --version
dotnet restore "Naufal Tech's Windows Powertoys.csproj" -p:Platform=x64
dotnet build "Naufal Tech's Windows Powertoys.csproj" -c Debug -p:Platform=x64 --no-restore
```

Open `Naufal Tech's Windows Powertoys.slnx` in a compatible Visual Studio if you
prefer the IDE. The solution contains the application; tests are separate projects.
Build commands compile code; they do not run repair/tweak actions. Running the
application requests Administrator privileges. Do not use `dotnet App.dll` as a
replacement for launching the correctly bootstrapped WinUI executable.

## Publish and create Setup

```powershell
powershell -ExecutionPolicy Bypass -File .\build-installer.ps1
```

This performs Native AOT publication, stages and verifies the complete application
payload, checks its icons, and compiles Setup. It does **not** install Setup.

- Application stages: `artifacts/publish/win-x64-<timestamp>/`
- Application name: `Naufal Windows Powertoys.exe`
- Installer: `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`
- Default installation folder:
  `C:\Program Files\Naufal Tech's Limited\Naufal Windows Powertoys`
- App-owned user data: `%LOCALAPPDATA%\Naufal Windows Powertoys`

Do not override `PublishDir` casually: the staged build script accounts for the
previous Windows App SDK MSB3094 copy-target issue. `-SkipPublish` requires an
existing completed, version/hash-verified stage, which is not supplied in this
source-only repository. `-NoRestore` is only for an already-restored configuration.

More detail: [Installer guide](Installer/README.md).

## Run regression checks

The following two console suites use synthetic/test data by default; do not add
the optional machine-probe flags when you only want regression tests.

```powershell
dotnet restore .\Tests\ProfileVerification\ProfileVerification.Tests.csproj --configfile .\Tests\ProfileVerification\NuGet.Config
dotnet run --project .\Tests\ProfileVerification\ProfileVerification.Tests.csproj --no-restore
dotnet restore .\Tests\Localization\Localization.Tests.csproj --configfile .\Tests\ProfileVerification\NuGet.Config
dotnet run --project .\Tests\Localization\Localization.Tests.csproj --no-restore
```

Additional source/assets/staging checks (Windows PowerShell):

```powershell
.\Tests\ParityAudit\Test-CatalogInteraction.ps1
.\Tests\ParityAudit\Test-AppDataRouting.ps1
.\Tests\ParityAudit\Test-InstallerLocation.ps1
.\Tests\ParityAudit\Test-PublishStage.ps1
.\Tests\ParityAudit\Test-ReportExport.ps1
.\Tests\ParityAudit\Test-AppIcons.ps1
```

`Inspect-StaticParity.ps1` is optional and requires a private reference directory
containing the original `V78.ps1` and `Naufal Windows Powertoys V7.8.exe`. Neither
file is required to compile this application, and neither is distributed here:

```powershell
.\Tests\ParityAudit\Inspect-StaticParity.ps1 -ReferenceDirectory 'D:\PrivateReference'
```

See [source-package verification](SOURCE_PACKAGE.md) for this export's checks and
[CHANGELOG.md](CHANGELOG.md) for historical development results. Assertions are
not counts of independently tested user features or Windows mutations.

## Repository layout

| Path | Purpose |
| --- | --- |
| `*.cs`, `*.xaml` | Application logic, UI, localization catalogs, native interop and services |
| `*.csproj`, `*.slnx` | Build and solution definitions |
| `app.manifest`, `Package.appxmanifest` | Windows application/package metadata |
| `Assets/` | Branding source and derived PNG/ICO assets required by the application |
| `Properties/` | Shared launch settings and publish profiles, not private IDE state |
| `Installer/`, `build-installer.ps1` | Installer definition, staged publishing and icon tooling |
| `Tests/` | Regression suites, source checks and optional read-only diagnostic probes |
| `CHANGELOG.md`, dated `*AUDIT*.md` and other notes | Development evidence and known limitations |
| `Collect-CrashDiagnostics.ps1` | Optional diagnostic collector; generated output is private |

No compiled EXE/DLL, NuGet cache, `.vs`, `bin`, `obj`, runtime backups, logs, signing
keys, or original reference binaries are included. The PNG/ICO files are source
assets, not generated runtime cache. Historical audit notes contain prior build
identities; the old output files and private analysis workspace are not included.

## Status and safety

The application contains privileged service, registry, boot, package, network,
driver, and security operations. Review code and warnings and use disposable
Windows test machines/VM snapshots before trusting Apply/Restore on a real PC.

- Blank native UI text and an earlier unconfirmed native error dialog remain
  recorded open issues; this source export does not fix them.
- Source/routing and regression coverage do not certify 1:1 behavior on all
  Windows 10/11 versions or hardware.
- Documented default restore is scoped; unknown vendor defaults are not guessed.
- Restoring a removed Store app does not restore deleted personal app data.
- Language coverage is not a professional linguistic or full visual certification.
- Current application/installer outputs are unsigned. Setting Company/Publisher
  metadata does not sign a file.

## Upload, licensing and third-party dependencies

Follow [GITHUB_UPLOAD.md](GITHUB_UPLOAD.md) to upload the extracted source folder.
No repository has been created and no files have been uploaded by this export.

**No open-source license has been selected or added.** The owner should choose
the intended license before presenting this as an open-source release. This
package does not automatically assign MIT, GPL, or another redistribution license.
Review ownership/redistribution of code and artwork before making the repository
public. NuGet packages and Microsoft/vendor components retain their own terms;
they are restored separately, not bundled as source in this export.
