# Catalog Apply / Restore button correction

Date: 2026-09-04

## Reported symptom

Advanced Windows Tweaks & De-Bloat showed `Selected 5 | Pending changes 0`
and refused to apply. The old handler required BOTH a selected checkbox and a
manually changed toggle target. Selecting an OFF row alone did nothing.

## Corrected behavior

- Checkboxes select the operation's scope. They never change Windows.
- Apply selected applies the selected, available, not-yet-applied tweaks (ON).
  It no longer reads the switch as an action target. Already-applied rows are
  skipped with an explicit message rather than being restored accidentally.
- Pending changes counts selected, available OFF rows, matching the Apply plan.
- Restore selected calls the saved-state restore API for all available selected
  rows, including rows currently OFF. A saved snapshot may itself contain ON.
- Restore all defaults remains a distinct Windows-default operation.
- Per-row switches run a single-item Apply or saved-state Restore after a
  confirmation naming the affected item. Their visual state is reset to the
  last read-back while confirming; cancelling does not leave a fake target.
- Apply confirmation lists the affected rows and explains ON = tweak applied,
  not necessarily service/feature enabled. Administrator and high-risk Apply
  acknowledgement checks are retained.
- The catalog is busy/locked before awaiting confirmation. Cancel, failure,
  and task-lease refusal release the lock through finally. Failed restore rows
  retain their checkboxes for review/retry instead of silently losing selection.
- Select all/safe/advanced and De-select all remain selection-only operations.

This shared implementation covers Essential Windows Tweaks, Gaming Tweaks and
Advanced Windows Tweaks & De-Bloat. One-shot action cards are unchanged.

## Verification

- Debug build: 0 errors, 0 warnings.
- Regression suite: 412 passing assertions, including 39 new catalog assertions.
- New cases cover the five-selected-OFF screenshot, the complete selection /
  availability / current-state truth table, missing states, mixed applied rows,
  immutable command scope, backend Apply(true) dispatch, separate saved/default
  restore APIs, failed results and rejected invalid commands.
- Tests use an in-memory recording backend, never the Windows tweak services.
- No administrative tweak, restore, repair or reboot was executed. Interactive
  confirmation/cancellation and final published startup were not UI-tested in
  this turn. This is not a claim of complete application-wide 1:1 parity.

## Build outputs

Native AOT publish completed, including generating native code. Inno Setup
completed successfully. Publisher remains `Naufal Tech's Softwares`.

- Application: `artifacts/publish/win-x64-20260904-201711/Naufal Windows Powertoys.exe`
  (18,392,064 bytes).
  SHA-256: `32620CD21E823D6F13647E896847CDD4E4472B0E00ECD6B454FC97A53BA24066`.
- Installer: `artifacts/installer/Naufal-Windows-Powertoys-Setup-7.8.0-x64.exe`
  (36,506,880 bytes).
  SHA-256: `6FB55CEBE0EB87CBF0E43878873F7F4A107FDB220E8484F799DE4F8C64151E72`.

The installer output replaces the prior setup at the same path. Earlier staged
application folders remain intact; the old installer can be rebuilt from its
corresponding staged folder. Neither new executable was launched or installed
during this turn. Keep the complete application folder together when using the
published EXE without the installer.
