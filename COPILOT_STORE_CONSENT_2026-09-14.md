# Copilot source-agreement fix — 14 September 2026

## Report and cause

The Windows AI apply screenshot reports WinGet exit **-1978335162**, also
**0x8A150046 / APPINSTALLER_CLI_ERROR_SOURCE_AGREEMENTS_NOT_ACCEPTED**.
The Copilot Store-product inventory query used `--disable-interactivity` but
did not supply source acceptance after user approval. On an account without
accepted Store source terms, it failed before the AI policy/service writes.

This is not evidence that Copilot is absent, and it must not be converted to
an unavailable/gray result or silently ignored. Network, source, timeout and
ambiguous inventory failures also remain unverified.

## Implementation

- A dedicated, scrollable source-consent window is shown for Windows AI Apply
  (single toggle or bulk) and Copilot uninstall/restore in Built-in Windows Apps.
- It includes a clickable Microsoft terms link, the two-letter region-code
  disclosure, the fact that WinGet may retain acceptance, and explicit
  **Agree and continue** / **Cancel** controls. The six new UI strings have
  entries in all 23 language tables.
- Cancelling returns before task admission. The grant is created in the caller
  only after the dedicated confirmation succeeds; it is not stored in app
  preferences or shared globally between independent tasks.
- Both exact-ID `list` and `uninstall` add `--accept-source-agreements` only
  inside that grant. Passive app inventory stays deferred, with no automatic
  acceptance. A disposed grant also invalidates inherited delayed work.
- The WinGet commands still use the validated official `msstore` source,
  exact product `XP9CXNGPPJ97XX`, user scope and the same-user non-elevated
  process runner. No force, fuzzy match, all-users removal, source reset,
  purchase or installation is added to the removal workflow.
- If consent is still rejected, the error explains how to retry and review the
  agreement instead of relying only on the raw WinGet prompt.

## Verification

- Functional tests: **4,382 assertions passed**. This includes command flags,
  the screenshot error, unknown-versus-absent classification, await/background
  propagation, nested and unrelated tasks, revocation and UI-path contracts.
- Localization: **99,732 assertions passed**, across 23 language tables.
  These checks do not constitute native-speaker or interactive visual approval.
- Main WinUI Debug x64: **0 warnings, 0 errors**.
- No live Windows AI Apply, Copilot removal, Store agreement acceptance, app
  installation or Windows setting change was performed to validate this fix.
  Actual Store/network availability and deployment success remain environment-
  dependent. Use the new Setup for a user-approved runtime retry.

## Fresh delivery

- Native AOT publish and Inno Setup 7.1.0 packaging completed successfully.
- Verified stage: `artifacts/publish/win-x64-20260914-070413-605`.
- Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`.
- Setup version: **8.0.0.0**; company: **Naufal Tech's Ltd.**;
  size: **38,182,645 bytes**.
- Setup SHA-256:
  `BE7C6890527AE419B8D1D9DD2E9A250B11032EE92C5474F5DBC3EA9B165DBA72`.
- Application SHA-256:
  `70BD1E4CA0A012824A1C94BA6DBAB51091D96F15B4546B87CC8CD0B3EDBA0D2C`.
- Application PE is x64 native with no CLR header. All staged payload hashes
  and 91 source/published icon assertions passed.
- Both EXEs are **unsigned (NotSigned)**. Neither was installed or launched.
- This installer supersedes the earlier same-date catalog-expansion installer;
  the GitHub source upload excludes both binary artifacts.

## Primary references

- [Microsoft WinGet list command](https://learn.microsoft.com/en-us/windows/package-manager/winget/list)
  documents exact/source/scope filters, source acceptance and non-interactivity.
- [Microsoft WinGet return codes](https://github.com/microsoft/winget-cli/blob/master/doc/windows/package-manager/winget/returnCodes.md)
  identifies the reported HRESULT as unaccepted source agreements.
- [Microsoft Store terms](https://aka.ms/microsoft-store-terms-of-transaction)
  is the agreement link shown in WinGet and in the application's confirmation.
