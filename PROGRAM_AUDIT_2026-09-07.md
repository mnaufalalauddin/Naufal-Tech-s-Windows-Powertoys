# Whole-program audit and fixes — 2026-09-07

## Result and limits

Six reproducible source-level defects were corrected. The 20 main-menu routes
remain connected. Functional regression checks, localization checks and publish
staging checks pass. This is **not** a certification of full 1:1 behavior with
the original PowerShell/PS2EXE application, nor a native UI or hardware test.

The audit used source inspection, reference parsing/hashes, pure/synthetic tests,
and compilation. It did not apply/restore tweaks, repair Windows, change services,
install drivers/runtimes, change security settings, reboot, launch the application,
or run the Setup installer on this PC. Existing safety checks were retained.

## Corrected findings

| Finding | Change | Verification |
| --- | --- | --- |
| P1: Finished repair operations kept their task/resource lease until the user closed the result window. Runtime/GPU/wizard completion also waited for result closure. | Removed 13 result-window waits from MainWindow completion/error paths. Backend completion now releases task/gate state and refreshes results while the modeless result window may remain open. Busy-window guards still prevent closing active work. | Source wiring checks plus a synthetic scheduler test: the next conflicting task is admitted after completion while the old result window is still open. |
| P1: A Quick/Full Repair SFC timeout or synthetic command failure could become a successful result with a warning. | Shared classification makes timeout and negative process-error results failures. DISM cannot pass when its timeout flag is set. Positive SFC nonzero exits retain the reference application's warning/CBS-log-review behavior. | Exit-code/timeout truth matrix and production wiring check. No real SFC/DISM operation executed. |
| P1: GPU verification could accept the previous healthy driver even when it did not match the selected package. | Compare NVIDIA/Intel active versions with the resolved target. An explicit mismatch fails unless installer status requires restart, in which case target verification remains pending with a warning. Non-comparable metadata, including AMD Auto-Detect, is explicitly unverified with a warning; it does not assert that the target/latest version is active. | Synthetic NVIDIA/Intel matches, mismatches, invalid versions and ambiguous AMD metadata. Actual vendor installers/endpoints/hardware remain untested. |
| P2: Repeated Analyze/reopen could accumulate blocked read workers after a timeout. | Added per-service/per-item shared read probes and retained the three catalog service identities across reopening. Retries reuse an in-flight read; completed reads are refreshed, not cached indefinitely. This helper is read-only and is never used to retry a mutation. | Blocked fake provider, three retries/one worker, independent healthy row, recovery, fresh read, provider error and retry. |
| P2: Overall maintenance progress could finish green despite FAILED/NOT VERIFIED stages, or a late callback could revive/regress an already finished stage. | Completion checks backend success together with terminal stage evidence; unknown/unreported successful stages become NOT VERIFIED. Failed/unverified bars/status are red. Late RUNNING callbacks cannot revive terminal rows; a delayed terminal result can settle an older row without moving the current title backward. Invalid stage indexes/counts and non-finite percentages are guarded. | Completion truth matrix, late-callback/index/count guards, native project compilation. All-skipped workflows remain valid no-ops only when the backend explicitly reports success. |
| P2: A zero/sub-millisecond command timeout could start a process whose timer had already expired. | Reject these durations before Process.Start. Explicit infinite timeout remains supported for the Store reset caller protected by its outer operation gate. Existing cancellation and invalid-timeout checks remain. | Invalid/missing executable sentinel proves zero, positive sub-millisecond and negative sub-millisecond requests fail before launch. |

The new policy/helper files are `RepairCommandOutcome.cs`,
`GpuDriverVersionVerification.cs`, `MaintenanceCompletion.cs` and
`CatalogStateReader.cs`. Integration changes are in `MainWindow.xaml.cs`,
`WindowsRepairService.cs`, `GpuDriverService.cs`, `MaintenanceProgressWindow.cs`,
`BoundedReadProbe.cs` and `NativeCommandRunner.cs`. Regression additions are in
`Tests/ProfileVerification/WholeProgramAuditTests.cs` and `GeneralAuditTests.cs`.

