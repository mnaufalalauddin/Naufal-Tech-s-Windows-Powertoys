# Advanced Windows Tweaks & De-Bloat audit — 5 September 2026

## Outcome

The Advanced catalog and its restore paths were re-audited against the supplied
`V78.ps1` source. The SysMain restore defect is corrected: a restore of
**SysMain / Application Prefetching** now targets the documented Windows startup
type **Automatic (`Start=2`)**, issues a real service start request, waits up to
15 seconds for **Running**, preserves the delayed-start metadata,
restores/removes the two Prefetch values as appropriate, and retains the backup
unless all read-back checks pass.

This pass is source/static and synthetic-test evidence. It did not execute any
registry, service, AppX, DISM, scheduled-task, Xbox, Windows AI, or other Windows
mutation. A disposable elevated Windows VM is still required to certify all
41 rows end-to-end.

## Reference parity checked

- Final native catalog remains guarded at **41 visible rows**.
- Reference and native service-group catalogs: **22 vs 22**, with zero ID,
  display-name, or service-membership differences.
- Reference and native documented service defaults: **33 vs 33**, with zero
  name/value differences.
- SysMain is mapped to `2` (Automatic) in both default maps.
- Known Windows services use a documented restore target. Services owned by
  NVIDIA, AMD, Intel, or other vendors without a reliable default continue to
  require an exact saved snapshot rather than receiving a guessed startup type.

## Findings corrected

| ID | Priority | Finding | Correction |
|---|---:|---|---|
| A01 | P1 | Service-group Restore always replayed the native snapshot. A snapshot captured while SysMain was Manual or already Disabled could therefore leave it non-Automatic. | Restore planning now gives the reference/default map priority for known Windows services. SysMain restores to Automatic and Running. |
| A02 | P1 | A missing native service snapshot prevented Restore even when the reference behavior had a documented Windows fallback. | Restore is now smart: documented default for known Windows services; exact snapshot for unknown vendor services; fail-closed when an unknown service is Disabled and no snapshot exists. |
| A03 | P1 | Startup configuration trusted `sc.exe` too narrowly and did not provide a durable registry fallback/read-back diagnostic. | Every service startup transition now reads back `Start`; if needed it applies the same registry fallback used by the reference, then verifies again with explicit command diagnostics. |
| A04 | P1 | Snapshot validation accepted startup values `0` and `1`, which are boot/system driver modes and cannot be safely restored through the service engine. | Only service modes Automatic (`2`), Manual (`3`), and Disabled (`4`) are accepted. |
| A05 | P1 | Runtime verification rules were not aligned with the reference. In particular, disabling a busy service could be reported as failed even though `Start=4` is durable and the process stops at reboot. | Exact runtime snapshots are verified. Documented Automatic services receive a real start request; continuous services such as SysMain must verify Running, while trigger-start services may self-stop without a false failure. Documented Manual stays demand-driven; documented Disabled is verified by startup type and may finish stopping at reboot. |
| A06 | P1 | Registry Lab and low-risk composite restores could accept a partial multi-value snapshot and later delete the remaining backup. | Multi-value snapshots are preflighted as a complete set. Partial sets abort before mutation and remain available for diagnosis/recovery. |
| A07 | P1 | Several Advanced backends removed backups before final state read-back. | Registry Lab, low-risk de-bloat, Network/Storage, Essential default restore, Windows AI, Xbox, and service-group cleanup now retain backups until their owning operation verifies success. |
| A08 | P2 | Service state rows exposed numeric values such as `SysMain=2`, making the result difficult to audit from the UI. | Rows now report readable startup/runtime text such as `SysMain=Automatic / Running`. |

## Restore semantics after this audit

- **Restore selected**, **Restore safe**, **Restore advanced**, or switching a
  row OFF:
  - known Windows service → documented Windows default;
  - unknown vendor service → exact saved startup/delayed/runtime state;
  - registry companion value → exact complete snapshot, otherwise remove the
    utility override so Windows regains control.
- **Restore all defaults** intentionally discards the relevant saved snapshot
  only after Windows-default read-back succeeds.
- Apply and Restore remain non-transactional across multiple selected rows, as
  in the reference utility: one row can succeed while another reports a failure.
  Each row preserves its own failed restore backup.

## Verification evidence

- Debug x64 build: **0 errors, 0 warnings**.
- **519 synthetic regression assertions passed**; no Windows mutations.
- **5 publish-stage assertions passed**; no application/installer execution.
- Static reference inspection: **20/20 Main UI routes**, reference PS1 parses
  with zero errors, and all **23 languages contain 396 entries**.
- Reference PS1 SHA-256:
  `660CD561C96FC00FBB26F15DFC94C4E03F4B4594E76C1D72A021C8E2CC18833D`.
- Reference EXE SHA-256:
  `C920ADF8AB56F644DBAA8EA470F9C4B3248A9CA1DCBD15C71AC980F9C9A01BFF`.

## Final build artifacts

- Native AOT application:
  `artifacts/publish/win-x64-20260905-135750-265/Naufal Windows Powertoys.exe`
  - SHA-256: `95A2993293917EFD9E892B19394995D9E2C92764CB9D56FC76F14276BC9FCC38`
  - Company: `Naufal Tech's Softwares`
  - File version: `7.8.0.0`
- Setup installer:
  `artifacts/installer/Naufal-Windows-Powertoys-Setup-7.8.0-x64.exe`
  - SHA-256: `FAF70C7571E0797C1F8162921E0CA7B941B54BBEA57C7673E6278FD76675849F`
- Both binaries are currently unsigned. The build generated them but did not
  launch either artifact.

## Required live confirmation

On an elevated disposable Windows test machine:

1. Open Advanced Windows Tweaks & De-Bloat and Analyze/reload.
2. Select SysMain / Application Prefetching and Apply selected.
3. Confirm `Start=4`, Prefetch values `0`, and the reboot note when applicable.
4. Select the same row and choose Restore selected.
5. Confirm the row reports `SysMain=Automatic / Running` and Windows Services
   shows Startup type **Automatic**.
6. Reboot, analyze again, and repeat representative Safe, Advanced, composite,
   absent-service, vendor-service, Xbox, Windows AI, Storage Sense, Reserved
   Storage, and failure/cancellation cases before claiming full 1:1 behavior.
