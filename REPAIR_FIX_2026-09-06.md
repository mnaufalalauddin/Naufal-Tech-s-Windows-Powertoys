# Store / Windows Update repair follow-up — 6 September 2026

Historical first pass. The user's subsequent 11:24 logs and the corrections to
Store post-reset verification / BITS and DoSvc startup validation are recorded
in `REPAIR_VERIFICATION_FIX_2026-09-06.md`. Its artifact supersedes this pass.

## Summary and limits

Two repair paths were corrected in response to the supplied Store log and
Windows Update screenshot. The separate `Exception Processing Message
0xc0000005 - Unexpected parameters` screenshot remains **unresolved**: the last
UI action was not recalled, no matching Application crash event was found in
the bounded query, and the application's managed `crash.log` does not exist.
Neither this code review nor a clean build proves that native crash is fixed.

No Windows repair, package reset, service stop/start, policy/ACL change, profile
Apply/Restore, GUI startup, or Setup installation was executed during this pass.
Real filesystem rename tests used only disposable, uniquely named temp fixtures.

## Microsoft Store Fix

The old Stage 3 called Windows App SDK `PackageDeploymentManager.ResetPackageAsync`
unconditionally. That API can be present while the reset feature is unsupported;
Microsoft's implementation throws E_NOTIMPL for that case. See the
[upstream implementation](https://github.com/microsoft/WindowsAppSDK/blob/main/dev/PackageManager/API/M.W.M.D.PackageDeploymentManager.cpp).
The original project reference uses the Windows
[Reset-AppxPackage cmdlet](https://learn.microsoft.com/en-us/powershell/module/appx/reset-appxpackage).

Changes:

- Stage 3 now invokes that OS cmdlet through the installed Windows PowerShell
  executable. The UI remains native; this specific backend stage deliberately
  uses the supported system cmdlet instead of the unavailable SDK operation.
- Fixed current-user Store scope, a strict main-package full-name allow-list,
  no wildcard/all-user reset/uninstall, no execution-policy bypass. Parameters
  are passed through a generated encoded command solely for quoting fidelity.
- The process requires successful exit and an exact completion marker emitted
  only after Reset-AppxPackage returns and current-user registration is re-read.
  It does not claim that cache cleanup or re-registration alone reset app data.
- Unsupported/failed terminal resets remain visible stage warnings with the
  actual error. Warning text is written immediately into the repair log.
- A two-minute timeout plus five-second grace uses the shared deployment gate.
  The PowerShell client is not killed and called "cancelled": the gate remains
  held until it actually exits. Timeout, cancellation or an already-pending
  deployment abort this repair before cache cleanup or registration continues.
  A stuck external reset may therefore remain running after the UI reports the
  timeout; the diagnostic explicitly says completion is unconfirmed.
- Reset confirmation now explicitly mentions current-user app data/preferences
  and possible sign-in reset. Purchased apps are not uninstalled.
- Added a backend Administrator check; partial cache cleanup no longer logs the
  unconditional `Cleared cache` success wording.

Read-only evidence on this PC: Reset-AppxPackage exists in module Appx; current
package is `Microsoft.WindowsStore_22607.1401.8.0_x64__8wekyb3d8bbwe` (Status 0).
The generated reset script was parsed but **never executed**. Cmdlet availability
and package presence are not proof of a successful reset on this PC.

## Windows Update Fix

The screenshot shows `.old` backup deletion followed by an access-denied rename
of the SoftwareDistribution parent. Current read-only ACL inspection found
SYSTEM ownership and Administrator FullControl, with no root reparse point.
That does not prove the precise historical blocker; an open handle, service
restart or external security restriction remains possible.

Changes:

- Resets the known `SoftwareDistribution\DataStore`,
  `SoftwareDistribution\Download`, and `System32\catroot2` cache directories,
  following Microsoft's documented
  [cache component paths](https://learn.microsoft.com/en-us/troubleshoot/windows-client/installing-updates-features-roles/additional-resources-for-windows-update).
  The SoftwareDistribution parent is no longer renamed. This intentionally
  improves the reference script's failing parent-rename behavior.
- Each move preserves its cache in a unique sibling `.bak-wpt-<time>-<id>`.
  Existing `.old`, `.bak`, and prior backups are never deleted or overwritten.
  Backups retain disk usage; no automatic recursive cleanup was introduced.
- Only those three exact relative paths are allowed. The directory chain is
  checked for reparse points/non-directories; access errors are not silently
  converted to "not present". No ownership or ACL reset is performed.
- Update Orchestrator (UsoSvc) is paused along with the original update-related
  services. Its startup configuration is not changed; if it was active before
  the pause, restart/recovery requests it running again.
- Service stop must be confirmed. A second full state read detects a service
  restarting while another is stopping, and blocks the cache mutation.
- Before each move/retry, services are checked again. Access/sharing violations
  get at most three rename attempts, with 500 ms between retries. Stop-state
  failures abort before rename. A persistent lock is a genuine error with an
  actionable message, not a skipped stage displayed as success.
- Restart and failure recovery use runtime read-back rather than trusting
  `sc.exe` exit 0/1056 alone. Recovery runs even when the caller token is cancelled;
  its individual waits remain bounded and failures appear in warnings.
- Native service-state reads are preferred over localized `sc.exe` output;
  service polling uses monotonic elapsed time. Cache backup existence is verified
  after rename and in the final verification stage.

This repairs known code defects and avoids the failing parent rename. It does
not certify that externally locked/protected Windows caches can be reset on
every PC. No disruptive handle killing, antivirus disabling, ownership takeover,
or permission reset was used to force the operation through.

## Verification

- Debug x64: 0 errors, 0 warnings.
- 754 regression assertions pass, including 37 new Store/cache safety checks.
  Cases cover current-user scope/input validation, missing success marker,
  fatal pending/reset timeout, retries, preserved backups, exact path allow-list,
  reparse attributes, cancellation, failed service gate and missing move result.
- Generated Store script: parser reports no syntax errors; script not executed.
- 5 publish-stage assertions pass.
- Diagnostics were collected read-only in
  `artifacts/diagnostics/crash-diagnostics-20260906-111724-576.json`.
  No matching process/event or managed crash log was found in that collection.
  EventLog service was running. The recorded build hash is the earlier build
  present at collection time, not proof of which executable showed the dialog.
- Existing Balanced/RSC fixes and the remaining whole-program audit limitations
  in `PROFILE_APPLY_FIX_2026-09-06.md` and `PROGRAM_AUDIT_2026-09-05.md` remain.

## If the native error appears again

Run `Collect-CrashDiagnostics.ps1` from the project root soon after the error.
It writes a JSON report in `artifacts/diagnostics`; it does not change Windows
settings. Record the exact EXE path and last button/action if possible. Review
the file before sharing because it contains local paths and event text. Native
faults may still need a debugger/crash dump if Windows produces no event.

## Build artifacts

Native AOT and Inno Setup compilation succeeded. The completed publish stage was
hash-verified, and the five staging assertions were re-run after completion.

- Publish stage: `artifacts/publish/win-x64-20260906-111825-307`.
- EXE: `Naufal Windows Powertoys.exe`, 18,641,408 bytes.
  SHA-256: `E274067FAD88944A524A28148098588DE84E1B9D8DDBFE4E83EA6BE99E534AA1`.
- Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-7.8.0-x64.exe`,
  36,579,301 bytes.
  SHA-256: `098EECF854EC23DACFE5D41499EE69D027D3C8E36A9DE0516DA4F319DF1B26B7`.
- Both FileVersion `7.8.0.0`, publisher `Naufal Tech's Softwares`, **NotSigned**.
- The same-name Setup was replaced; historical publish folders are retained.
  Use Setup or the entire publish folder with its runtime files, not a lone EXE.
- No release runtime, repair execution, or installation test is claimed.
  **The unexplained native error 0xc0000005 still needs reproduction/diagnosis.**
