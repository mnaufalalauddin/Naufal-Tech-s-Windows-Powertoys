# Publishing and building the Setup installer

## Publisher and verification boundary

The application Company, package PublisherDisplayName, installer AppPublisher,
and installer VersionInfoCompany use **Naufal Tech's Ltd.**. The stable
installer AppId and package signing identity are unchanged for upgrade safety.
This is display/version metadata; the current app and Setup are **unsigned**.
No certificate is generated or installed by the build script. A signed release
requires a separately authorized code-signing setup.

The latest Restore audit is `RESTORE_DEFAULTS_AUDIT_2026-09-09.md`. A successful publish/installer
compile does not certify 1:1 behavior, startup, or install/upgrade/uninstall.
The generated Setup still contains the behavioral gaps listed in that audit.

## Prerequisite

Install the 64-bit edition of Inno Setup 7:

```powershell
winget install --id JRSoftware.InnoSetup.7 -e -s winget -i
```

## One-command build

Open PowerShell in the project directory, then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-installer.ps1
```

By default the script restores the packages needed by the Release Native AOT
configuration. If Visual Studio has already restored them and NuGet is offline,
run the script with `-NoRestore`.

The script performs two stages:

1. Publishes the WinUI 3 application for `win-x64` as self-contained Native AOT.
2. Compiles all required application files into a single Setup executable.

The installer is generated under:

```text
artifacts\installer\Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe
```

The Native AOT output is first produced in the normal Windows App SDK publish
directory and then copied into a timestamped `artifacts\publish\win-x64-*`
staging directory. This avoids the Windows App SDK `MSB3094` copy-target error
that can occur when `PublishDir` is redirected directly to an arbitrary folder.

The installed application files are placed in Program Files, Start Menu and
optional Desktop shortcuts are created, and Windows receives a standard
Apps > Installed apps uninstall entry.

The Native AOT `.pdb` file is intentionally excluded from the installer.
Keep it privately if symbols are needed for diagnosing a crash.

## Visual Studio publish only

To create only the application folder in Visual Studio:

1. Select `Release` and `x64`.
2. Right-click the project and choose **Publish**.
3. Select the `win-x64` folder profile.
4. Choose **Publish**.

The project file already enables Native AOT, self-contained .NET, unpackaged
WinUI 3, and self-contained Windows App SDK deployment for Release builds.
