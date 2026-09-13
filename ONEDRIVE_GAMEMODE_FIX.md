# OneDrive scope, Game Mode toggle, and runtime catalog correction

Date: 12 September 2026.

## OneDrive

The reported `0x80131509` came from running WinGet elevated against a per-user
OneDrive installation. Passing `--scope user` selects the package scope; it does
not change the process token.

- One `OneDriveCommandSession` handles source validation and deployment in the
  same context. Machine scope retains the administrator runner. User scope uses
  `SameUserProcessRunner` for both removal and installation/restore.
- An elevated parent obtains the desktop shell token, requires the **same SID
  and session**, and rejects elevated/high-integrity or low-integrity tokens.
  The duplicate primary token is checked again. A different account supplied at
  a UAC credential prompt is not silently substituted with the desktop account.
- The final implementation uses `CreateProcessWithTokenW`. Native probes exposed
  identification-only linked tokens (1346), the service-only privilege requirement
  of `CreateProcessAsUserW` (1314), and insufficient duplicate-token handle access
  (5). These intermediate approaches were replaced; the final elevated probe
  passed with a medium-integrity child and correct account/session.
- Explicit executable path and Windows argv quoting; no command shell,
  scheduled task, credential collection, or elevated retry on failure.
- Standard output/error use temporary delete-on-close handles with bounded
  readback. Installer descendants retaining an output handle do not block pipe
  EOF. Source export has a finite timeout; deployment remains under the existing
  shared gate and is not killed just to advance the next item.
- Original scope journal, official-source validation, exact package ID and fresh
  installation inventory verification remain in place. No sync folders or
  personal OneDrive files are enumerated or removed by this tool.

## Game Mode

- Its switch represents the actual Windows feature, not a generic applied tweak.
  OFF routes to `SetStateAsync(false)` and writes `AutoGameModeEnabled=0`.
  ON writes `1`, even if an earlier saved snapshot says OFF.
- The first-change snapshot is retained across ON/OFF changes. Only explicit
  Restore invokes saved-state/default recovery; Restore remains available when
  the current feature state is OFF. Other catalog switches keep their existing
  apply/restore semantics.
- The confirmation identifies Game Mode ON or OFF and no longer explains it as
  a disabled-service tweak. Progress follows Applying, not Restoring.

## Runtime catalog

Removed Microsoft Edge WebView2 Runtime from analysis, runtime ID/source mapping,
and installer definitions in Games Runtime & Compatibility Check. This does not
uninstall WebView2 from Windows or remove WinUI's package dependencies.

## Verification and delivery limits

- Debug application build: zero errors and warnings.
- Functional tests: 3,313 assertions; synthetic Windows mutations only.
- Localization tests: 97,662 assertions across 23 languages.
- Static interaction/monitoring checks: 80 + 26; new scope/toggle/runtime wiring: 13.
- Native process probe: unelevated parent passed; elevated parent passed, including
  same-account/session medium integrity, stdout/stderr, exit code, and six literal
  argument cases. The actual WinGet execution alias and official-source export
  also passed in standard-user context, including from an elevated parent.
  This is not a real OneDrive uninstall/install test.
- No user Game Mode setting, OneDrive installation, or personal data was changed
  by verification. Native Game Mode UI clicks and Windows 10 execution remain
  untested in this session.
- Follow-up on 12 September 2026: the user installed Visual Studio Community
  18.10.0 / MSVC 14.51.36231 and .NET SDK 10.0.401. Hostx64/x64 linker detection
  passed. Restoring the new runtime pack resolved the initial NETSDK1112 error.
- Native AOT publish now succeeds. Complete, hash-verified stage:
  `artifacts/publish/win-x64-20260912-220437-859`. The EXE is 20,245,504 bytes,
  version 8.0.0.0, company Naufal Tech's Ltd., unsigned. SHA-256:
  `08F32A78C5133DAE9C66FAE9B9E7D2576A5E1AD5D8E97CD24A56E4739FB5C6A1`.
  The staged payload inventory and 91 icon assertions passed. Functional,
  localization and all 119 static wiring assertions passed again.
- Follow-up packaging on 12 September 2026: Inno Setup 7.1.0 successfully built
  `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe` from this
  complete stage, replacing the older installer. Version 8.0.0.0; unsigned.
  Size: 38,145,643 bytes. SHA-256:
  `4EFBBAF69E34460414729201812893E1F7EE3C3613180D17A29B4D00FA75C31B`.
  The 91 icon assertions passed again. Neither the installer nor the new
  published application was launched in this packaging follow-up. When using
  the portable publish instead of Setup, keep its entire directory together.

## Repeatable tests

```powershell
dotnet run --project .\Tests\ProfileVerification\ProfileVerification.Tests.csproj --no-restore
dotnet run --project .\Tests\Localization\Localization.Tests.csproj --no-restore -- --test-only
& .\Tests\ParityAudit\Test-OneDriveGameMode.ps1
```

To exercise the drop-elevation path, build the regression project and start its
DLL with an absolute dotnet host from an elevated console, passing
`--onedrive-user-probe <absolute-dotnet.exe> --require-elevated`.
The child probe only reports token/argv information; when WinGet is present it
also exports the current official source read-only. It never installs/uninstalls.

## Microsoft references

- [WinGet issue describing the user-scope elevated-uninstall restriction](https://github.com/microsoft/winget-cli/issues/6363)
- [CreateProcessWithTokenW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createprocesswithtokenw)
- [DuplicateTokenEx](https://learn.microsoft.com/en-us/windows/win32/api/securitybaseapi/nf-securitybaseapi-duplicatetokenex)
- [GetShellWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getshellwindow)
- [Why the elevated and desktop accounts must not be assumed identical](https://devblogs.microsoft.com/oldnewthing/20131118-00/?p=2643)
