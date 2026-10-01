# Development checkpoint — 1 October 2026

This continues the existing v8.0.0.0 source. It does not mark the full backlog
complete. Earlier source changes are preserved. This checkpoint distinguishes
development source from published release binaries; source publication alone
does not update the installer available on GitHub Releases.

## Implemented and verified by regression tests

- Two HKCU privacy primitives (Advertising ID and tailored diagnostic experiences)
  share authoritative originals across Essential Telemetry and Advanced aliases.
  Legacy backup tags remain compatible mirrors. Missing values differ from zero;
  value kinds are preserved. Conflicting/corrupt originals fail closed.
- Snapshot capture is durable before system changes. Restore never captures the
  already modified current state. The original remains until both catalog owners
  have retired their backup. Interrupted canonical identity-only records require
  recovery; they are not silently overwritten.
- Static effect planning permits matching shared-original owners only for migrated
  aliases. It continues rejecting opposite desired values and independent owners.
  This is not a transactional rollback of an entire multi-action batch, nor does
  it claim every primitive is executed exactly once across all modules.
- LSA protection has an independent, guarded enable path and configured/effective
  evidence. Its original snapshot is separated from HVCI. Automatic LSA rollback
  remains unavailable because local registry evidence cannot exclude a firmware
  lock. See [security controls](security-controls.md).

| Regression suite | Passed assertions | Scope |
| --- | ---: | --- |
| ProfileVerification | 4,786 | Pure/mock action, profile, migration-codec and staged-audit regressions |
| Localization | 353 | English-only UI/resource/navigation checks |
| SecurityProtection | 54 | Mock LSA controls and test-owned snapshot files |
| BacklogModules | 70 | Fake security writes and DISM responses |
| StorageServicing | 33 | Fake servicing commands |
| VirtualMachine guards | 22 | Pure isolation fixtures |

No live audit flag was used on the host. None of these results proves native
Apply/rollback, sign-in compatibility after reboot, or WIM deployment.

## VM checkpoint and remaining user step

The authorized disposable clone was found running with all eight network adapters
disabled and clipboard, file transfer, drag/drop, VRDE and USB disabled. One guest
user was logged in. The source VM was not started or modified by this continuation.
The selected Windows installation ISO exists; it has not been serviced or deployed.

At the initial check, the optical drive still held Guest Additions, not the test
disc. A fresh test DVD was subsequently attached to the clone's existing optical
drive and verified in VirtualBox; all eight NICs remained disabled. The payload
contains 195 hash-listed files, including the updated self-contained harness and
installer. Both live entry points verify the explicit guest hardware UUID before
any mutation. This verifies media attachment, not guest execution.
See [the VM protocol](vm-validation.md). Guest authentication, UAC and terminal
execution are not automated through the desktop UI.

**Still unverified:** controls beyond the three user-tested profiles and Print-to-PDF round trip,
changed protection after guest reboot, other storage round trips, offline WIM modification/export, deployment to a separate blank
virtual disk and first boot. A running clone, successful build or exported WIM is
not evidence that these tests passed. Windows 10 validation is also outstanding.

## Changed source areas

- `SharedPrivacySnapshot.cs`, `EssentialTweaksService.cs`, `DebloatService.cs`.
- `CatalogEffectPlan.cs`, `CatalogInventoryReport.cs`.
- `SecurityMitigationModels.cs`, `SecurityMitigationNative.cs`,
  `MainWindow.SecurityMitigations.cs` from the retained security continuation.
- `Tests/ProfileVerification/SharedPrivacySnapshotTests.cs`,
  `CatalogEngineTests.cs`, the existing guest-only shared-snapshot audit and launcher.
- `Tests/SecurityProtection/`, `Tests/VirtualMachine/`, README and changelog.

Whole-program effect coverage, other snapshot migrations, remaining independent
security controls and the native VM validation matrix remain open. No original
snapshot, user VM or source installation image was deleted to make tests pass.

## Local installer

### Guest-audit follow-up

The user supplied evidence that the first manual guest run was blocked by an
existing `Essential\Telemetry` backup before any test mutation. Profile did not
run. The harness now selects a unique test-owned registry backup root through an
instance of the same snapshot-store implementation. The production constructor
and backup path remain unchanged; the isolated-root factory is test-build-only.
Five additional pure checks verify immutable production roots, independent runs
and rejection of empty run IDs (**4,708 assertions** at that point). The user
subsequently provided the successful rerun described below. Existing-production-backup migration remains untested.
Earlier installer details below describe the first build of this checkpoint,
not the subsequently rebuilt guest-audit fix.

