# Build, publish and test

[← Back to the main README](../README.md)

## Prerequisites

Build on **Windows x64**. The source includes other platform configurations,
but the maintained installer flow and recorded Release verification use x64.

- .NET SDK **10.0.400** or a later patch in its feature band; see [global.json](../global.json).
- Visual Studio / Build Tools compatible with that SDK, Windows app development/XAML
  tooling, and **Desktop development with C++** for Native AOT.
  See [Microsoft's Native AOT prerequisites](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot).
- A Windows SDK and NuGet access for the first restore. The project pins
  `Microsoft.WindowsAppSDK` **2.4.0** and
  `Microsoft.Windows.SDK.BuildTools` **10.0.28000.2705**.
- **Inno Setup 7** for Setup EXE compilation; `ISCC.exe` must be at a location
  recognized by [build-installer.ps1](../build-installer.ps1) or on PATH.

The project targets `net10.0-windows10.0.19041.0` and declares a platform minimum
of `10.0.17763.0`. These declarations are not proof of compatibility with every
older Windows build, runtime dependency, driver or tweak. Test on the intended
Windows 10/11 configurations. Do not change pinned packages merely to make
restore succeed without checking compatibility.

## Debug build

Open an **x64 Developer PowerShell** in the repository directory.
Do not copy `bin` or `obj` from another PC.

```powershell
dotnet --version
where.exe link
where.exe cl
dotnet restore "Naufal Tech's Windows Powertoys.csproj" -p:Platform=x64
dotnet build "Naufal Tech's Windows Powertoys.csproj" -c Debug -p:Platform=x64 --no-restore
```

For Native AOT, ensure the compiler/linker target x64 rather than the
`Hostx86\x86` toolchain. Select an x64 development environment in Visual Studio.

Alternatively, open `Naufal Tech's Windows Powertoys.slnx` in a compatible Visual
Studio. The solution contains the app; tests are separate projects.

Compilation does not execute repair/tweak actions. Running the application
requests Administrator privileges. Do not substitute `dotnet App.dll` for
launching the correctly bootstrapped WinUI executable.

## Release Native AOT + Setup

```powershell
powershell -ExecutionPolicy Bypass -File .\build-installer.ps1
```

The script publishes the updated native application, stages and verifies the
complete payload, checks icons and compiles Setup. It does **not** install it.

| Output or location | Path |
| --- | --- |
| Completed application stages | `artifacts/publish/win-x64-<timestamp>/` |
| Application | `Naufal Windows Powertoys.exe` inside the completed stage |
| Setup | `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe` |
| Default install directory | `C:\Program Files\Naufal Tech's Limited\Naufal Windows Powertoys` |
| App-owned user data | `%LOCALAPPDATA%\Naufal Windows Powertoys` |

- `-NoRestore` is only for an already-restored configuration.
- `-SkipPublish` requires an existing completed, version/hash-verified stage with
  unchanged application inputs. A source-only clone does not supply such a stage.
- Do not casually override `PublishDir`: staged publishing handles the previous
  Windows App SDK `MSB3094` copy-target issue.
- Keep matching native symbols privately for crash diagnosis; the installer
  excludes PDB files.
- Company/Publisher metadata does not sign a file. Current recorded development
  outputs are unsigned. Building does not authorize installation or release upload.

For packaging details, see [Installer/README.md](../Installer/README.md).

## Regression checks

### Functional and localization suites

These console suites use synthetic/test data by default. Do not add optional
machine-probe flags when you only want regression tests.

```powershell
dotnet restore .\Tests\ProfileVerification\ProfileVerification.Tests.csproj --configfile .\Tests\ProfileVerification\NuGet.Config
dotnet run --project .\Tests\ProfileVerification\ProfileVerification.Tests.csproj --no-restore
dotnet restore .\Tests\Localization\Localization.Tests.csproj --configfile .\Tests\ProfileVerification\NuGet.Config
dotnet run --project .\Tests\Localization\Localization.Tests.csproj --no-restore -- --test-only
```

### Native header theme/layout tests

This backend-free WinUI host links real UI/scaling/theme code, uses test-local
preferences and does not run repairs or tweaks.

```powershell
dotnet restore .\Tests\HeaderLayout\HeaderLayout.Tests.csproj -p:Platform=x64
& .\Tests\HeaderLayout\Test-HeaderLayout.ps1 -StartupTheme Dark
& .\Tests\HeaderLayout\Test-HeaderLayout.ps1 -NoBuild -StartupTheme Light -StartupScale 25
```

See [the test host guide](../Tests/HeaderLayout/README.md) for coverage and limits.

### Source/assets/staging checks

```powershell
.\Tests\ParityAudit\Test-CatalogInteraction.ps1
.\Tests\ParityAudit\Test-AppDataRouting.ps1
.\Tests\ParityAudit\Test-InstallerLocation.ps1
.\Tests\ParityAudit\Test-PublishStage.ps1
.\Tests\ParityAudit\Test-ReportExport.ps1
.\Tests\ParityAudit\Test-AppIcons.ps1
```

Optional static parity inspection requires private copies of the original
`V78.ps1` and `Naufal Windows Powertoys V7.8.exe`; neither is needed to build
and neither is distributed in this source repository.

```powershell
.\Tests\ParityAudit\Inspect-StaticParity.ps1 -ReferenceDirectory 'D:\PrivateReference'
```

Assertion counts describe test checks, not independently tested user features or
successful Windows mutations. Build, native UI interaction, and privileged-action
testing have different verification boundaries.

## Source layout and privacy

| Path | Purpose |
| --- | --- |
| `*.cs`, `*.xaml` | Application logic, UI, localization, interop and services |
| `*.csproj`, `*.slnx` | Build and solution definitions |
| `app.manifest`, `Package.appxmanifest` | Windows application/package metadata |
| `Assets/` | Branding source and derived PNG/ICO assets |
| `Properties/` | Shared launch settings and publish profiles |
| `Installer/`, `build-installer.ps1` | Installer definition, staging and icon tooling |
| `Tests/` | Regression suites and optional read-only diagnostic probes |
| `docs/` | Contributor documentation and README screenshots, not application payload |
| `CHANGELOG.md`, dated audit notes | Historical development evidence and limits |
| `Collect-CrashDiagnostics.ps1` | Optional diagnostic collector; generated output is private |

Do not commit EXE/DLL outputs, NuGet caches, `.vs`, `bin`, `obj`, runtime backups,
logs, signing keys or private reference binaries. PNG/ICO files in `Assets` are
source assets, not runtime caches. Historical notes may identify old build
hashes; those old binaries and private analysis workspaces are not included.

See [source-package verification](../SOURCE_PACKAGE.md) for the first export and
[the changelog](../CHANGELOG.md) for subsequent work. The initial
[upload guide](../GITHUB_UPLOAD.md) is for importing a fresh source folder:
do not reinitialize this existing checkout or force-push its history.
