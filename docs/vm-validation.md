# Disposable VM validation

This protocol validates the existing production backends through the application;
it does not substitute a second implementation of Apply/Restore. Unit-test passes,
VM startup, DISM exit codes and successful WIM export are **not** deployment passes.

## Initial evidence — 30 September 2026

- VirtualBox 7.2.20 was found. An explicitly authorized **full independent clone**
  of the user's Windows 11 test VM was created under ignored `artifacts/vm-validation`.
  The original VM and disk were not modified or started.
- The initial registered-clone approach was blocked by the approval reviewer because
  it could temporarily inherit NAT. The safer workflow created an **unregistered**
  clone, disabled inherited networking in that clone, then registered it.
- Before first boot, read-only checks verified all eight NICs disabled, no shared
  folders, no clipboard/file transfer/drag-and-drop, no VRDE or USB controllers,
  and an independent normal base VDI wholly inside the owned clone directory.
- A powered-off baseline checkpoint was created. The clone booted headless;
  Guest Additions reported run level 2 and Windows build 26300.9539. A later
  read-only query reported **one logged-in user** after manual login. Guest desktop
  interaction and production-backend execution were not verified.
- User-selected Windows installation ISO exists and was read only for file identity.
  It has not been attached, installed, mounted, altered or used for deployment.
- `Tests/VirtualMachine/Test-Guards.ps1`: **22 pure assertions passed**. These are
  isolation-policy fixtures, not real Apply/rollback results.
- **Apply/rollback, post-reboot protection effectiveness, WIM servicing and WIM
  deployment have NOT RUN.** Guest login and explicit in-guest operation remain
  required. No password should be placed in source, command arguments or chat.

Local VM UUIDs and private media paths belong in local evidence, not public docs.
The disposable clone is retained for continued validation; it was not deleted.

## Read-only preflight

### Follow-up — 1 October 2026

The retained clone was found running, with networking and sharing still disabled.
Its optical drive initially still contained Guest Additions. A newly generated
NWU test DVD (195 hash-listed files) was attached to that clone and the attachment
was verified. It includes the new installer and a rebuilt self-contained harness.
Both profile and shared-snapshot live entry points now independently check the
explicit guest hardware identity, even when invoked without the PowerShell launcher.
No guest audit, reboot or WIM deployment was executed by this continuation. The
PowerShell launcher still requires manual invocation and typed consent inside the
guest. See [the checkpoint report](checkpoint-20261001.md).

Run from repository root with the exact **disposable** VM UUID and its full owned
directory. The VM must be powered off for this pre-boot check. A failure is not
silently repaired. No hypervisor configuration is changed by either test script.

```powershell
.\Tests\VirtualMachine\Test-Guards.ps1
.\Tests\VirtualMachine\Test-VirtualMachineReadiness.ps1 `
    -VmId '<disposable-vm-uuid>' -OwnedDirectory 'C:\exact\owned\clone'
```

The readiness check rejects external disks and external snapshot parents, unknown
disk formats, enabled adapters, sharing and identity/path mismatches. Existing
snapshot chains are accepted only when every parent remains inside the clone.
Keep JSON output in ignored `artifacts` or `TestResults`, not in commits.

After manual login, collect guest identity and effective security evidence:

```powershell
.\Tests\VirtualMachine\Get-GuestValidationEvidence.ps1 `
    -ExpectedGuestUuid '<verified-guest-hardware-uuid>'
```

The hardware UUID must first be compared with the selected clone, not guessed from
the guest's computer name. The collector refuses non-VirtualBox systems and UUID
mismatches. Transfer the signed/hashed build and scripts by read-only ISO where
available; do not enable host sharing or networking merely to simplify testing.

## Manual live guest launcher

The prepared test disc contains a self-contained production-backend console harness,
`Invoke-GuestRoundTrip.ps1`, the read-only evidence collector and SHA-256 checksums.
The launcher is intentionally bound to the authorized clone UUID, rejects physical
PCs and other VMs, requires Administrator rights, refuses a running utility, checks
all harness files against the payload manifest and requires typed consent. It must
be run directly from the read-only test DVD. It is **not** run by the agent through
a terminal UI, and it does not collect credentials or automatically reboot.

Inside the disposable Windows guest, manually open PowerShell as Administrator.
Find the test DVD letter in This PC; replace `E:` below with that letter:

```powershell
& 'E:\Invoke-GuestRoundTrip.ps1' -Audit Both
```

Enter the exact confirmation displayed. `Both` runs the narrow shared-snapshot
round-trip first, then the profile audit only if it succeeds. The shared-snapshot
test covers two production HKCU registry targets, not the whole Telemetry/service
bundle. Its initial version safely refused to run because the clone already had
a Telemetry backup. The revised harness uses the same snapshot-store implementation
with an immutable, unique test-only backup root. Production backup paths are neither
consumed nor deleted; the application's normal backup location is unchanged. This
tests native capture/alias/retirement behavior but **not migration of existing
production backups**. Logs count targets that actually differed from baseline;
an already-applied target does not prove a successful Windows-setting mutation.
Failed test snapshots remain for diagnosis. The test-root factory is compiled only
into the harness, not the shipping application.
`SharedSnapshot` and `Profile` can also be selected individually for diagnosis.
If local execution policy blocks the script, stop and report the message; do not
change organization policy or disable security controls to force execution.