Updated guest-audit-fix installer: **38,762,712 bytes**, SHA-256
`8A1CE7A29D4BEE81074161756A43EDF5475F7DF4417FB0126DC5F39CF50452A4`.
Native AOT and Inno Setup builds succeeded. A new checksummed test DVD was
generated for the clone. The subsequent guest rerun is recorded below.

Native AOT publish and Inno Setup compile succeeded. The installer was not installed
on the host and has not been pushed to GitHub:

`artifacts/installer/Naufal-Windows-Utility-Setup-8.0.0.0-x64.exe`

- Size: 38,761,382 bytes.
- SHA-256: `0C1DEC70383CFC81996E9C2DC1DFAEF05E6D40489DEF9F77EA270D99A3871D46`.
- Source version: 8.0.0.0; publish stage: `win-x64-20261001-074859-206`.
- Self-contained VM harness publish also succeeded; no live mode was run on the host.

## User-provided real guest evidence and next acceptance pack

The 1 October guest transcript records all three profiles at 23/23 and exact
baseline rollback after each intentional failure checkpoint. RSC was not applicable.
The shared snapshot ownership/rollback/retirement test passed, but **0/2** target
values differed from baseline. Neither result verifies old backup migration or
post-reboot effectiveness. See [scope and next steps](vm-validation.md).

New guest-only acceptance entry points prepare copy-based legacy registry migration
and staged Print-to-PDF / eligible HVCI Apply/rollback with separate reboot evidence.
Unknown or interrupted phases do not restart with a new original. LSA remains
read-only. Normal regression execution does not invoke these live entry points.

The authorized ISO contains `install.esd`, not `install.wim`; no conversion or
deployment has run. A read-only WIM/ESD readiness collector has 51 passing pure
checks. Additional guest execution and an explicitly verified deployment target
are required. No host Windows setting or original VM was changed.

The ESD was extracted without mounting/servicing, and a hash-matched copy is included
only in ignored local test media. Original installation media remain untouched.
Current continuation installer: **38,762,643 bytes**, SHA-256
`E53E4CEE0052EB7C95948CA8424CE424E6C533E6B9ACF45F908DAE1F58505548`.
Native AOT and Inno Setup succeeded; product/file metadata remain **8.0.0.0**.
The self-contained acceptance harness also published successfully. Neither the
installer nor the new live modes were run on the host; no GitHub push occurred.

The new DVD attachment was verified on the authorized running clone: **197
hash-listed files**, including the ESD copy and the new acceptance entry points.
All eight NICs, clipboard/file transfer, drag-and-drop, VRDE and USB remained
disabled. This proves payload attachment only; no new guest acceptance stage or
guest reboot was executed by this continuation. Start with LegacyMigration and
the read-only image readiness report, then review before SecurityStorage Prepare.

## Later guest results and unnecessary-reboot fix

Subsequent user transcripts verified Print-to-PDF Disabled after the first reboot
and exact Enabled baseline after the second. HVCI was NOT EXERCISED because the
existing eligibility gates blocked it; LSA remained read-only and protected.
Advertising ID legacy-copy migration succeeded; Tailored Experiences preserved
conflicting originals (Essential DWORD 1, Advanced DWORD 0) and correctly blocked.

The harness unnecessarily requested another reboot after skipping HVCI. It now
finishes skipped controls immediately and supports read-only finalization of that
older no-change waiting manifest. Identity checks, pending-servicing checks and
post-boot requirements for actual changes remain intact. Eighteen new assertions
bring the regression count to 4,786. The subsequent user transcript confirms
`CompletedWithSkippedControls` with no further reboot requested. HVCI Apply/rollback
was not exercised; closing the audit does not make skipped controls pass.

The final summary also prints `LSA read-only baseline=False`. The saved security
snapshot's internal `Lsa` property is omitted by the default JSON serializer, so
that comparison lacks its baseline. It should be treated as inconclusive, not
as an observed protection change. The live query still reported LSA-light
protection. Fixing serialization/unknown-state reporting and testing the comparison
remain outstanding; the evidence has not been rewritten to claim success.

Rebuilt no-extra-reboot-fix installer: **38,762,745 bytes**, SHA-256
`C0B49FB71988D9D38B140E5A4F685D1745D1B1FC2D16FB9E22A372C676AA2D17`.
Native AOT, Inno Setup and self-contained harness publish succeeded; no installer
was run on the host. VM guard (22) and WIM readiness (51) fixtures still pass.
