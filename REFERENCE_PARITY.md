# V7.8 Structural Reference Audit (Not Runtime Certification)

Last updated: 2026-09-09 (structural tables below retain their historical audit date)

Current corrections, installed-binary identity and remaining parity gaps are in
`CATALOG_RECOVERY_AUDIT_2026-09-09.md`. The older structural inventory missed
Essential bulk integration for three actions despite counting their individual
buttons. That integration is now restored. The installed Program Files build
is still 7.8.0.0; the new release is 8.0.0.0 / Naufal Tech's Ltd. with silver NT
branding. Historical "matched" counts and old branding below do not certify
current runtime behavior or override the newer audit's limitations.

The current fixes and verification boundary are recorded in
`AUDIT_FIXES_2026-09-04.md` (F01–F09 code corrections, 373 regression assertions).
`PARITY_REAUDIT_2026-09-04.md` preserves the nine pre-fix findings.
`PARITY_AUDIT_2026-09-04.md` and earlier reports are historical. Counts and read-only route checks below prove
structural coverage; they do not prove every administrative mutation is 1:1.

## Reference identity

- Reference executable: `Naufal Windows Powertoys V7.8.exe`
- Reference executable SHA-256:
  `C920ADF8AB56F644DBAA8EA470F9C4B3248A9CA1DCBD15C71AC980F9C9A01BFF`
- Reference script: `V78.ps1`
- Reference script SHA-256:
  `660CD561C96FC00FBB26F15DFC94C4E03F4B4594E76C1D72A021C8E2CC18833D`
- The executable is a managed Win-PS2EXE wrapper with one manifest resource,
  `V78.ps1`. The embedded resource is byte-for-byte identical to the supplied
  script (2,437,485 bytes and the same SHA-256 above). Therefore the script is
  the authoritative behavioral source for the executable.

The reference executable and script are inspected statically. They are never
executed by this project or by the audit workflow.

## Product identity parity

| Field | Reference | Native application |
|---|---|---|
| User-facing product | Naufal Tech's Windows Powertoys | Naufal Tech's Windows Powertoys |
| File version | 7.8.0.0 | 7.8.0.0 |
| Product version | 7.8.0.0 | 7.8.0.0 |
| Description | Windows 10/11 PowerToys for IT System-Administrator | Windows 10/11 PowerToys for IT System-Administrator |
| Company | empty | Naufal Tech's Softwares (explicit user requirement) |
| Executable name | Naufal Windows Powertoys V7.8.exe | Naufal Windows Powertoys.exe (explicit user requirement) |
| Icon | reference gear icon | extracted reference gear icon |

The package manifest also uses version `7.8.0.0`. The native assembly and
file names intentionally omit `V7.8`, following the requested release name.

## Main UI route parity

The 20 reference routes are present and connected:

1. Full Repair
2. Quick Repair
3. Windows Update Fix
4. Microsoft Store Fix
5. Explorer Fix
6. Disk Info
7. System Report
8. Windows Activation
9. Office Activation
10. Disable Defender
11. Restore Defender
12. BitLocker Manager
13. Smart App Control
14. Essential Windows Tweaks
15. Gaming Tweaks
16. Games Runtime & Compatibility Check
17. GPU Driver Manager
18. Advanced Windows Tweaks & De-Bloat
19. MSI Mode Utility
20. Legacy Windows Panels

The Advanced button uses the English translation of the original
`De-Bloat Windows` localization key, matching the final reference UI.

## Exact catalog ownership

