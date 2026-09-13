# Source synchronization - 13 September 2026

This update synchronizes the latest local development source of **Naufal Tech's
Windows Powertoys 8.0.0.0** into the existing GitHub repository. Company metadata
remains **Naufal Tech's Ltd.** The base is the previously merged `main` commit
`9991d4faddf62158277b5306c41980a1f5ebc098`; no history rewrite is required.

## Included changes since the initial upload

- Complete legacy Photo Viewer launch/Open with registration for PNG, JPG, and
  the other supported extensions, with snapshot/read-back regression coverage.
  Selecting the default viewer remains a user-confirmed Windows action.
- Explicit Game Mode OFF behavior rather than routing OFF to Restore.
- Microsoft OneDrive in the built-in app manager (32 entries in total), including
  same-user, scope-aware command execution and restoration behavior.
- CPU, RAM, GPU 3D, and network history graphs, plus running-only Task Monitoring.
- Adaptive header layout, Text Scaling recovery, and a backend-free native
  header test host. Its interactive test is not run by this synchronization.
- NTFS flag-aware state verification and related regression tests.
- Removal of WebView2 from the runtime compatibility/installer catalog, not from
  unrelated applications that may depend on it.
- Latest development changelog, fix notes, source tests, and delivery rules.

## Export preservation and privacy

The application implementation was not redesigned during this sync. **211
application, test, build-script, project, configuration, and asset inputs matched
their development originals byte-for-byte by SHA-256 before Git line-ending
normalization.** The repository retains its normal text/binary attributes.

Repository-only setup files (`README.md`, `.gitignore`, `.gitattributes`,
`global.json`, and the public-source `NuGet.Config`) remain in place. The existing
sanitized historical audit documents and the explicit `-ReferenceDirectory`
parameter in the optional private-reference auditor were preserved. The new
changelog content was copied with its old personal analysis path sanitized again.

The staged source includes no compiled application or Setup EXE, DLL/PDB, IDE
state, package cache, runtime backups, private reference executable/script, logs,
crash dumps, credentials, or signing keys. A tracked-file scan found no personal
developer-home paths or common private-key/GitHub-token/AWS-access-key patterns.
Pattern checks are not a comprehensive security audit.

## Verification on this checkout

Checks were executed on 13 September 2026 with .NET SDK **10.0.401**:

| Check | Result |
| --- | --- |
| WinUI Debug x64 restore/build | Passed; 0 errors, 0 warnings |
| Functional regression suite | 3,434 assertions passed |
| Localization suite | 97,662 assertions passed across 23 languages |
| Catalog/button/routing/progress checks | 80 assertions passed |
| Monitoring source wiring | 26 assertions passed |
| OneDrive/Game Mode/runtime wiring | 13 assertions passed |
| AppData routing | 18 assertions passed |
| Installer location | 12 assertions passed |
| Publish-stage helper | 10 assertions passed |
| Report export | 10 assertions passed |
| Source/icon assets, without launching the app | 73 assertions passed |
| Source-input SHA-256 comparison | 211 inputs matched |
| Forbidden tracked file / common credential / personal path scan | No findings |

Build and regression output stayed in ignored `bin`, `obj`, and `artifacts`
directories. Dependency restore used the repository configurations and available
package cache. NuGet vulnerability auditing was disabled for these verification
commands only; the repository configuration was not changed to disable it. This
does not imply current dependency vulnerability clearance or a clean-PC restore
test without cached packages.

## Boundaries

- No repair/tweak actions, app removal, driver installation, default-app change,
  or interactive application smoke test were run for this sync.
- No new application behavior was authored, no new version was assigned, and no
  installer was generated or executed for this source-only synchronization.
  The corresponding existing Native AOT/Setup delivery is recorded in
  [PHOTO_VIEWER_FIX.md](PHOTO_VIEWER_FIX.md) and [CHANGELOG.md](CHANGELOG.md).
- This is not certification of complete legacy 1:1 behavior, all Windows 10/11
  hardware configurations, or resolution of every previously reported UI issue.
- No software license is selected by synchronizing source. The owner still
  needs to choose redistribution terms before claiming an open-source license.

For the historical first export and its separate checks, see
[SOURCE_PACKAGE.md](SOURCE_PACKAGE.md).
