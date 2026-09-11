# NT branding and program audit — 2026-09-09

## Scope and visual source

User supplied `Assets/BrandingSource.png (original NT-s.png)`.
The unchanged 280×128 source is preserved as `Assets/BrandingSource.png`:
SHA-256 `E6203DCCC07A9DB2DC93B8F0E7F366EC880C5F765ABAAD1434DC00E4F050C711`.
All ten ICO sizes and seven package PNG assets were regenerated from this source.
The rectangular NT artwork is aspect-fitted on a transparent canvas, not cropped
or stretched. The 300×300 asset was visually inspected. The source PNG itself is
excluded from distribution.

The canonical ICO supplies the EXE, MainWindow, shared ToolWindow (including
catalogs, wizard and progress windows), Setup and uninstaller. Start/Desktop
shortcuts reference the installed ICO and the existing stable AppUserModelID.
The application name, publisher `Naufal Tech's Ltd.`, and version `8.0.0.0` remain
unchanged. See `ICON_AUDIT_2026-09-09.md` for final resource checks and artifacts.

## Findings corrected

1. **Tool-window sizing changed during unrelated preference updates.** Theme
   and language events reached the same resize path as text scaling. Resizing now
   occurs only when the geometry scale actually changes. Minimized/maximized
   sizes no longer overwrite the restored-window size baseline; Windows retains
   control of those states. Initial sizing/placement uses the monitor work area,
   including negative multi-monitor coordinates, instead of the owner's size.
   Moving monitors refreshes practical size limits. Closed windows ignore events.
2. **Close-warning lifetime could propagate an unhandled async exception.**
   After the warning completes, the dashboard may already be closed and a task
   window reference may no longer be usable. The continuation now checks those
   states; warning-display exceptions are logged instead of escaping the closing
   event. Busy-task close prevention remains in place.
3. **Publish validity covered only the EXE.** An unchanged executable allowed
   missing/altered DLLs, PRI files and icons in an otherwise marked-complete stage.
   Schema 2 records path, length and SHA-256 for every staged payload file.
   Missing, changed or extra files invalidate selection. Old EXE-only markers
   are rejected; run a fresh publish before using `-SkipPublish` with old stages.
4. **Packaging could mix newly changed branding with an older valid stage.**
   The installer build now compares staged icon resources and PNGs with current
   source assets before invoking Inno Setup, including `-SkipPublish` builds.
   The startup-order icon assertion also explicitly requires the identity call
   to exist, rather than accidentally accepting a missing search result (-1).

## Broader audit and tests

- Functional suite: 1,815 assertions passed after the source fixes. Coverage
  includes catalog selection/admission, unavailable/error distinctions, progress
  state transitions, wizard preferences, restore/default policy, profile
  verification, and bounded-process behavior. Tests do not tune this PC.
- Localization suite: 93,224 assertions passed across all 23 languages.
- Static parity inventory: all 20 Main UI menu routes resolve; each of the
  23 resource tables contains 577 entries. The reference PS1 parses without
  errors and reference hashes remain unchanged. This is structural evidence,
  not a 1:1 behavior certification.
- Publish-stage tests: 10 assertions passed, including modified/missing icons,
  extra DLLs, incomplete stages, and rejection of old EXE-only markers.
- Debug build: 0 errors, 0 warnings.

## Limits and follow-up

No native application, installer, original reference EXE or Windows-changing
repair/apply/restore operation was executed during this audit. The window
lifetime/sizing fixes are compiler-checked and code-reviewed, but native
multi-monitor, maximize/minimize, RTL and eight-scale UI tests remain necessary.
Historical native crash reports are not declared resolved by these changes.
Long-form localization completeness and real-device 1:1 behavior remain open
as described in earlier audit reports.

The existing Start Menu shortcut was read without modification and points to
the installed Program Files EXE and canonical ICO. The installed application
has not been upgraded automatically. Reading the existing taskbar pin folder
was denied by the sandbox; actual pins and their cached appearance are not
claimed verified. No shortcuts/pins or icon cache were deleted or rewritten.

Microsoft's [AppUserModelID documentation](https://learn.microsoft.com/en-us/windows/win32/shell/appids)
is the reference for matching the process/window identity to shell shortcuts.
The new installer must be installed to update the installed icon assets; use
the final artifact details in `ICON_AUDIT_2026-09-09.md`.
