# Language switching follow-up — 7 September 2026

## Request and findings

The user reports that switching languages leaves text from the previous language
in the app. This follow-up changes the shared localization adapter, not Windows
settings or the translation dictionaries.

- **Confirmed source contamination:** the BitLocker monitor appended its final
  sentence with `detailsText.Text += ...`. The text-property callback can already
  have translated that property, so reading it to construct the next source
  produces mixed-language canonical text. The full status is now composed from
  backend values in a local canonical string, then assigned once.
- **Lifetime vulnerability:** canonical values previously lived only in a
  `ConditionalWeakTable` keyed by managed DependencyObject wrappers. There was
  no window-owned retention or callback-token cleanup. The portable resource
  tests retained every UiLocalizedValue themselves and did not exercise this
  lifecycle. This is now protected by a window-scoped strong retention set.
  Native wrapper collection as the cause of the user's particular screen has
  NOT been visually reproduced; the fix addresses the vulnerable code path.
- **Detached content:** before removed rows are forgotten, their callbacks are
  unregistered and their original source text is restored. Reinserted content
  therefore cannot adopt a previous translation as its source. Unload alone
  does not discard state. Both MainWindow and ToolWindow explicitly release
  observers, timer subscriptions and callbacks when they close.
- **Popup/content consistency:** context menus, nested menu items, object
  headers and object tooltips now share the same traversal and lifetime set.
  The scale flyout is attached to its button and translated as part of its
  owning window, rather than through an independent one-off scan. An attached
  flyout is used (not Button.Flyout) to retain the existing explicit ShowAt
  behavior without also enabling automatic click-to-open.

Authored controls only are localized. Template-created text, raw log buffers,
user input, service IDs, paths, registry values and backend operation names are
not rewritten. Language display names stay in their native language. No reverse
lookup from translated text is used.

## Verification and limits

- 92,006 localization assertions pass (previously 80,676).
- New tests compile the actual production `UiTranslation.cs` adapter against
  small test doubles for authored XAML controls. They execute all 529 ordered
  language pairs on existing controls, forced managed garbage collection,
  repeated callback registration, dynamic property changes, nested menus,
  tooltips, object headers, removed/reinserted rows, unload/reload and close.
- A separate weak-reference test verifies that window retention keeps a managed
  object alive and that removal/close releases it exactly once. This does not
  simulate the Windows Runtime's RCW implementation.
- Read-only logs, editable text, literal language names and numeric output are
  checked for preservation. A source guard prevents appending to displayed Text.
- 914 functional regression assertions and 5 publish-stage assertions pass.
- Actual WinUI Debug build: 0 errors, 0 warnings. Native AOT publish and Inno
  Setup compilation succeed; completed-stage hash verification succeeds.

**No claim of native UI verification:** Computer Use listed the installed app
as not running. An attempted launch of the preceding completed publish was
followed by `computer-use request budget exhausted`; launch outcome is unknown.
No UI clicks/language changes were performed. Testing did not switch to shell
UI automation after that limit. No Apply/Restore/repair operation or installer
was executed. Native screenshots, clipping, live locale changes and startup
stability for this new build remain unverified.

The existing resource coverage remains 573 entries per language. Missing native
feature descriptions and app-authored messages recorded in
`LOCALIZATION_AUDIT_2026-09-07.md` are still open; this is not a claim that every
screen is now fully translated. The older 0xc0000005 report is not resolved by
these tests.

## Output

- Stage: `artifacts/publish/win-x64-20260907-142857-426`.
- App: `Naufal Windows Powertoys.exe`, 18,968,064 bytes.
- App SHA-256: `ED42578D1AC38076FD7F86A25028E31512814E05BD7C68AB5F3A4E9175878BB8`.
- Installer: `artifacts/installer/Naufal-Windows-Powertoys-Setup-7.8.0-x64.exe`.
- Installer size: 36,653,262 bytes.
- Installer SHA-256: `FD25EEA88D15BF364D32ADFEB45CA7BFFEB26E919A66E476A91902DCAF15C860`.
- Both version 7.8.0.0, company `Naufal Tech's Softwares`, Authenticode NotSigned.
- Same-name installer output replaced its preceding build. Historical publish
  stages remain available; user backups and reference files were not removed.

Close the preceding app before running the new build. Do not copy just the EXE
out of its publish folder: use the whole publish folder or the new installer.

## Re-run non-mutating checks

```powershell
dotnet run --project Tests\Localization\Localization.Tests.csproj --no-restore -- --test-only
dotnet run --project Tests\ProfileVerification\ProfileVerification.Tests.csproj -c Debug --no-restore
dotnet build "Naufal Tech's Windows Powertoys.csproj" -c Debug -p:Platform=x64 --no-restore
.\Tests\ParityAudit\Test-PublishStage.ps1
```
