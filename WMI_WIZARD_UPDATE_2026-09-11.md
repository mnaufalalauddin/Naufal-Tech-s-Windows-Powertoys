# First-run WMI update — 2026-09-11

## Scope and findings

The old wizard selected WMIC installation by default and ran DISM Add-Capability
with a 12-minute timeout. A failed WMIC installation/probe blocked saving completed
setup even though native application features use WMI/API access, not wmic.exe.
Service query errors were also all described as MISSING, including access denial.

## Changes

- Removed the WMIC installer and executable probe from the first-run service.
  Existing WMIC installations are neither modified nor removed.
- Replaced wizard steps 6/7 with mandatory, read-only WMI system and memory checks
  using the existing NativeRscReader COM implementation (Native AOT compatible).
- System verification requires one Win32_OperatingSystem result with nonempty
  Caption, valid Version and positive BuildNumber. It does not branch on localized
  OS names or confuse Windows 11's NT version 10.0 with Windows 10.
- Memory verification requires one Win32_ComputerSystem result with positive
  TotalPhysicalMemory. It does not require physical DIMM inventory or a page file,
  which can legitimately be absent in virtual machines or user configurations.
- Both reads run off the UI thread, each bounded to 30 seconds for the caller.
  BoundedReadProbe reuses a still-pending read on retry instead of accumulating
  blocked workers. A timed-out native read is not forcibly aborted.
- Timeout is explicitly unconfirmed with a warning stage; HRESULT/provider/access
  errors remain failures. Neither permits saving completed wizard state. Successful
  subsequent readback can pass on retry. No errors are disguised as unavailability.
- Infrastructure diagnostics distinguish service-not-found (1060), query failure,
  and timeout. PRESENT does not claim a demand-start service is running.
- The mandatory WMI checkbox is checked/disabled. Updated description explains
  Windows 10/11 and that WMIC is not required. WinGet's Internet requirement is
  separate from offline WMI verification. The nine-stage progress layout remains.
- Five new display keys have translations in all 23 languages. Older unused WMIC
  translation keys remain for reference compatibility, but no active wizard control
  or backend uses those installation labels. Technical log output remains original.
- Schema 2 and Skip/Don't show again semantics are unchanged. No live settings,
  backups, service configurations, registry values or installed packages were changed.

## Verification

- Debug x64 build: 0 errors, 0 warnings.
- Functional suite: 2,184 assertions, including 64 added WMI/wizard assertions.
  Synthetic cases include Windows 10 17763/19045, Windows 11, localized Caption,
  missing/empty/invalid data, missing/duplicate instances, access/provider errors,
  provider/caller timeouts, retry worker reuse and readiness/state wiring.
- Localization suite: 94,029 assertions across 23 languages; new required keys,
  placeholders, nonblank resources and language round trips included.
- Production WMI reader tested read-only outside the sandbox: Windows 11 Pro
  10.0.28000 and TotalPhysicalMemory=34271703040 both verified.
- Static AppData routing: 18; installer location: 12; catalog interaction: 80;
  publish-stage fixture checks: 10.
- Native AOT publish and Inno Setup compile succeeded. No application/installer
  GUI, real setup mutations or Apply/Restore operation was launched.
- Final application/installer branding: 101 assertions passed. All 168 published
  payload files matched the completed-stage SHA-256 inventory.

Windows 10 execution is not yet tested on a physical machine or VM. Synthetic
compatibility cases are not full OS certification. The previously reported blank
labels/progress rendering is unrelated, unresolved, and not fixed by this change.
This update does not certify every catalog, WMI provider, or operation as 1:1.

## Artifacts

- Stage: `artifacts/publish/win-x64-20260911-193939-479`
- Application: `Naufal Windows Powertoys.exe` (19,623,936 bytes)
- EXE SHA-256: `DF1385C662CD29101A54EB73B40C24C272F64139AAAB6C2AD02E42CC5AAC577D`
- Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`
  (38,011,800 bytes)
- Setup SHA-256: `7CE23250ED6E5D7E2DCFBE10F705CE28FD3AA21741489D304BDF0091C00B62FC`

Version remains 8.0.0.0; company Naufal Tech's Ltd.; silver NT branding retained.
Artifacts are unsigned. Fixed-name Setup output was rebuilt; historical publish
stages remain. The installed application was not overwritten or uninstalled.

Installer default remains `C:\Program Files\Naufal Tech's Limited\Naufal Windows Powertoys`.
An existing installation registered at a different path requires normal uninstall
before relocation. App-owned data remains under `%LOCALAPPDATA%\Naufal Windows Powertoys`.

## Microsoft references

- [WMIC removal and migration to WMI APIs](https://support.microsoft.com/en-us/servicing/os/windows/docs/2025/09/windows-management-instrumentation-command-line-wmic-removal-from-windows): only WMIC is removed, not WMI.
- [Win32_OperatingSystem](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-operatingsystem): OS properties used by the system probe.
- [Win32_ComputerSystem](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-computersystem): TotalPhysicalMemory is read-only and predates Windows 10.
