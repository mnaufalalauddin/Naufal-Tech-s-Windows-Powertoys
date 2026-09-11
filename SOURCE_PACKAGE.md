# Source package — 12 September 2026

Prepared from the current development source of **Naufal Tech's Windows Powertoys
8.0.0.0**, with company metadata **Naufal Tech's Ltd.** This is an independently
usable source snapshot for a new GitHub repository, not a binary release or a
claim that all outstanding program defects have been fixed.

## What was preserved

- All current application C#/XAML source, project/solution and manifests.
- Branding source, derived PNGs and ICO; publish and launch configurations.
- Installer/publish/icon scripts, tests, diagnostics source, changelog and audits.
- **168 code/project/manifest/asset files checked byte-for-byte by SHA-256 against
  the development originals: all matched.** No production application behavior
  was changed for this export.

## Export-only changes

- Added root `README.md`, `GITHUB_UPLOAD.md`, this provenance/verification note,
  `.gitignore`, `.gitattributes`, a .NET 10.0.400 `global.json`, and a credential-free
  `NuGet.Config` using the public NuGet source.
- Replaced the optional static reference auditor's developer-specific default
  directory with a required `-ReferenceDirectory` parameter. Its private inputs
  are not distributed and are not needed to compile the app.
- Updated the profile-test README's historical assertion-count explanation.
- Removed developer-home absolute paths from five copied historical documents;
  converted their local source links to repository-relative links and used
  placeholders for private analysis/reference directories.
- Left historical audit conclusions and the changelog's original cutoff intact.
  This file records the later source-export work; the development originals were
  not modified.
- Included no license assignment, account credentials, signing key, original
  reference EXE/PS1, machine-state snapshot, private diagnostic output, compiled
  EXE/DLL/PDB, `bin`, `obj`, `.vs`, `artifacts`, or NuGet cache.

## Verification performed on the exported source

Verification ran on **12 September 2026** from a second clean copy, so build/test
output did not enter the upload folder. It did not reuse the original project's
`bin` or `obj` directories.

| Check | Result |
| --- | --- |
| Main WinUI project, Debug x64, SDK 10.0.400 | Passed; 0 errors, 0 warnings |
| Functional regression console suite | 2,655 assertions passed |
| Localization console suite | 96,099 assertions passed across 23 languages |
| Static catalog/button/routing/progress checks | 80 assertions passed |
| AppData routing | 18 assertions passed |
| Installer location | 12 assertions passed |
| Publish-stage helper | 10 assertions passed |
| Report-export source checks | 10 assertions passed |
| Source/icon asset checks without a published binary | 73 assertions passed |
| Production code/project/asset SHA-256 comparison | 168 files matched the originals |
| Forbidden output/key/private-state filename check | No matches in the upload folder |
| Personal Windows home-path scan | No matches in exported code/scripts/docs/configs |
| Common private-key/GitHub-token/AWS-key pattern scan | No matches; not an exhaustive secret audit |
| Git ignore behavior in the verification copy | Output, IDE state, logs and sample key paths ignored; source/assets retained |

Dependency restore used the existing local package cache/feed to avoid a network
requirement during validation. NuGet's vulnerability-metadata audit was disabled
for this **offline verification command only**, not in the exported project or
NuGet configuration. A restricted user-level NuGet configuration initially blocked
the tooling; a process-local isolated configuration directory resolved that
environment issue without editing application source or the user's NuGet settings.

The portable package is configured for a normal first restore from NuGet on the
recipient machine. That clean-machine network restore was not tested here.
No current vulnerability/security clearance is implied.

## Not performed

- No app launch, administrative Apply/Restore, service/registry/BCD mutation,
  driver installation, Store-app removal/reinstallation, or reboot.
- No new Native AOT publication, Setup compilation/installation, or Windows 10
  runtime test during this source-packaging task. Earlier build evidence remains
  historical in `CHANGELOG.md` and the dated audits.
- No GitHub repository, remote, user identity, commit, push, public upload, or
  open-source license selection. A temporary local Git repository was used only
  in the separate verification copy to check ignore rules; `.git` is not included.

Known application issues—including blank native UI text—remain recorded in the
root README and changelog. Buildability is not full functional certification.

## Use this package

Extract it, review it, follow `README.md` to build, and follow `GITHUB_UPLOAD.md`
to upload the folder's contents. The ZIP is a transport container; committing
only the ZIP does not create a browsable source repository. This snapshot does
not synchronize later changes from the original development directory.
