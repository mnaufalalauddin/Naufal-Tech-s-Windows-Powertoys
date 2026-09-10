# Button and catalog interaction audit — 2026-09-10

## Result and scope

Implemented the confirmed button, interaction and progress findings below, then
rebuilt Native AOT and Setup 8.0.0. Source routing for all 20 Main UI actions and
the shared catalog implementation was checked alongside the existing service
regression suite. This is not certification of complete real-device 1:1 behavior.
No Windows repair, tweak, profile Apply/Restore, driver operation or installation
was executed during this audit.

The supplied `Naufal-Windows-Powertoys-Setup-7.8.0-x64.exe` SHA-256 is
`1C636B8C4FFEA4DD11EC2CE4D45CA71A7705D7D2D5E3268E9160132810F515E1`,
matching the preserved project 7.8 installer. Main UI button labels/order/routes
were retained against that reference. The historical installed-folder comparison
is in `CATALOG_RECOVERY_AUDIT_2026-09-09.md`; at this audit's read-only check,
`C:\Program Files\Naufal Windows Powertoys\Naufal Windows Powertoys.exe`
was absent. This audit did not remove or modify that installation.

## Confirmed findings and corrections

1. **Inconsistent button presentation.** Added an application-wide flat,
   bordered `ReferenceButtonStyle`; Main UI tool buttons inherit the same base.
   Shared footer/catalog/runtime buttons use square corners. Existing primary
   and risk colors, labels, ordering and bottom catalog toolbar remain intact.
   Explicit 32px header minimums avoid unintended growth from the 34px base.
   Light-color baselines remain compatible with the existing theme mapper.
   Individual action columns now size automatically rather than fixing the
   translated button area at 250px.
2. **Individual actions were not protected throughout confirmation/queueing.**
   A per-catalog operation gate now begins before confirmation and remains held
   through completion. Analyze, bulk Apply/Restore and individual controls share
   the same busy state; initial analysis also disables action buttons. Closing
   remains possible after failed analysis, but not during an active operation.
   Exceptions from confirmation/individual event handlers are reported and the
   gate is released in `finally`.
3. **Individual Restore lacked the same progress context as Apply.** Both now
   dispatch through `CatalogActionRunner`, carrying the item's scoped progress
   reporter across synchronous work and asynchronous continuations. The runner
   preserves backend failure/unavailable distinctions and propagates exceptions;
   it does not convert a failure into success.
4. **Bulk backend work could still block the XAML dispatcher.** After preflight,
   `CatalogOperationRunner` now dispatches backend execution to a worker for
   Apply, exact Restore and explicit default Restore. This covers synchronous
   registry/snapshot work before an implementation's first asynchronous wait.
   Existing admission, snapshot, verification and rollback rules are preserved.
5. **Overall progress appeared idle during the first long operation.** Overall
   activity becomes indeterminate while the first item is running/verifying;
   after items settle, the bar represents the processed-item fraction. An
   individual Windows command's percentage is not misrepresented as the
   completion percentage of an entire multi-step tweak. Errors turn the overall
   bar red as soon as reported and survive theme refresh. Final completion stops
   animation; unavailable entries retain their distinct neutral classification.
6. **Live overall/export detail could lag item progress.** Begin, verification,
   backend reports and completion now update the overall summary immediately,
   so copied/saved reports include current activity rather than an old message.

Primary implementation: `App.xaml`, `MainWindow.xaml`, `MainWindow.xaml.cs`,
`ToolWindow.cs`, `CatalogActionRunner.cs`, `CatalogSelectionPlan.cs`,
`CatalogProgressState.cs`, `CatalogProgressWindow.cs`.
Regression additions: `Tests/ProfileVerification/CatalogInteractionTests.cs`
and `Tests/ParityAudit/Test-CatalogInteraction.ps1`.

## Verification

| Check | Result |
| --- | --- |
| Debug build, final source | 0 errors, 0 warnings |
| Functional regression suite | 2,090 assertions passed; no Windows settings changed |
| Localization regression suite | 93,224 assertions across 23 languages passed |
| Main UI labels/order/routes | 20/20 statically checked |
| Button/routing/progress static assertions | 80 passed |
| Report export static assertions | 10 passed |
| Publish-stage integrity regression assertions | 10 passed |
| Final EXE/assets/installer icon assertions | 101 passed |
| Final stage file inventory and hashes | All 168 payload files validated |
| Native AOT and Inno Setup | Final timestamped stage and Setup produced |

New behavioral tests include individual Apply/Restore running off a dispatcher,
progress scope across awaits, concurrent reporter isolation, exception cleanup,
unsupported Restore rejection, and bulk execution under a simulated dispatcher
for all three operation directions. Progress model tests exercise working,
verified, failed and unavailable states.

An incremental verification initially encountered sandbox write denial on
`obj/.../input.json`. After renewing scoped project write permission, that same
Debug build completed successfully. This was not a source compilation failure.

## Native UI observations and remaining limits

The earlier candidate `win-x64-20260910-214456-801` was opened read-only. Its
Main UI rendered with the NT icon, the 20 expected buttons, flat/risk styling,
and live metrics; no crash dialog was observed in that check. The Computer Use
skill reported that the app had higher Windows integrity than its helper. A
catalog click did not produce a verified navigation result, so further clicks
were stopped rather than bypassing that boundary. The candidate was left open.
The final worker-dispatch correction was built afterwards; the final EXE was
not separately launched. Installer execution, taskbar-pin appearance and actual
Apply/Restore behavior remain unverified on this PC.

Not all backends emit fine-grained substep percentages. Missing telemetry uses
indeterminate activity, not invented percentages. Structural localization tests
do not certify translation quality or every layout at eight scales. Previously
documented missing no-backup defaults for some Essential options, complete
per-setting expected-value tables, real-device GPU/MSI/repair behavior and
historical native crash reproduction remain open. The 20-route check does not
prove every feature's runtime equivalence. No feature-loss claim is attributed
to a model change without source-history evidence.

## Final artifacts

- Application: `artifacts/publish/win-x64-20260910-214932-569/Naufal Windows Powertoys.exe`
- Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`
- Both: Windows file version **8.0.0.0**, company **Naufal Tech's Ltd.**,
  silver NT branding; **NotSigned**.
- EXE: 19,595,264 bytes; SHA-256
  `8EAB32AB7684CBA00823C65F8F681BAF3E71530BE649BC78E5F447B94D8DAABC`.
- Setup: 37,999,129 bytes; SHA-256
  `51B8240FE5221061AFC1A3F80D436CEE47BBEEEFDB7E3500406A7471DE99EAE3`.

The fixed-name 8.0.0 installer output was rebuilt. Timestamped publish stages and
the old 7.8 installer remain available. Distribute the Setup or the complete
validated publish directory, not the application EXE on its own.
