# Icon and shell-integration audit — 2026-09-09

## Result

The latest supplied `images/NT-s.png` replaces the earlier blue Windows artwork
as the single visual source for the
application's Windows shell branding. It is preserved in `Assets/BrandingSource.png`
for regeneration, but is excluded from the distributed package.
Source SHA-256:
`E6203DCCC07A9DB2DC93B8F0E7F366EC880C5F765ABAAD1434DC00E4F050C711`.

## Covered paths

| Surface | Implementation | Verification |
|---|---|---|
| File Explorer / executable | 10-frame ICO is the project `ApplicationIcon` and is embedded in the EXE. | Every 16–256 px ICO frame found in the EXE resource. |
| Main title bar | `MainWindow` calls `AppWindowIcon.Apply`. | Static audit. |
| Every catalog/progress/tool title bar | The only other top-level type is `ToolWindow`; it calls `AppWindowIcon.Apply`, and all catalog/progress windows use it. | Static audit prevents unreviewed new `Window` subclasses. |
| Running taskbar group | `ShellAppIdentity` assigns stable `NaufalTechs.WindowsPowertoys` before `MainWindow` is created. | Static startup-order audit. |
| Start Menu and Desktop shortcuts | Inno Setup points each shortcut directly at `{app}\Assets\NaufalWindowsPowertoys.ico` and applies the same AppUserModelID. | Installer-source audit. |
| Setup, uninstaller, and Apps & Features | `SetupIconFile` embeds the ICO into Setup/Uninstall; `UninstallDisplayIcon` uses the installed ICO. | ICO resources found in Setup. |
| Package assets | Square, wide, splash, lock-screen and Store logo assets derive from the same source and publish beside the EXE. | Dimensions and publish hashes audited. |

The ICO contains 16, 20, 24, 32, 40, 48, 64, 96, 128, and 256 pixel frames.
The rendering preserves the complete 280×128 rectangular artwork without
cropping or stretching; square assets include transparent space above/below.

## Latest verified artifacts

- Publish stage: `artifacts/publish/win-x64-20260909-173212-481`.
- EXE SHA-256:
  `E7B88EA244EE43D34FB686FBEE74328765F963EA7D0144AFDD327C31BED9C3C8`.
- Setup SHA-256:
  `B29225EACB824A2C5F3C391720BBDDF17D8823820CE7C9E75ACF6A8C13133DA6`.
- Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`
  (38,032,399 bytes). The fixed-name older Setup was replaced by this build;
  older timestamped application publish stages remain available.
- EXE and Setup: FileVersion/ProductVersion `8.0.0.0`, company
  `Naufal Tech's Ltd.`, Authenticode `NotSigned`.
- `Test-AppIcons.ps1`: **101 static assertions passed** after the full audit.
- Stage marker schema 2 verified all **168 payload files**, not just the EXE.
  Packaging also checks the staged icon against current source assets.

## Existing taskbar pins

The installed folder supplied later in this audit was confirmed to be the old
7.8 release, with all 160 payload/manifest files matching its original publish
stage. It has none of the newer `Assets` icon files. Rebuilding the repository
does not update that installed EXE, PRI, or shortcuts; the new Setup must be
installed to deploy them. See `CATALOG_RECOVERY_AUDIT_2026-09-09.md`.

Windows keeps an existing pinned-taskbar shortcut and icon cache under the user
profile. The application should not delete or rewrite user pins. After installing
this release, an old pin may still display its old cached icon until the user
right-clicks it, chooses **Unpin from taskbar**, then launches the new Start Menu
shortcut and chooses **Pin to taskbar**. New Start Menu/Desktop shortcuts already
have the ICO and matching AppUserModelID.

The static audit does not launch the EXE, installer, or Windows shell. The
existing Start Menu shortcut points to the Program Files EXE and canonical ICO;
reading the actual taskbar pin folder was denied by the sandbox. No pin/cache
was modified, and installed icon appearance is not claimed verified. See
`NT_BRANDING_PROGRAM_AUDIT_2026-09-09.md` for related program fixes and limits.
