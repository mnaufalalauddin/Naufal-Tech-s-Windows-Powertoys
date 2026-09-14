# Catalog expansion delivery — 14 September 2026

## Built artifacts

- Application and installer file version: **8.0.0.0**.
- Company: **Naufal Tech's Ltd.**
- Normal Release x64 Native AOT publish, followed by Inno Setup **7.1.0**.
- x64 linker: MSVC **14.51.36231**, Hostx64/x64.
- Fresh stage: `artifacts/publish/win-x64-20260914-005627-521`.
- Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`.
- Setup size: **38,177,073 bytes**.
- Setup SHA-256: `785A92718E24969EF7313BB9E498E4988799809883366A1FE0599B78D030CB8D`.
- Published application SHA-256: `CE93FF7B56283BB4AF059178AEEE059386885E9053774D42DC167BF6D92C9557`.
- Both executable signatures: **NotSigned**. No certificate or signing claim is implied.
- The stage's complete file inventory was independently rehashed against
  `publish-complete.json` after packaging and matched.

## Checks completed

- **4,333** functional/regression assertions passed with fake backends or
  read-only/static checks; no Windows settings changed.
- **98,766** localization assertions passed across **23** language tables.
- **80** static catalog/button/progress integration assertions passed.
- **26** monitoring, **13** OneDrive/Game Mode/runtime, **18** AppData routing,
  **12** installer-location, **10** publish-stage and **10** report-export checks passed.
- **91** branding/published-icon assertions passed.
- Debug compilation succeeded without warnings/errors; subsequent fresh
  Release Native AOT publish and Setup compilation succeeded.
- Production current-account AppX inventory probe completed without creating
  a mutation audit or installing/removing/registering apps. The probe build
  reported NU1900 for inaccessible online vulnerability data; no successful
  dependency-vulnerability audit is claimed.

No installer was run, no live tweak/uninstall/encryption operation was executed,
and no Git commit/push or GitHub release was made for this batch. Native UI
click/layout testing of the new additions remains unperformed. The installer
replaces the previous 8.0.0 Setup at the same output path; older complete publish
stages remain available. This receipt is outside the runtime publish payload.

See [implementation details and compatibility limits](CATALOG_EXPANSION_2026-09-14.md)
and [changelog](CHANGELOG.md). New long descriptive text may still fall back to
English. Restore availability for retired apps depends on staged packages,
Store availability, device/region compatibility and licensing.
