# Program re-audit — 2026-09-09

## Outcome

Eight groups of defects were corrected in the shared operation infrastructure,
startup wizard, catalog progress and activation-information reader. The audit
started from the September 7 build and re-ran its regression baseline before
editing. The baseline passed 1,372 functional and 92,006 localization assertions.

This is a source/test audit with compilation, not proof that every feature is
1:1 with the original PS1/EXE. No main application, reference EXE, Setup, repair,
tweak, profile Apply/Restore, driver/runtime installation or security mutation
was launched. Tests use synthetic data, disposable test files and harmless child
processes that print output or wait. No Windows settings were changed.

## Findings and fixes

| ID | Defect and impact | Correction | Evidence |
| --- | --- | --- | --- |
| R1 — High | `AcquireManagedTaskAsync` awaited the informational “Task queued” window before awaiting the lease. A lease could be granted while its workflow still waited for the user to dismiss that message, holding resources without doing work. | New `TaskAdmission` waits on the actual scheduler request. The notice is modeless, auto-dismissed on admission, and a notification exception cannot discard the granted lease. Confirmation dialogs still require the user's decision before requesting a task. | Real scheduler + pending fake notification tests, immediate admission, show/dismiss faults, original request failure, and MainWindow wiring guard. |
| R2 — High | First-run prerequisite mutations bypassed the shared scheduler. The async Loaded event did not catch wizard failures; a backend exception could leave its progress window busy. Read-only detection could finish after the dashboard closed. Slow setup stages stayed WAITING until they finished. | First-run work acquires `SystemMutation`, `AppxDeployment` and `WindowsServicing`; success/failure releases the lease. Exceptions finish the progress result and are logged. The Loaded handler has an exception boundary and checks dashboard lifetime after detection. Slow stages report RUNNING before awaiting Windows. Merely showing/skipping the wizard acquires no mutation resource. | Scheduler conflict/release tests, source guards for lease/disposal/error completion/closed-window checks and stage starts. Actual native wizard interaction is not tested. |
| R3 — High | WinGet bootstrap copied the HTTP response body without a cancellation token into a fixed shared temporary filename. A stalled body could hang preparation, or the file could collide with another attempt. The bundle was deleted even after an unconfirmed deployment timeout. | Unique per-attempt bundle; four-minute token spans headers and body; existing atomic downloader checks size/completeness and removes its own partial file. Limit: 512 MiB, minimum 1 KiB. An unconfirmed timed-out deployment retains its package. | Production token/path/retention wiring guards plus existing downloader tests for truncation, oversize, cancellation, faults, replacement and unrelated-file preservation. No network download or AppX installation executed. |
| R4 — High | A synchronous exception from an output/progress observer stopped a redirected-pipe reader. A verbose child could fill that pipe and block until timeout. | Shared runner isolates the broken observer, drains both pipes and includes its error in the final diagnostics. The real child exit code is preserved; a display callback failure does not imply Windows rollback succeeded or the command failed. | Actual harmless child writes over 384 KiB to each pipe. Throwing observer is invoked once; both end markers survive. Exit 0 and exit 7 both remain truthful, without timeout. Existing timeout/cancellation tests also pass. This does not catch arbitrary exceptions thrown later by separately queued UI callbacks. |
| R5 — Medium | Five query/profile runner implementations used separate uncancellable `ReadToEndAsync` tasks, returning early on timeout and leaving stream task cleanup inconsistent. | Licensing, security/BitLocker, performance-profile powercfg, gaming status and live gaming status now use `NativeCommandRunner`, preserving their arguments, timeout durations and result contracts. | Shared real-child stdout/stderr, timeout/cancellation and failure tests; per-service source wiring guards; complete project compilation. No actual powercfg or BitLocker operation executed. |
| R6 — Medium | Catalog progress accepted NaN/infinity, indexed unknown IDs directly, and allowed a late RUNNING callback to replace VERIFYING. An empty model could be “AllVerified.” | Validate IDs/phases, normalize non-finite percentages to indeterminate, reject verification regression and preserve terminal states. Empty progress is never verified. Renderer only applies accepted updates; NOT VERIFIED status uses failure coloring. | Finite/non-finite percentage matrix, invalid ID/phase tests, late callback/terminal immutability checks and existing batch-progress tests. Native rendering remains untested. |
| R7 — Medium | Office activation discovery only searched fixed C-drive paths. OSPP results also lacked a clear limitation for Microsoft 365 subscription activation. | Enumerate actual Program Files/x86 and 32/64-bit machine Office registration roots, including legacy layouts. Reject relative/network fallbacks and de-duplicate candidates. Keep `/dstatus` information-only and explicitly identify its scope. | Non-C, x86, legacy, InstallRoot, Click-to-Run, duplicate/absent/relative/UNC fixtures, plus read-only command wiring. No license changes or Office script execution. |
| R8 — High | Catalog Analyze rendered every `IsAvailable=false` as a failure, even for genuinely absent hardware/services. Conversely, composite Advanced rows silently ignored child read failures as if those children were absent. | Explicit confirmed-unavailability state; neutral terminal progress with a gray summary badge; unavailable rows excluded from verified/applied counts. Access denial, timeout, corrupt targets, missing backups and failed writes remain errors. Re-check availability after queue admission before calling a mutation; skip only a confirmed absent target. Composite aggregation retains child errors. Presence checks distinguish nonexistent files/service keys from unreadable or malformed targets. | 41-row count fixtures (0/1/8/41 unavailable), mixed success/absence/error matrix, terminal-event tests, fake backend write guards, late absence after failed writes, actual existing/missing-file probes, shared UI wiring checks and all 529 language-pair switches on the badge text. No real catalog Apply/Restore executed. |