| Surface | Reference final model | Native model | Result |
|---|---:|---:|---|
| Performance atomic source definitions | 96 | statically represented by final definitions | matched |
| Performance merged definitions | 14 | 14 final merged definitions | matched |
| Performance hidden child IDs | 72 | not exposed as duplicate rows | matched |
| Performance final visible rows | 38 | 38 | matched |
| Essential base definitions | 16 | 16 backend definitions | matched |
| Essential definitions owned by Advanced | 9 | filtered from Essential and reused by Advanced | matched |
| Essential final base toggles | 5 | 5 | matched |
| Essential final base actions | 1 | 1 | matched |
| Essential utility actions | 8 | 8 | matched |
| Manual Gaming source toggles | 7 | 7 backend definitions | matched |
| Manual Gaming final visible toggles | 6 | 6 | matched |
| Gaming BCD rows | 8 | 8 | matched |
| Gaming direct actions | 15 | 15 | matched |
| Advanced final feature rows | 41 | 41 (constructor asserts the count) | matched |
| Runtime compatibility entries | 18 | 18 | matched |
| Legacy Windows Panels | 15 | 15 | matched |
| Languages | 23 | 23 | matched |
| Text scale choices | 8 | 8 plus Reset to 100% | matched |

All 38 final Performance IDs and every registry-setting signature were
compared by ID, registry path, value name, registry kind and normalized value.
The comparison returned 38 compared definitions and zero differences.

## Operation parity

| Operation | Reference stages/actions | Native stages/actions | Result |
|---|---:|---:|---|
| Full Repair | DISM, SFC | 2 | matched |
| Quick Repair | SFC | 1 | matched |
| Windows Update Fix | 6 | 6 | matched |
| Microsoft Store Fix | 9 | 9 | matched |
| Explorer Fix | 7 | 7 | matched |
| Runtime analyzer | 18 result rows | 18 result rows | matched |
| BitLocker Manager | status, suspend, resume, decrypt | 4 | matched |
| Smart App Control | status, settings | 2 | matched |
| GPU Driver Manager | source, install/update, repair/read-back | present | matched |
| MSI Mode Utility | refresh, dirty apply, registry, details/read-back | present | matched |

The native maintenance surface retains stage name, progress percentage,
elapsed time, live output, final report and copy support. Mutating operations
require confirmation and Administrator rights and perform read-back
verification where the Windows API exposes a verifiable state.

## Intentional native presentation changes

These are direct user requirements and do not remove behavior:

- every tool opens in a separate resizable WinUI 3 window;
- content follows window resizing;
- long explanatory header paragraphs are removed;
- the executable is Native AOT/self-contained and does not embed or launch the
  reference PowerShell script;
- the final executable name is `Naufal Windows Powertoys.exe`.

## Verification boundary

Build, static parity, startup, theme, scaling, localization and route tests are
safe on the developer PC. Repair, registry, BCD, service, driver, Defender,
BitLocker, de-bloat and profile mutations must be end-to-end tested on a
disposable Windows test image. The audit never changes the developer PC merely
to claim parity.

## Previously recorded Native AOT verification

- Output: `Naufal Windows Powertoys.exe`
- SHA-256:
  `DA089F5858D48ABC5FA58C2C005AD60B0E14E68612BB1CAD728442BEFD445CC0`
- Debug x64 build: 0 errors, 0 warnings.
- Native AOT publish: completed successfully, including native-code generation.
- Read-only UI Automation smoke: 20 of 20 Main UI routes opened and closed,
  23 of 23 languages enumerated, all eight scale values plus Reset enumerated,
  and the Light/Dark control switched and restored successfully.
- No mutating operation was confirmed or executed during verification.
- LIVE GAMING STATUS parity includes every read-only reference field and the
  reference one-second refresh cadence. The catalog bulk bar is anchored below
  the option viewport as in the reference application.

The reference resource-lock task queue is now represented with the same eight
managed Main UI routes and exact resource sets, plus the native mutation
surfaces. Xbox package, low-risk de-bloat, Performance Lab, and manual Gaming
original-state data now have guarded cross-version restore compatibility.
Concurrent runtime timing and the remaining historical state schemas remain
open certification items. See the full audit before using “1:1 verified.”

The 2026-09-04 profile audit corrected MMCSS-only false VERIFIED detection with
a full 23-check evaluator and per-adapter RSC semantics. Its 73 regression checks
and read-only probe evidence are recorded separately; the earlier 20-route UI
smoke test above was not repeated as part of that regression suite.
The latest Native AOT/Setup artifacts and pending current-binary UI startup test
are recorded in `PARITY_AUDIT_2026-09-04.md`; the hashes in the historical section
above identify its earlier milestone, not the latest release output.
