# Installation and AppData locations — 2026-09-10

## Implemented locations

Default installation:
`C:\Program Files\Naufal Tech's Limited\Naufal Windows Powertoys`.

Application-owned local user data:

```text
%LOCALAPPDATA%\Naufal Windows Powertoys\
    Settings\                 language, theme, text scale, first-run state
    Backups\LegacyV78\        imported Debloat and Performance Lab snapshots
    RuntimeCache\             runtime packages and GPUDrivers subfolder
    Temp\                     app-owned prerequisite/privilege scratch files
    crash.log                 diagnostic errors and migration warnings
```

The publisher metadata remains **Naufal Tech's Ltd.**, version **8.0.0.0**.
The requested **Naufal Tech's Limited** name is the installation parent folder,
not another publisher/company rename.

## Installer behavior

`DefaultDirName` uses the company parent folder. `UsePreviousAppDir=no` prevents
an old flat Program Files installation from silently replacing that default.
Desktop/Start-menu shortcuts, working directories and icons continue using
`{app}`. The existing AppId and AppUserModelId are unchanged.

The x64 uninstall registration was read and still points to
`C:\Program Files\Naufal Windows Powertoys`. `PrepareToInstall` now checks
that registration before installation: if its normalized path differs from
the selected destination, Setup stops with instructions to finish all tasks,
close the app and uninstall the existing version normally first. It does not
execute that uninstaller, move/delete the old directory, or install a second
copy over the same uninstall registration. An upgrade into the same directory
can proceed. The destination remains user-selectable.

This follows Inno Setup's documented
[default directory substitution](https://jrsoftware.org/ishelp/topic_setup_defaultdirname.htm),
[previous-directory reuse](https://jrsoftware.org/ishelp/topic_setup_usepreviousappdir.htm),
[AppId/uninstall identity](https://jrsoftware.org/ishelp/topic_setup_appid.htm)
and [pre-install guard](https://jrsoftware.org/ishelp/topic_scriptevents.htm).

## Data compatibility and safety

`AppDataPaths` centralizes application-owned paths. Only the owning app instance
runs migration, before MainWindow and display preferences are loaded.

- Settings move logically to `Settings`: import priority is the most recent
  native version's `WindowsPowerToysV78` directory, former canonical-root files,
  then `WindowsPowerToysV77`. An existing new destination always wins.
- First Time Wizard JSON is copied intact, preserving Skip/Don't show again.
- The two legacy JSON snapshots used by native Restore are copied byte-for-byte
  into `Backups\LegacyV78`. Restore uses that location when present, otherwise
  the old snapshot path remains readable for compatibility.
- Migration copies through unique temporary files and a non-overwriting rename.
  It never deletes original files or overwrites a captured backup. Read/copy
  failures are logged and can retry on another launch. Access failure or a
  directory collision is not interpreted as a missing snapshot.
- Unknown legacy files and executable service-lock scripts are not imported.
  The old service-lock cleanup still targets the old filename intentionally.
- Windows/vendor caches, Windows temporary-file cleanup targets, machine-wide
  ProgramData state and registry backup identities are unchanged. Registry-held
  snapshots are not exported/moved into AppData by this change.

Old AppData folders are retained for recovery and original-app compatibility.
No live user-data migration was executed in this session: the application was
already running, and direct enumeration of its elevated AppData folders returned
Access denied. Migration is implemented for the next new-version launch and was
tested using isolated synthetic directories only.

## Verification and artifacts

- Debug: **0 errors, 0 warnings**.
- **2,120** functional regression assertions passed, including 30 new path/data
  tests for source priority, idempotence, first-run state, byte preservation,
  non-overwrite, legacy fallback, locked input, retry and temporary-file cleanup.
- **18** static AppData-routing and **12** installer-location assertions passed.
- **93,224** localization, **80** catalog interaction and **10** publish-stage
  assertions passed; **101** final icon assertions passed.
- Native AOT and Setup compilation completed; all **168** payload files validated.
- Stage: `artifacts/publish/win-x64-20260910-225455-440`.
- Application SHA-256:
  `9DD4534A1ACCA8E4D752649C2DE08CBF30FD67C9B048523DF4D6294ADEDAA1FB`.
- Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`.
- Setup: **37,997,142 bytes**, SHA-256
  `14B9B14D0403DFD55063E5440F95FB77E9512203623380F195EB5E9C57845752`.
- Both artifacts remain unsigned. The fixed-name Setup was rebuilt; older
  timestamped application stages remain available.

No native application/installer was launched and no Windows tweak was executed.
Real installer relocation and live-user-data migration are not yet certified.
The reported blank labels/progress, which did not recover after the user moved
the window, remain an unresolved rendering diagnosis. This location update is
not a fix for that separate issue or a claim of full runtime 1:1 parity.