Logs remain on the guest's local disk beneath
`%LOCALAPPDATA%\Naufal Windows Powertoys\VmValidation\Run-...`. A failure stops
subsequent tests. Review the actual before/after/rollback evidence before reporting
PASS. Preserve the failed-state logs before reverting the disposable checkpoint.
No transcript or profile data should be committed to the public repository.

## Recorded guest results — 1 October 2026

User-supplied guest logs record all three performance profiles at **23/23** and
exact independent baseline restoration after each intentional failure checkpoint.
The RSC row was **not applicable**: 23/23 is the verifier's score, not proof that
23 hardware capabilities changed. Elevated BCD reads succeeded. These results do
not establish post-reboot profile effectiveness or Windows 10 compatibility.

The two-target shared-snapshot audit also completed, preserving first originals,
cross-catalog ownership, exact value/type/absence and retirement behavior. Both
settings were already zero (**0/2 differed from baseline**), so this is not evidence
of two changed Windows values. Production backup migration was not exercised.

During continuation the clone's clipboard was found host-to-guest. With the user's
explicit approval it was disabled again; all eight NICs remained disabled.

## Acceptance stages

### Later user-provided execution evidence — 1 October 2026

The following supersedes the preparation-only status for these specific cases:

- Legacy-copy migration: Advertising ID migrated; Tailored Experiences blocked
  conflicting saved DWORD originals (Essential 1 versus Advanced 0). Source
  fingerprints were unchanged. Seven native registry-format fixtures passed.
  The real conflict is unresolved; chronology cannot be inferred from these tags.
- Print to PDF: initial Enabled, Disabled verified after one reboot, restore
  configured successfully, and exact Enabled baseline verified after a second
  reboot. Actual printing and other storage controls were not tested.
- HVCI: NOT EXERCISED. Management indicators were present, lock state unknown,
  Code Integrity enforced and VBS not enabled. None of these gates was weakened.
- LSA: read-only evidence reports LSA-light protection; no Apply/rollback tested.
- ESD index 1: metadata identifies Windows client Professional x64 build 26300.
  Approximately 63 GB guest space was free and recorded pending indicators were
  false. Only the guest's system disk was present, not a deployment target.

The first staged harness unnecessarily requested a third reboot after skipping
HVCI. The corrected harness completes skipped controls without inventing reboot
coverage. Existing `AwaitAppliedBoot` evidence with **no security change** and
completed post-boot storage verification can be finalized by `Resume` without
another reboot; it only reads system state and writes test evidence. The old
manifest is not manually edited or deleted. A security change, pending storage,
identity mismatch, unexpected private backup or unknown phase still fails closed.
Finalization with the corrected harness is not yet verified in the guest.

### Reproduction procedure

Use the updated read-only test DVD, inside the disposable guest only:

```powershell
& 'D:\Invoke-GuestRoundTrip.ps1' -Audit LegacyMigration
& 'D:\Invoke-GuestRoundTrip.ps1' -Audit SecurityStorage -Stage Prepare
```

Replace `D:` if the DVD has another letter. Run the second command only after
reviewing the first result. Each command asks for typed confirmation.

- **LegacyMigration:** copies only the two audited Essential/Advanced registry
  backup tags into unique test storage. Calls the production migration logic on
  those copies, compares canonical values/types/absence and rechecks source
  fingerprints. Missing/conflicting/corrupt originals are reported, not made into
  passes. Seven native registry-format fixtures cover migration and fail-closed
  cases. This is a copy-based rehearsal, not in-place migration of all old backups
  or JSON/file backup formats. No real Windows settings are changed by this mode.
- **SecurityStorage:** accepts only initially Enabled Print to PDF. Disables it
  without removing payload, pauses for a manual guest reboot, verifies Disabled,
  restores Enabled, then pauses for another reboot to verify the original state.
  It does not test printing a document, all capabilities, cleanup or drivers.
- **HVCI:** only enables an eligible initially-disabled protection using the
  existing strict production gates; never weakens an already-enabled protection
  for coverage. After a separate manual reboot, examines running DeviceGuard
  evidence, restores the exact original configuration, and requires another boot
  to compare effective baseline. Unavailable/managed/locked states stay blocked.
  LSA is evidence-only; no firmware unlock or automatic LSA rollback is attempted.

The launcher uses one persistent `SecurityStorageAcceptance` evidence directory.
Prepare refuses an existing run. The manifest binds the guest UUID, SID, machine,
Windows build and boot identity. When it explicitly reports `Await...Boot`, restart
**only the disposable guest** manually and run:

```powershell
& 'D:\Invoke-GuestRoundTrip.ps1' -Audit SecurityStorage -Stage Resume
```

