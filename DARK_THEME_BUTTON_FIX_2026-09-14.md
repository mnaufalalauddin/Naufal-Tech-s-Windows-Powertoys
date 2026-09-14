# Dark Mode button contrast follow-up — 14 September 2026

## User report and scope

After the first Light header fix, the user reported dark text on neutral buttons
in Dark Mode: Task Monitoring, Exit, repair/system menus, profile buttons,
MSI Mode, legacy panels and header controls.

The previous regression suite checked the header and one colored button, not
all neutral buttons. The expanded pre-fix native test reproduced **1.25:1**
contrast on the theme button. The previous passing header results therefore
were not sufficient evidence of button readability.

## Correction

- The shared `ReferenceButtonStyle` now obtains foreground, background and border
  from matching Light/Dark/Default **ThemeResource** entries. Its flat geometry
  and colored style variants are preserved.
- The manual palette mapper only remaps explicitly local foreground colors.
  It no longer tries to identify app-owned foregrounds by inspecting
  `Style.Setters` and `BasedOn`. Implicit styles were not covered reliably.
- Button style-owned backgrounds and borders are not replaced with local
  baseline values. This preserves native theme refresh and subsequent
  primary/neutral profile-style changes.
- Explicit local colors in dynamically constructed catalog rows still use
  the existing canonical palette mapper. Theme-owned header text remains free
  of local overrides, retaining the Light header correction.

Microsoft describes ThemeResource as re-evaluating when the active theme changes:
[ThemeResource markup extension](https://learn.microsoft.com/en-us/windows/apps/develop/platform/xaml/themeresource-markup-extension).

No tweak, repair, service or app-uninstall behavior was changed.

## Verification evidence

| Check | Result |
| --- | --- |
| Native AOT, saved Dark / scale 100 | 70,627 assertions; 512 layout cases; 8 flyouts — passed |
| Native AOT, saved Light / scale 25 | 70,628 assertions; 512 layout cases; 8 flyouts — passed |
| Minimum measured header contrast | 16.61:1 in both native runs |
| Minimum measured enabled button foreground/background contrast | 4.61:1 in both native runs; rendered label checks also require at least 4.5:1 |
| Functional regression | 4,382 assertions passed |
| Localization regression | 99,732 assertions across 23 catalogs passed |
| Static button/routing/progress checks | 92 assertions passed |
| Publish-time icon audit | 91 assertions passed |
| Main application Debug x64 | Zero errors and warnings |
| Main application Release x64 Native AOT + Setup | Published and compiled successfully; completed stage hash-verified |

The matrix covers every one of the **28 enabled authored Main UI buttons**,
four window widths, eight scale choices in both directions, Light/Dark and
English/German/Indonesian/Arabic. It checks effective brushes and rendered
TextBlock labels. Disabled buttons are not judged by enabled-text contrast.

Additional tests create catalog buttons in a separate, backend-free window:
implicit styles, explicit shared styles, explicit local colors, theme round
trips, re-enabling and primary/neutral style changes. Tests ensure style-owned
brush properties are not frozen into local overrides.

The host's Native AOT publish explicitly includes its linked XBF files and PRI
index. The test catalog uses real Border/Button/brush instances and no system
services. Debug-only success is no longer the sole native-UI evidence.

## Visual review and limits

Dark and Light PNGs were rendered with RenderTargetBitmap from the Native AOT
test host and visually inspected. Neutral button text is light in Dark and dark
in Light; header text remains readable in both. The host uses the actual main
XAML and application styles but deliberately has no live hardware, first-run,
repair or graph backend; initializing labels and a static clock are expected.

These previews are not product screenshots and were not substituted into the
README. They live beside the test executable as `theme-preview-dark.png` and
`theme-preview-light.png`, under ignored build output.

No production installer was executed. No Windows tweak, app uninstall, repair,
registry/service change, or production Apply/Restore action was performed.
This fix does not certify all historical blank-text reports, UI clipping at
every scale, or complete Windows 10/11 behavioral parity.

## Fresh local delivery

Version: **8.0.0.0**
Company: **Naufal Tech's Ltd.**
Application and installer Authenticode status: **NotSigned**.

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `artifacts/publish/win-x64-20260914-155844-425/Naufal Windows Powertoys.exe` | 20,460,544 | `B6F0939319016C9685BDF91A5A4E49391A50F018762F11894462816040C24159` |
| `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe` | 38,229,427 | `A5F7DF3AA426E922A05DB6A657D2CB0A4F3874EDB8997D52FE2859C61FA54644` |

This supersedes the earlier Light-only build with Setup SHA-256 beginning
`9232175E`. The filename/version stays the same; the hash identifies the new
build. No commit, push or release upload was performed by this follow-up.
