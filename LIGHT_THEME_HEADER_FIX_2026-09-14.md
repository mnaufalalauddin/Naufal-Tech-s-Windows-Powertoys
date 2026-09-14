# Light Mode header contrast fix — 14 September 2026

> This is the earlier header-only checkpoint. Its button coverage was incomplete.
> The subsequent [Dark Mode button follow-up](DARK_THEME_BUTTON_FIX_2026-09-14.md)
> supersedes its style-handling implementation and installer artifact.

## Report and reproduction

The user reported nearly white clock, Languages label and selected language text
against the Light Mode header. In the native backend-free header host, starting
with saved Dark preferences and switching to Light reproduced a **1.04:1** clock
contrast ratio before the fix (the test requires at least 4.5:1).

## Root cause and correction

`UiDisplaySettings` captured the effective foreground of every TextBlock/Control
and later assigned it as a local value. An inherited/default white foreground
captured in Dark Mode consequently overrode WinUI's Light theme foreground.

The baseline now distinguishes authored foregrounds from default/inherited ones.
Only local values or explicit Style/BasedOn setters are remapped. Default and
inherited foregrounds remain owned by WinUI's theme resources. Explicit white
labels on colored primary buttons remain white; authored muted colors still
follow the existing palette mapping.

No registry, service, app-removal or repair behavior was changed.

References:
- [Dependency properties and value precedence](https://learn.microsoft.com/en-us/windows/apps/develop/platform/xaml/dependency-properties-overview)
- [XAML theme resources](https://learn.microsoft.com/en-us/windows/apps/develop/platform/xaml/xaml-theme-resources)
- [DependencyProperty.UnsetValue](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.dependencyproperty.unsetvalue?view=windows-app-sdk-2.0)

## Verification

- Saved Dark / startup scale 100: **11,053 assertions**, 512 layout cases and
  8 flyout checks passed; minimum measured header contrast **16.61:1**.
- Saved Light / startup scale 25: **11,054 assertions**, 512 layout cases and
  8 flyout checks passed; minimum measured header contrast **16.61:1**.
- Coverage includes Light/Dark, English/German/Indonesian/Arabic, four window
  widths, eight scales in both directions, header bounds and native hit tests.
- Native checks include clock, Languages label, selector/selected-item foregrounds,
  absence of frozen local foregrounds, explicit primary-button white, and date color.
- Functional regression: **4,382 assertions passed**.
- Localization regression: **99,732 assertions passed** across 23 catalogs.
- Debug x64 built with zero errors and warnings.
- Release x64 Native AOT publication and Inno Setup compilation succeeded.
- Publish-time icon audit: **91 assertions passed**.
- The completed publish stage and both executable hashes were verified.

These are code/native-host checks, not a screenshot inspection of the final
elevated production executable. No installer, repair, tweak or uninstall was
run on the user's Windows installation. Other historical blank-text/native-crash
reports are not declared resolved by this specific header fix.

## Generated artifacts

Company metadata: **Naufal Tech's Ltd.**
File version: **8.0.0.0**
Authenticode: **NotSigned** for application and Setup.

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `artifacts/publish/win-x64-20260914-151859-248/Naufal Windows Powertoys.exe` | 20,518,400 | `F723C4975116D148662433A5576F409C2F1922650FAD912B1840FE8A381D2868` |
| `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe` | 38,192,044 | `9232175ED721A33BC0EC5140B5609B5D2C70EE17DA1A14611DCD4C014C264D5A` |

These identify this local build, not an uploaded GitHub release. The published
v8.0.0.0 installer checked on this date predates this fix. Source and release
assets must be tracked independently even when their version strings match.

## README presentation

The README now uses the existing NT branding, a concise badge/navigation header,
the user's unmodified Dark Mode dashboard screenshot, feature and recommendation
tables, and separate build/test documentation. The original low-contrast Light
screenshot is deliberately not presented as a screenshot of the corrected build.
No sponsors, donation links, license grant or release upload were invented.
