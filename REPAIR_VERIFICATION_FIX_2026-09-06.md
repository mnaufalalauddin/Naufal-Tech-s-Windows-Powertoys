# Repair verification follow-up — 6 September 2026, 11:24 logs

## Findings

The user's new Windows Update run successfully preserved all three cache backups
and restarted the services. Its remaining two warnings were exact startup-value
comparisons: BITS was Automatic (2), while DoSvc was Manual (3), both Running.

The Store warning was emitted by our own immediate post-reset registration
check, not by a terminating error from Reset-AppxPackage. The generated script
had already returned from that cmdlet when it threw `Store registration could
not be verified after resetting app data.` Stage 5 subsequently registered Store
and StorePurchaseApp, and Stage 9 found Store 22607.1401.8.0. The logs do not prove
why the immediate query temporarily lacked the exact previous full name.

## Changes

- Store Stage 3 retains exact validated current-user package targeting,
  `-ErrorAction Stop`, successful exit plus exact completion marker, and the
  pending-deployment/timeout gate. The marker now means only that the reset
  **command completed**, not that registration or package health is verified.
- Stage 9 checks current-user Microsoft Store family identity, the installed
  manifest, and Windows `Package.Status.VerifyIsOK()`. It retries metadata reads
  up to six times, with one second between attempts. A newer Store version in
  the same family is valid. Missing/unhealthy/unreadable registration remains a
  failure; a real Stage 3 reset failure remains a warning even if Stage 9 passes.
- Windows Update preserves existing enabled BITS/DoSvc Automatic or Manual
  startup configurations. It repairs disabled/invalid modes to the existing
  fallback values (BITS Manual, DoSvc Automatic), without repeatedly forcing an
  enabled service back to a single mode. This is an operational repair policy,
  **not a claim that every PC has the same factory service defaults**.
- BITS' variable startup mode is explicitly documented by Microsoft. For DoSvc,
  this change preserves the user's observed enabled configuration and still
  requests/verifies Running; it does not infer that Manual is a universal default.
- cryptsvc remains Automatic and wuauserv remains Manual in this repair policy.
  Missing/unreadable, Disabled, Boot/System and unknown values are not accepted.
- Final verification now checks runtime **Running for every repair service**,
  plus UsoSvc when the repair was responsible for restarting it. Startup alone
  cannot produce a clean verification. This verifies the state at that time;
  it does not permanently force demand/trigger services to stay running later.
- Startup and runtime issues are logged independently. No system cache backup,
  ACL, service trigger, or unrelated catalog default was changed by this patch.

Primary references:

- [BITS Startup Type](https://learn.microsoft.com/en-us/windows/win32/bits/bits-startup-type)
- [PackageStatus.VerifyIsOK](https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.packagestatus.verifyisok)
- [Reset-AppxPackage](https://learn.microsoft.com/en-us/powershell/module/appx/reset-appxpackage)

## Verification and limits

- 914 regression assertions pass (160 additional checks). Positive/negative
  cases include transient and permanently missing Store registration, changed
  version, wrong publisher family, missing manifest, unhealthy status, denied
  metadata, cancellation, all relevant startup modes and non-Running states.
- Debug x64 build: 0 errors, 0 warnings.
- The generated PowerShell reset script parses successfully; it was not executed.
- 5 publish-stage assertions pass. No application or installer was launched.
- All behavioral tests use synthetic state or disposable temp-directory fixtures.
  No Store reset, service change, cache mutation in Windows, or profile Apply was
  executed on this PC. The user's next real run is still needed for end-to-end
  confirmation; no whole-program 1:1/runtime-stability claim is made.
- The earlier unexplained native 0xc0000005 dialog remains unresolved. See
  `REPAIR_FIX_2026-09-06.md` for the separate diagnostic evidence and collection tool.

## Artifacts

Native AOT and Inno Setup compilation succeeded. The expected completed publish
stage was hash-verified, and the five staging assertions passed again afterward.

- Publish stage: `artifacts/publish/win-x64-20260906-113350-937`.
- EXE: `Naufal Windows Powertoys.exe`, 18,663,936 bytes.
  SHA-256: `BEC807829B04B855EB87750998512BA75875EACCAE0F6BDD2EAB30917186D525`.
- Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-7.8.0-x64.exe`,
  36,586,703 bytes.
  SHA-256: `E44C554E61E8A021A6056AAF74E48E3E03E6C80139FE88ABAD2165B6515F5EBB`.
- FileVersion `7.8.0.0`, publisher `Naufal Tech's Softwares`; both **NotSigned**.
- The same-name Setup was replaced. Earlier publish stages are retained and
  can be used to rebuild their installer. Use Setup or the entire publish folder,
  not a lone executable stripped of its runtime files.
- Compile/hash checks do not establish real repair success or resolve the prior
  native crash. No application startup or Setup installation was tested here.
