# Full Behavioral Parity Audit — 2026-09-03

Historical milestone. See `PARITY_AUDIT_2026-09-04.md` for subsequent profile
corrections, current artifact hashes and current-binary verification limits.

## Verdict

The native application now has structural coverage for the reference Main UI,
catalogs, languages, report surfaces, and repair-stage models. It must **not**
yet be described as proven 1:1 behavior on every Windows configuration.
Destructive and administrative paths still require controlled testing in a
disposable Windows VM with before/after snapshots.

## Authoritative reference

- `Naufal Windows Powertoys V7.8.exe`
  - SHA-256: `C920ADF8AB56F644DBAA8EA470F9C4B3248A9CA1DCBD15C71AC980F9C9A01BFF`
- `V78.ps1`
  - SHA-256: `660CD561C96FC00FBB26F15DFC94C4E03F4B4594E76C1D72A021C8E2CC18833D`
  - 38,482 lines and 649 parsed functions; PowerShell AST parse errors: 0.

The supplied executable is a Win-PS2EXE wrapper whose embedded script matches
the supplied script. The script is therefore the behavioral specification.
Neither reference artifact is executed by the porting/audit workflow.

## Verified static coverage

- All 20 final Main UI routes are present and connected.
- Final visible catalog ownership/counts are represented:
  - Performance: 38 rows.
  - Essential: 5 final toggles, 1 final action, and 8 utility actions.
  - Gaming: 6 manual toggles, 8 BCD rows, 38 Performance rows, and 15 actions.
  - Advanced: 41 final feature rows.
- Runtime compatibility has 18 analyzed entries.
- Legacy Windows Panels has 15 entries.
- UI language selector has all 23 reference languages.
- Text scale has 25%, 50%, 75%, 100%, 125%, 150%, 175%, and 200% plus reset.
- Maintenance stage counts match the final reference:
  - Full Repair: 2.
  - Quick Repair: 1.
  - Windows Update Fix: 6.
  - Microsoft Store Fix: 9.
  - Explorer Fix: 7.
- Product name/version/icon and the user-requested executable name are set.

## Corrections made during this full audit

1. Added `requireAdministrator`, matching the reference manifest.
2. Added the reference single-instance policy and duplicate-instance notice.
3. Prevented exit while a task is active and opens Active Tasks after warning.
4. Added the schema-2 first-run prerequisite workflow:
   - Restore Point;
   - WinGet/App Installer;
   - WMIC Feature on Demand;
   - DISM, WMI, AppX, State Repository, and servicing checks;
   - nine-stage progress and persisted completion/suppression state.
5. Separated catalog selection from state changes.
6. Corrected bulk restore semantics:
   - Restore Safe/Advanced/Selected replays first-change snapshots;
   - Restore All Defaults applies explicit Windows/default behavior;
   - restore buttons no longer merely force every row toggle to OFF.
7. Added explicit original/default restore implementations for Gaming, BCD,
   Performance, Essential, Debloat registry/service/network groups, Xbox, and
   Windows AI backends.
8. Corrected Xbox restore fallback to the three public Store components when
   no package snapshot exists, with Game DVR and service fallback handling.
9. Added restart-candidate collection and a post-verification restart decision
   for Advanced De-Bloat operations.
10. Kept long-running repair/catalog progress, percentage, elapsed time,
    read-back state, and failure reporting.
11. Replaced the former global busy gate with the reference-style task model:
    - named logical resources;
    - non-conflicting/read-only concurrency;
    - FIFO conflict queue and automatic start after lock release;
    - duplicate active-task rejection;
    - live RUNNING/QUEUED counts, waiting owner, elapsed time, and history;
    - close/reboot protection while running or queued work exists.
    The six reference mutation routes use the same resource names as the
    script; native-only mutation surfaces join the same `SystemMutation` lock.
12. Added guarded compatibility for existing original-state data:
    - Xbox Restore reads the existing package snapshot when no native snapshot
      exists, while retaining the package allow-list and Gaming Services
      exclusion;
    - the eight low-risk de-bloat rows and 38 Performance Lab rows can import
      their exact previous JSON snapshot into the native backup schema;
    - manual Gaming original state is recognized for Dynamic Tick, HPET,
      Game DVR, MPO, Windowed Optimization, HAGS, and Game Mode;
    - imports require an exact catalog ID, registry hive, path, and value-name
      match, never overwrite a native snapshot, and reject partial snapshots.

## Intentional presentation differences requested by the product owner

- Every Main UI tool opens as a separate resizable WinUI 3 window.
- Tool contents stretch/reflow with their window.
- Long explanatory paragraphs were removed from tool headers.
- Native output is self-contained Native AOT and never embeds or launches the
  legacy PowerShell script.
- Output filename is `Naufal Windows Powertoys.exe`.

These differences mean pixel-for-pixel UI identity is not a goal. Feature and
behavior parity remains the goal.

## Remaining certification work

The following items prevent an honest blanket “1:1 verified” statement:

1. **Administrative mutation matrix** — every Apply/Restore/default path must
   be exercised on clean Windows 10 and Windows 11 VM snapshots, including
   unavailable and partially installed hardware/features.
2. **Driver/firmware-dependent paths** — GPU Driver Manager and MSI Mode need
   NVIDIA, AMD, Intel, line-based IRQ, MSI, and MSI-X test machines/VMs.
3. **Servicing edge cases** — Windows Update, Store, DISM/SFC, WMIC, Recall,
   Widgets, AppX provisioning, and WinGet need online/offline/corporate-source
   test cases and restart-required cases.
4. **Security paths** — Defender, Smart App Control, BitLocker, service locks,
   and high-risk de-bloat require snapshot rollback validation.
5. **Task scheduler runtime matrix** — the named resource-lock queue,
   duplicate rejection, automatic dispatch, Active Tasks states, close guard,
   and reboot guard are implemented and build-verified. Concurrent mutation
   timing and queued-window lifecycle still need interactive VM validation.
6. **Remaining state migration matrix** — Xbox package, low-risk de-bloat,
   Performance Lab, and manual Gaming original-state data are now compatible.
   Other historical transaction/state files still require per-schema review
   before they can be declared safely reusable by every native backend.
7. **Signed release** — the current AOT executable is unsigned. Signing affects
   Windows trust prompts, not feature logic, but is required for release-grade
   distribution.

## Latest build evidence

- Debug x64 build: 0 errors, 0 warnings.
- Native AOT x64 publish: succeeded, including native-code generation, with no
  trim/AOT warnings after the first-run state writer was made AOT-safe.
- Output: `Naufal Windows Powertoys.exe`, 17,764,864 bytes, SHA-256:
  `DA089F5858D48ABC5FA58C2C005AD60B0E14E68612BB1CAD728442BEFD445CC0`
- The one-command installer pipeline completed successfully after its Windows
  App SDK staging path was corrected. Setup output:
  `Naufal-Windows-Powertoys-Setup-7.8.0-x64.exe`, 36,328,903 bytes, SHA-256
  `73742F79AE127882743819DDA34B4ACDA0219AB8E66198BE087DA7A2D25633CB`.
- Installer staging: `win-x64-20260903-213657`, 160 files.
- Both the application executable and Setup executable are currently unsigned.
- No repair, driver installation, registry/BCD/service mutation, Defender,
  BitLocker, de-bloat, or reboot action was executed during this audit.

## VM certification rule

For each mutating definition, capture the reference and native application's
before state, requested action, command/API result, after state, restore result,
and restart result on disposable snapshots. A row is certified only when both
applications reach the same observable Windows state or a reviewed, documented
safety exception is approved.
