# Legacy Windows Photo Viewer registration repair

Date: 12 September 2026.

## Confirmed cause

The previous Essential tweak wrote 13 capability mappings and an Applied marker,
but did not create their launch handlers or Open with registrations. Read-only
inspection on this PC found PhotoViewer.dll and the Windows TIFF handler, but no
PhotoViewer.FileAssoc.Jpeg or PhotoViewer.FileAssoc.Png open command. The old
readback could still show ON because it never examined those handlers.

## Changes

- Register one application-owned ProgID, NaufalTechs.PhotoViewer.Image, with a
  quoted rundll32 / PhotoViewer.dll ImageView_Fullscreen command, display name,
  application identity, and icon. Do not replace Windows' built-in TIFF handler.
- Point all 13 capability mappings at that implemented handler and add an
  OpenWithProgids entry for each extension: CR2, JPG, WDP, JFIF, DIB, PNG, JXR,
  BMP, JPE, JPEG, GIF, TIF and TIFF. Decoding remains subject to the legacy
  viewer's codec support; registration does not install extra RAW codecs.
- Verify all 36 registration values, including the effective merged class
  registration and the command. Do not use the old Applied marker as evidence.
- Snapshot all targets before any registration write. Retain the original 15
  snapshot tags so earlier backups and upgraded backups can both be restored.
  Require the entire version-2 snapshot before Restore; verify restored values
  and kinds, retain backups on failure, and never guess missing saved values.
- Notify the Windows Shell after Apply/Restore. Open Default Apps after successful
  Apply (Windows 10 generic Settings page; Windows 11 app-specific page).
- Do not write UserChoice, its hash, extension defaults, or other apps' Open with
  entries. ON means the viewer is registered, not that a user chose it as default.

## Verification

- 3,434 functional regression assertions passed, including 121 new Photo Viewer
  assertions for missing/wrong handlers, quoted image paths, no forced defaults,
  capture-before-write, legacy/partial/upgraded snapshots, exact Restore, and
  corrupted snapshots. Registry changes in these tests are in-memory only.
- 97,662 localization assertions across 23 languages passed.
- 80 static catalog button/routing/progress assertions passed.
- Explicit read-only native probe confirmed DLL presence and the missing PNG/JPG
  commands. New registration detection reports incomplete, not ON.
- No actual Apply/Restore, association change, or native image-opening smoke
  test was performed on the user's Windows installation. Windows 10 execution
  was not tested on this PC.

## Delivered build

Native AOT x64 publish and Inno Setup 7.1.0 compilation succeeded on 12 September
2026. Delivery metadata was rechecked and recorded on 13 September 2026.
Stage: artifacts/publish/win-x64-20260912-222959-396.
Setup: artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe.
Version: 8.0.0.0. Company: Naufal Tech's Ltd. Signature: NotSigned.
Size: 38,159,096 bytes. SHA-256:
B32D19D04775DF9D3F84B0007639B6E8BA57AA0F7167D05A2498E21AC316C7B1.
All 91 published-icon assertions passed. The previous Setup at this path has
been replaced. No installer was executed during verification.

## After installing the updated build

Open Essential Windows Tweaks, Analyze/reload, then apply Legacy Windows Photo
Viewer. A previously incorrect ON may now read OFF until repaired. In Default
Apps choose Windows Photo Viewer for .png, .jpg and any other desired image types.
Alternatively use File Explorer > Open with > Choose another app. This final
default-app confirmation belongs to the user.

## Microsoft references

- [File types, OpenWithProgids and Shell association refresh](https://learn.microsoft.com/en-us/windows/win32/shell/fa-file-types)
- [Managing default applications and user ownership](https://learn.microsoft.com/en-us/windows/win32/shell/vista-managing-defaults)
- [Change default apps in Windows 10/11](https://support.microsoft.com/en-us/windows/apps/change-default-apps-in-windows)