### Unavailable badge behavior

- Shared Essential, Gaming and Advanced toggle catalogs show a wrapping gray
  badge, for example: `33 out of 41 have been verified, but 8 tweaks can't be
  applied due to unavailability on this PC.` The same summary appears in the
  separate progress window. New status text has resources in all 23 languages.
- Analyze counts successfully read applicable options, whether ON or OFF. This
  is not a claim that those tweaks were applied. An Apply/Restore progress window
  counts only operation results actually verified in that batch.
- Unavailable is terminal and neutral, not FAILED or COMPLETED/verified. A batch
  containing only verified and confirmed-unavailable results can finish without
  errors; any actual failed or unverified item still makes the result red.
- Components intentionally absent because their removal tweak is already
  applied (for example Widgets) retain their existing applied-state semantics.
  Missing backups and generic command failures are not relabeled unavailable.
- The badge has no fixed height, uses white text on gray and participates in the
  existing scaling/localization traversal. This is source/test evidence; native
  visual inspection at all eight scale levels remains outstanding.

For R7, Microsoft's documentation describes Office16 with and without `root`,
x86 Program Files, and the fact that OSPP does not verify Microsoft 365 subscription
activation ([Microsoft Office activation tools](https://learn.microsoft.com/en-us/office/volume-license-activation/tools-to-manage-volume-activation-of-office)).
This update does not add subscription activation support or any activation bypass.

## Scope and remaining validation

The 20 main-menu handler connections, original-reference hashes, 23 language
tables and publisher metadata were checked again. Shared changes affect the
catalog, repair, runtime, GPU, security, profile and reporting workflows that use
these helpers. This turn directly inspected startup/window lifetime, scheduler
admission, both progress models, deployment/download helpers, localization/theme
integration and all remaining duplicated redirected-process runners.

The broader per-menu ledger remains in `PROGRAM_AUDIT_2026-09-07.md`. Re-running
the previous test suites does not mean every branch of every backend was reviewed
again or that a connected menu has been interactively tested. In particular:

- **Native crash:** the older `0xc0000005` report is still unresolved. No current
  `crash.log` content was found at the application's configured path. A bounded
  read of Application event IDs 1000/1026 for the preceding seven days returned
  no matching events. This absence is not proof of stability. Startup exception
  guards address a concrete managed path, not a proven cause of that native crash.
- **Real side effects/parity:** service Running/startup restoration, registry/BCD
  snapshots, Windows Update/Store recovery, Widgets/AppX, MSI and driver/runtime
  installations still need before/after tests in disposable Windows environments.
- **Languages/layout:** all 23 resource tables remain present, but longer native
  descriptions and some results/confirmations still fall back to English. The
  new diagnostic/scope prose also uses English fallback. No claim of complete
  localization or 25–200% native layout/RTL correctness is made.
- **External Windows calls:** timeouts bound waiting and prevent repeat ownership
  where supported; they cannot guarantee that every native Windows operation can
  be forcibly cancelled. Retained deployment files are intentional when completion
  is unknown, not an assertion that the deployment stopped.
- **WinGet bootstrap:** network/install failure handling is improved, but bundle
  publisher/identity validation before deployment still merits dedicated work;
  this run does not certify the external endpoint, dependencies or a clean-PC
  bootstrap. Windows package trust/deployment checks remain in place.
- **Signing:** publisher metadata remains `Naufal Tech's Softwares`; this does not
  constitute an Authenticode signature.

## Reference checks

- Main-menu connections and defined handlers: **20/20**.
- Language resource tables: **23**, merged keys per table: **577**.
- PS1 parse errors: **0**.
- Reference PS1/project-copy SHA-256:
  `660CD561C96FC00FBB26F15DFC94C4E03F4B4594E76C1D72A021C8E2CC18833D`.
- Reference EXE SHA-256:
  `C920ADF8AB56F644DBAA8EA470F9C4B3248A9CA1DCBD15C71AC980F9C9A01BFF`.
- References remained in `<private-reference-directory>` and were not modified.

## Verification

- **1,629 functional regression assertions pass** (257 added since the baseline).
- **93,224 localization assertions pass** across 23 languages, including the
  previous language-switch/lifetime checks using XAML test doubles.
- **5 publish-stage assertions pass**.
- Final Debug build: **0 errors, 0 warnings**.
- Final Native AOT publish and Inno Setup compilation: **successful**. No app or
  installer was launched; startup, install/upgrade and native visual behavior
  are not certified by this build.

## Final artifacts

- Completed stage: `artifacts/publish/win-x64-20260909-005946-954`.
- App: `Naufal Windows Powertoys.exe`, 19,189,248 bytes.
  SHA-256: `103CD0EADD3C4FD3F9D3233E09D75A8AB77BADBC6D45B26D3081899FC75C7E72`.
- Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-7.8.0-x64.exe`,
  36,715,740 bytes.
  SHA-256: `B12BCD755CF52E947C22EA767A610CE81CC103B86B898C757CFB927269E98253`.
- Both: version 7.8.0.0, Company `Naufal Tech's Softwares`, Authenticode
  `NotSigned`. Publisher metadata is not a certificate.
- The fixed-name Setup was regenerated. Earlier timestamped publish stages are
  retained. For portable transfer, copy the complete publish-stage folder,
  including its runtime libraries/resources, not the EXE alone.

Commands (existing isolated CLI/NuGet environment):

```powershell
dotnet run --project Tests\ProfileVerification\ProfileVerification.Tests.csproj -c Debug --no-restore
dotnet run --project Tests\Localization\Localization.Tests.csproj --no-restore -- --test-only
.\Tests\ParityAudit\Inspect-StaticParity.ps1
.\Tests\ParityAudit\Test-PublishStage.ps1
dotnet build "Naufal Tech's Windows Powertoys.csproj" -c Debug -p:Platform=x64 --no-restore
.\build-installer.ps1 -NoRestore
```

New helpers: `TaskAdmission.cs`, `OfficeScriptDiscovery.cs`, `CatalogAvailability.cs`,
`CatalogAvailabilityBadge.cs`. New resources: `NativeUiCatalog.Availability.cs`.
New regression suites: `Tests/ProfileVerification/ReauditSeptember9Tests.cs`,
`Tests/ProfileVerification/CatalogAvailabilityTests.cs`.
Existing source changes: `MainWindow.xaml.cs`, `App.xaml.cs`,
`FirstRunPrerequisiteService.cs`, `NativeCommandRunner.cs`,
`CatalogProgressState.cs`, `CatalogProgressWindow.cs`,
`LicensingInformationService.cs`, `PerformanceProfileService.cs`,
`GamingStatusService.cs`, `GamingLiveStatusService.cs`,
`SecurityInformationService.cs`. Existing test entry points/project links were updated.
The availability follow-up also changes `ToggleCatalogModels.cs`,
`CatalogSelectionPlan.cs`, `DebloatCatalogService.cs`, `DebloatService.cs`,
`DebloatServiceGroupsService.cs`, `DebloatRegistryLabService.cs`,
`PerformanceLabService.cs`, `EssentialTweaksService.cs`,
`EssentialActionsService.cs`, the translation template/merge entry points, and
the localization regression tests.