Repeat only for another explicitly requested boot phase. Same-boot, changed-machine,
interrupted and failed phases are rejected. Preserve logs and review/recover the
clone checkpoint on failure; do not delete a manifest to force a new test.
An exit code of zero while awaiting reboot is **not** completed verification.

### Source image discovery

Read-only archive listing found `sources\install.esd` (5,692,219,812 bytes) and
`sources\boot.wim` (709,860,410 bytes) in the authorized ISO. Only `install.esd` was
subsequently extracted to a new ignored artifact directory and copied into the
test DVD payload, with matching SHA-256. No image mount, export, servicing or
deployment was performed. The app's offline workspace accepts
WIM, so the selected ESD index must first be exported to a **new** WIM inside the
guest with adequate free space. The original ISO must stay unchanged.

On the new test DVD, collect metadata first (adjust the DVD letter if necessary):

```powershell
& 'D:\Get-WimDeploymentReadiness.ps1' -SourceWim 'D:\WindowsSource\install.esd' -Index 1
```

Index 1 is an explicit candidate, not a claim of matching image identity before
the metadata query succeeds. Retain the JSON output privately for review before
any export/deployment step. The ESD is local test media, never a GitHub asset.

`Get-WimDeploymentReadiness.ps1` collects guest-only optical-source metadata, image
index identity, free space, pending servicing and disk inventory. It does not select
or authorize a target disk. Its **51 pure fixture checks** are not guest execution
or deployment proof. Actual readiness collection, WIM servicing/export, deployment
to a separately verified blank disk and first boot remain outstanding.

## Final recorded guest result — 1 October 2026

The latest user-provided continuation reached `CompletedWithSkippedControls`.
Print to PDF returned to its original Enabled state after the second reboot;
no additional reboot is requested by that audit. HVCI Apply/rollback was not
exercised because the existing eligibility gates blocked it. LSA remained read-only
and reported live LSA-light protection. This does not validate other storage or
security controls, profile effectiveness after reboot, or WIM deployment.

The printed `LSA read-only baseline=False` is a known reporting defect: the
internal LSA snapshot property is omitted from the default JSON persistence, so
the resumed comparison has no baseline. Treat that comparison as inconclusive;
do not interpret it as protection loss or weaken protection to obtain a pass.
Serialization/reporting correction and a regression test remain outstanding.
Retain the original evidence. Do not rerun Prepare or delete manifests solely
because the completed audit includes skipped controls.

## Apply / rollback matrix

Use the built application and its production snapshot/action engine. Record the
source commit or archive hash, installer/executable SHA-256, guest build, checkpoint,
target stable action IDs, before state, app result, after state and exact rollback.

| Test | Required evidence |
| --- | --- |
| Shared primitive reached through two catalogs | One original value/type/absence captured; no second snapshot overwrite; both reads agree. |
| Apply already-applied action | No extra mutation; original snapshot remains unchanged. |
| Restore through alternate catalog | Exact original value/type/absence restored; no invented fallback. |
| Conflicting or foreign snapshot | Fail closed before writes; evidence explains conflict. |
| Individually supported security control | Confirmed single protection only; unsupported/managed/locked state denied; no generic mitigation masks. |
| Security rollback | Exact configuration restored; reboot guest only and separately verify effective protection. |
| Storage feature/capability | Select a present supported target; fresh preflight; verify poststate; restore original state using retained payload/source. |
| Interrupted/failing action | Durable evidence survives; uncertain outcome remains uncertain and does not claim success. |

Use application confirmations, not direct registry edits to manufacture a passing
test. Do not weaken eligibility gates to accommodate a VM. An unavailable protection
is a **blocked/not-applicable test**, not a successful disable/restore. Do not test
ResetBase or irreversible cleanup as if it had an exact Undo.

## WIM servicing and deployment matrix

1. Retain and hash the original ISO/WIM. Select its exact client image index.
2. Through Offline Image Workspace, clone that WIM into a unique guest workspace;
   inspect target metadata and mount identity before selecting any removal.
3. Exercise one allowlisted present feature/capability/app at a time. Capture both
   planned and actual states, dependencies/errors and every DISM log.
4. Verify Discard preserves the original image. In another session, Commit then
   Export into a new WIM. Record hashes, image identity and selected changes.
5. Deploy only to a separately identified blank disposable virtual disk/VM. A
   successful export alone is not deployment. Never run disk formatting or
   partition commands on the host or on the original user VM's disk.
6. Verify first boot/OOBE, login, selected removals, retained features, app launch,
   servicing health and rollback/recovery procedures. Document any offline limits
   (for example, Windows Update cannot be tested with networking disabled).
7. Repeat on Windows 10 and relevant Windows 11 builds before claiming cross-build
   support. Unsupported boot/security/device configurations remain unverified.

Deployment requires exact target-disk confirmation and suitable installation/boot
media. This repository intentionally includes no unattended `diskpart clean`,
credential embedding, hypervisor bypass, automatic host reboot or host servicing
command in the VM test tooling.