The NVIDIA conversion follows its documented last-five-digit convention
([NVIDIA driver FAQ](https://www.nvidia.com/en-gb/drivers/drivers-faq/)). Intel's
four-part driver versions are compared directly where both values are available
([Intel version numbering](https://www.intel.com/content/www/us/en/support/articles/000005654/graphics.html)).
These rules do not establish that an installer actually ran successfully; they
strengthen read-back verification after the existing signature/exit/device checks.

## Coverage ledger

All 20 named XAML main-menu routes and their handlers were checked by
`Tests/ParityAudit/Inspect-StaticParity.ps1`. The following is the scope of the
code/test review, not a claim that every backend branch was executed.

| Area | Evidence reviewed / retained | Remaining validation |
| --- | --- | --- |
| Full/Quick Repair, Explorer Fix, Windows Update Fix, Microsoft Store Fix | Five menu routes; shared runner/stage reporting/task lifetime; repair regression policies and previous Store/update verification fixes | Real Windows repair, cache/service recovery, Store deployment and failure injection in a disposable VM |
| Essential, Gaming, Advanced De-Bloat | Select/Apply/Restore planning, shared progress, read retry behavior, service/snapshot restore guards, action gates | Registry/BCD/app-package/service side effects and original-script parity for every option |
| GPU Driver Manager | Resolver/install/signature/read-back paths; target-version correction | NVIDIA/Intel/AMD/OEM/hybrid hardware, network/download endpoint changes, reboot-required installs |
| Games Runtime Compatibility | Package/prerequisite workflows, task lifetime and verification paths | Real clean-machine install/repair, missing prerequisites, reboot-required outcomes |
| MSI Mode Utility | Configuration/read-back rules and INF/IRQ inspection paths; retained regression checks | Real device/driver behavior and post-reboot verification |
| Defender controls, BitLocker, Smart App Control | Routes, administrative/confirmation checks, tamper-protection and read-back guards | Supported Windows policies/builds and actual security transitions; not exercised here |
| Disk Info, System Report | Routes, report/read-only paths and display integration | Real native UI, device variants, access-denied/unavailable data on other PCs |
| Windows/Office Activation | Information-only slmgr/ospp paths; no activation bypass added | Office installation discovery remains limited; uncommon/non-system-drive installations need additional fixtures |
| Legacy Windows Panels | Panel mapping/launch paths and route | Panel availability across Windows editions/builds |
| Profiles/live status/first-run wizard | Existing profile/RSC/power-policy tests, task gating, completion path, existing wizard preferences | Hardware-dependent policies and actual wizard/skip/reopen UI behavior |
| Languages/theme/scaling/windows | 23-language regression suite, previous language-switch lifecycle fix, shared result-window ownership | Native 25–200% scaling, RTL/clipping, all popups/open windows and native-speaker review |
| Publish/Setup/publisher | Project and installer metadata, hash-verified staging tests, Debug/AOT/Setup builds | Fresh-PC install/uninstall/upgrade and code-signing certificate |

## Reference identity and structural results

- Reference script: `C:\Users\Naufal\Desktop\Naufal ChatGPT\V78.ps1`.
- Script and project-copy SHA-256:
  `660CD561C96FC00FBB26F15DFC94C4E03F4B4594E76C1D72A021C8E2CC18833D`.
- Reference EXE: `C:\Users\Naufal\Desktop\Naufal ChatGPT\Naufal Windows Powertoys V7.8.exe`.
- Reference EXE SHA-256:
  `C920ADF8AB56F644DBAA8EA470F9C4B3248A9CA1DCBD15C71AC980F9C9A01BFF`.
- Script parse errors: 0. Function definitions: 649; repeated function names: 3.
- Main-menu routes connected/defined: 20/20.
- Merged localization resource tables: 23, with 573 keys each.
- Product: `Naufal Tech's Windows Powertoys`; executable: `Naufal Windows Powertoys.exe`.
- Company/publisher display metadata: `Naufal Tech's Softwares`.

Resource counts and connected handlers are structural evidence only. The original
EXE was hashed, not launched or exhaustively compared interactively.

## Tests and build

- Functional regression suite: **1,372 assertions pass** (baseline 914).
- Localization suite: **92,006 assertions pass across 23 languages**. Includes
  529 language pairs through the production adapter with XAML test doubles;
  these are not real WinUI click/render tests.
- Publish staging: **5 assertions pass**, including incomplete/tampered-stage rejection.
- Final Debug build: **0 warnings, 0 errors**.
- Native AOT publish and Inno Setup compile: **successful**. Completed-stage EXE
  hash revalidated; neither executable was launched.

Verified output:

- Stage: `artifacts/publish/win-x64-20260907-202745-458`.
- EXE: `Naufal Windows Powertoys.exe`, 19,147,264 bytes.
- EXE SHA-256: `6E468C3D3400BA1CAF1343349043C59480FC5C54320101AE3B43EB32D8AD36FF`.
- Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-7.8.0-x64.exe`,
  36,711,752 bytes.
- Setup SHA-256: `2101D7288580A9267D8AACB5D8881407B4DCD0688C7C733BC4BA074BD02F3E21`.
- Both: file version 7.8.0.0, Company `Naufal Tech's Softwares`, Authenticode
  `NotSigned`. The prior same-name Setup was replaced by this build; older
  completed publish stages remain available for rebuilding an older installer.

Commands used with the existing isolated CLI/NuGet environment:

```powershell
dotnet run --project Tests\ProfileVerification\ProfileVerification.Tests.csproj -c Debug --no-restore
dotnet run --project Tests\Localization\Localization.Tests.csproj --no-restore -- --test-only
.\Tests\ParityAudit\Test-PublishStage.ps1
.\Tests\ParityAudit\Inspect-StaticParity.ps1
dotnet build "Naufal Tech's Windows Powertoys.csproj" -c Debug -p:Platform=x64 --no-restore
.\build-installer.ps1 -NoRestore
```

## Still open — do not mark these fixed

1. The previously reported native `0xc0000005` crash has no reproduced trigger or
   conclusive stack trace from this run. Compilation and synthetic tests do not
   resolve or disprove it. No native application launch was attempted this turn.
2. Full original-program behavior parity still requires per-option before/after
   comparisons on disposable systems, including administrator-denied paths,
   reboot/relaunch, service Running states, install/restore failures and rollback.
3. Long native descriptions, confirmations/results and several MSI/GPU/runtime/
   BitLocker/Process Manager strings still use English fallback. See
   `LOCALIZATION_AUDIT_2026-09-07.md` and `LANGUAGE_SWITCH_FIX_2026-09-07.md`.
   Equal resource counts do not imply the whole UI is fully translated.
4. A bounded read stops the caller waiting; it cannot forcibly interrupt every
   native Windows inventory call. Reuse prevents repeated blocked workers but
   does not prove that the original Widgets/other mutation hang is resolved.
5. Driver target checks cannot derive a comparable display INF version from AMD
   Auto-Detect package metadata. The application now discloses this limitation.
6. Publisher metadata is not an Authenticode signature. Release artifacts remain
   unsigned until a trusted certificate/signing workflow is supplied.
