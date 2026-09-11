# Behavioral audit continuation — 2026-09-04

Historical profile-focused milestone. The later `PARITY_REAUDIT_2026-09-04.md`
contains the current cross-menu findings, publisher change, and artifact hashes.

## Scope and verdict

Continuation of `PARITY_AUDIT_2026-09-03.md`, using the same supplied executable
and PowerShell source as the behavioral reference. Neither reference artifact
was executed. Overall runtime parity is still **not certified 1:1**.

This pass concentrates on preference persistence and Performance Profile. It
found a real false-positive: the native Main UI used MMCSS alone to display
VERIFIED and disable Apply even when the remaining profile settings differed.

## Implemented corrections

- Main UI now certifies only a complete 23/23 profile: MMCSS profile and nine
  individual values, active target plan, CPU AC/DC policy, core parking,
  Dynamic Tick, HPET, per-interface TCPNoDelay/TcpAckFrequency, global TCP,
  network throttling, two QoS settings, and per-adapter IPv4/IPv6 RSC.
- A partial match remains Custom with a matched/total count and does not disable
  Apply as CURRENT PROFILE. Missing data and read errors cannot certify values.
- Apply and Verify resolve the same installed target plan. Historical
  KnownPowerGuids are accepted only if they still identify a valid installed
  plan of the correct profile family.
- Full verification runs inside the extended transaction rollback boundary.
  Failure attempts restoration of CPU, BCD, registry and each adapter's original
  IPv4/IPv6 RSC state, followed by the outer MMCSS/active-plan restoration.
  Outer rollback messages no longer imply that every extended rollback succeeded.
- Replaced global netsh RSC mutation with native per-adapter WMI Enable/Disable
  methods, following the reference networking scope. Global RSC is not modified.
  Methods validate provider return values and read back target adapter states.
- LIVE RSC and the full verifier share one native per-adapter read, yielding
  ON/OFF/MIXED/UNKNOWN. BCD access failure displays UNKNOWN/UNAVAILABLE, not DEFAULT.
- Restart guidance is based on whether the requested BCD options changed.
- Theme/language/font preferences now use the reference V78 LocalAppData folder.
  Only missing preference files are copied from the prior native folder, then
  the reference V77 folder. Existing canonical preferences are never overwritten.
- Added portable regression tests under `Tests/ProfileVerification`; they are
  excluded from WinUI build/publish items and require no extra test framework.

## Deliberate error-handling differences

The reference often swallows provider failures. The native verifier instead
distinguishes a failed RSC query from a successful empty enumeration, and BCD
access denial from a genuinely absent override. Failed reads prevent VERIFIED.
A successful empty RSC enumeration remains non-applicable, as in the reference.
This is an explicit conservative difference, not exact error-path equivalence.

## Test evidence

- Debug x64 build: 0 errors, 0 warnings before release publication.
- 73 synthetic regression assertions passed for all three profiles and the live
  RSC formatter. No mutations were executed by the harness.
- Read-only local probe enumerated two adapters with IPv4/IPv6 enabled, matching
  a separate CIM read. WMI Enable/Disable metadata and input construction were
  validated without executing either method.
- The unelevated probe found Optimized Gaming MMCSS but a full result of 21/23:
  only Dynamic Tick and HPET were unreadable due to BCD access restrictions.
  This does not establish that those BCD settings are incorrect.
- The user-reported `ProfileVerification.Tests.exe` crash came from the first
  diagnostic probe's unhandled WMI access denial. The probe and top-level harness
  now catch errors. Subsequent completed probes did not crash.

## Remaining work and limits

- Real profile Apply, partial adapter failure, mixed-state rollback, unplugged
  adapters, and restore verification still require disposable administrative VM
  tests. The production mutation methods were not executed on this PC.
- Profile transaction history/export and exact original verification-dialog
  presentation still need comparison; the 23-check engine is not a claim that
  every profile-related user interaction is identical.
- Provider-specific behavior on Windows 10, Windows 11, non-English BCD output,
  unavailable features and alternate GPU/NIC hardware remains unverified.
- Full repair, driver, security and de-bloat mutation matrices from the preceding
  audit remain open. No installed app, service, driver, registry, BCD, firewall,
  Defender, BitLocker or reboot setting was changed during these tests.

## Native API basis

- Windows SDK `WbemCli.h` supplies the explicit COM vtable signatures, avoiding
  reflection-based runtime COM marshalling in Native AOT.
- [RSC Enable method](https://learn.microsoft.com/en-us/windows/win32/fwp/wmi/netadaptercimprov/enable-msft-netadapterrscsettingdata)
  and [RSC Disable method](https://learn.microsoft.com/en-us/windows/win32/fwp/wmi/netadaptercimprov/disable-msft-netadapterrscsettingdata)
  define the separate IPv4 and IPv6 inputs.

## Release artifact evidence

- Native AOT x64 publication completed, including native-code generation.
  Inno Setup compilation completed successfully. No AOT warning was reported.
- Immutable staged app folder: `artifacts/publish/win-x64-20260904-075647`;
  160 files. Keep the complete folder when using the app without Setup.
- App: `Naufal Windows Powertoys.exe`, 17,918,976 bytes.
  SHA-256: `167E1820A821631E2874AB73E2B3798716FE78A274373512E9600581B191E36A`.
- Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-7.8.0-x64.exe`,
  36,369,203 bytes. SHA-256:
  `A6CE43BFA8D9432381882C20EB7425F5B72BB1F089744CEEB85FA032182F69A0`.
- Both executables are unsigned. Setup was compiled, not installed/tested.
  The pipeline replaced the previous same-name generated Setup output; previous
  source/staged app folders were not removed and can be used for rebuilding.
- Startup inspection of this specific AOT binary was attempted using the
  computer-use skill. The tool returned `Computer Use app approval timed out`.
  No approval bypass or shell fallback was used. Current-binary UI startup is
  therefore **not verified**; prior route/theme/scale evidence is historical only.
