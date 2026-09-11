# Built-in Windows Apps — 2026-09-12

## Access and behavior

Advanced Windows Tweaks & De-Bloat → Built-in Windows Apps → **Review apps**.
This is an additional action; the existing 41 tweak definitions are unchanged.
The review window has 31 unchecked app rows, Select all, De-select all,
Analyze / reload, **Uninstall selected**, **Restore selected**, and an individual
Microsoft Store button for each app. Both batch actions require confirmation.
Missing apps remain selectable for restore. A read failure disables mutation;
it is not interpreted as an empty inventory. App names remain product names;
the new controls, scope, warnings and consent text have entries in all 23 languages.

Scope is the current process's Windows account, including when the program is
started with credentials for another account. It does not uninstall packages
from other accounts, remove provisioning, alter WindowsApps ACLs, remove app
frameworks/resources, remove Microsoft Store/App Installer, or uninstall classic
Win32/FoD Paint, Notepad, Quick Assist, or Teams. Thus Windows 10 builds with only
those classic variants correctly show their Store-app counterpart as absent.

Uninstall can delete application-local data and stop codec/media, clock, notes,
phone integration, assistance and other app functionality. Confirmation tells
the user to back up files first. The operation log is **not a data backup**.

Restore first checks for a healthy current-user registration. Otherwise it tries
native family-name registration from Windows' remaining staged payload. For the
25 entries with a verified product ID below, it can next reinstall through
WinGet's Microsoft Store source (exact ID, user scope, no interactive purchase,
no forced closure/hash bypass/reboot). Consent to Store/package agreements is
displayed before the user continues. The source export must match Microsoft's
documented HTTPS Store endpoint and REST type before a download starts.

The other **six entries** have local restore plus an explicit per-app Store
recovery button, not an invented Store ID or an unverified third-party download.
There is no promise that a retired, unlisted, region/device-restricted or
unlicensed package remains downloadable. Store pages can require manual action.
Opening a Store page is never counted as restoration: Analyze reloads the actual
inventory afterward. Restore reinstalls app functionality, not deleted personal
data or necessarily the exact previously installed app version.

## Identity / Store recovery map

All identities are exact package families. `8wekyb3d8bbwe` is the publisher suffix
unless explicitly shown. Teams accepts the two named package-family alternatives.
Product IDs link to the corresponding Microsoft Store listing checked during
implementation; links/availability can change independently of this application.

| App | Package name | Automatic Store product ID after local restore fails |
| --- | --- | --- |
| AV1 Video Extension | Microsoft.AV1VideoExtension | [9MVZQVXJBQ9V](https://apps.microsoft.com/detail/9mvzqvxjbq9v) |
| AVC Encoder Video Extension | Microsoft.AVCEncoderVideoExtension | [9PB0TRCNRHFX](https://apps.microsoft.com/detail/9pb0trcnrhfx) |
| Clock | Microsoft.WindowsAlarms | [9WZDNCRFJ3PR](https://apps.microsoft.com/detail/9wzdncrfj3pr) |
| Dev Home | Microsoft.Windows.DevHome | Local + manual Store recovery |
| Feedback Hub | Microsoft.WindowsFeedbackHub | [9NBLGGH4R32N](https://apps.microsoft.com/detail/9nblggh4r32n) |
| Get Help | Microsoft.GetHelp | Local + manual Store recovery |
| HEIF Image Extension | Microsoft.HEIFImageExtension | [9PMMSR1CGPWG](https://apps.microsoft.com/detail/9pmmsr1cgpwg) |
| HEVC OEM | Microsoft.HEVCVideoExtension | Local + manual Store/license recovery; paid plural package excluded |
| Media Player | Microsoft.ZuneMusic | [9WZDNCRFJ3PT](https://apps.microsoft.com/detail/9wzdncrfj3pt) |
| Microsoft Bing | Microsoft.BingSearch | [9NZBF4GT040C](https://apps.microsoft.com/detail/9nzbf4gt040c) |
| Microsoft Clipchamp | Clipchamp.Clipchamp_yxz26nhyzhsrt | [9P1J8S7CCWWT](https://apps.microsoft.com/detail/9p1j8s7ccwwt) |
| Microsoft Family | MicrosoftCorporationII.MicrosoftFamily | [9PDJDJS743XF](https://apps.microsoft.com/detail/9pdjdjs743xf) |
| Microsoft News | Microsoft.BingNews | [9WZDNCRFHVFW](https://apps.microsoft.com/detail/9wzdncrfhvfw) |
| Microsoft Teams | MSTeams / MicrosoftTeams | Local + manual Store recovery; classic Teams not targeted |
| Microsoft To Do | Microsoft.Todos | [9NBLGGH5R558](https://apps.microsoft.com/detail/9nblggh5r558) |
| Mobile Devices | MicrosoftWindows.CrossDevice_cw5n1h2txyewy | Local + manual Store recovery |
| Outlook for Windows | Microsoft.OutlookForWindows | [9NRX63209R7B](https://apps.microsoft.com/detail/9nrx63209r7b) |
| Paint | Microsoft.Paint | [9PCFS5B6T72H](https://apps.microsoft.com/detail/9pcfs5b6t72h) |
| Phone Link | Microsoft.YourPhone | Local + manual Store recovery |
| Photos | Microsoft.Windows.Photos | [9WZDNCRFJBH4](https://apps.microsoft.com/detail/9wzdncrfjbh4) |
| Power Automate | Microsoft.PowerAutomateDesktop | [9NFTCH6J7FHV](https://apps.microsoft.com/detail/9nftch6j7fhv) |
| Quick Assist | MicrosoftCorporationII.QuickAssist | [9P7BP5VNWKX5](https://apps.microsoft.com/detail/9p7bp5vnwkx5) |
| Solitaire & Casual Games | Microsoft.MicrosoftSolitaireCollection | [9WZDNCRFHWD2](https://apps.microsoft.com/detail/9wzdncrfhwd2) |
| Sound Recorder | Microsoft.WindowsSoundRecorder | [9WZDNCRFHWKN](https://apps.microsoft.com/detail/9wzdncrfhwkn) |
| Start Experiences App | Microsoft.StartExperiencesApp | [9PC1H9VN18CM](https://apps.microsoft.com/detail/9pc1h9vn18cm); NOT StartMenuExperienceHost or Client.CBS |
| Sticky Notes | Microsoft.MicrosoftStickyNotes | [9NBLGGH4QGHW](https://apps.microsoft.com/detail/9nblggh4qghw) |
| VP9 Video Extensions | Microsoft.VP9VideoExtensions | [9N4D0MSMP0PT](https://apps.microsoft.com/detail/9n4d0msmp0pt) |
| Weather | Microsoft.BingWeather | [9WZDNCRFJ3Q2](https://apps.microsoft.com/detail/9wzdncrfj3q2) |
| Web Media Extensions | Microsoft.WebMediaExtensions | [9N5TDP8VCMHS](https://apps.microsoft.com/detail/9n5tdp8vcmhs) |
| WebP Image Extension | Microsoft.WebpImageExtension | [9PG2DK419DRG](https://apps.microsoft.com/detail/9pg2dk419drg) |
| Windows Notepad | Microsoft.WindowsNotepad | [9MSMLRH6LZF3](https://apps.microsoft.com/detail/9msmlrh6lzf3) |

## Progress, audit and failure semantics

Each batch uses the existing shared task admission and a separate
CatalogProgressWindow. Per-app native percentages are Windows-reported; unknown
download/local-registration progress stays indeterminate rather than fabricated.
Green indicates work/success, red indicates failure. A package absent before
uninstall is neutral UNAVAILABLE. Failed permission/readback/license/download
checks are not mislabeled as ordinary absence or verified success.

Each completed uninstall requires a new inventory showing no approved family.
Each completed restore requires a new inventory showing an approved family whose
Windows package status passes VerifyIsOK. This does not certify every app feature.
A timeout stops the batch and the shared deployment gate remains held until the
underlying Windows operation really finishes. Later rows remain unstarted.

Before mutation, an inventory and selection log is saved to
`%LOCALAPPDATA%\Naufal Windows Powertoys\Logs\BuiltInApps`.
An audit-write failure stops the batch. Protected/nonremovable packages are left
to Windows' normal deployment checks; no protection bypass is attempted.

## Verification and limits

- Native AOT publish and Setup compile completed. Stage:
  `artifacts/publish/win-x64-20260912-000505-037`; EXE 19,839,488 bytes,
  SHA256 `F3F55073358689325C0778F8DBADDB8AAF5260DD5E1C66F400E8AE125BC3A26B`.
  Setup `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`,
  38,052,032 bytes, SHA256
  `94DF7740E1C1A4C0E89A05C2D8AB10432BBF8C6AD36B4280046648CD204442DA`.
  Version 8.0.0.0, company Naufal Tech's Ltd., both unsigned.
  Completed-stage payload hashes and 101 icon assertions pass.
- Debug build: 0 errors / 0 warnings.
- 2,655 functional regression assertions, including 471 new app tests.
- 96,099 localization assertions across 23 languages (coverage/round-trip tests,
  not a professional linguistic review).
- Existing static checks: 80 catalog/routing, 18 AppData, 12 installer-location,
  10 publish-stage checks passed.
- Production read-only inventory probe runs without invoking any mutation. In
  this session it ran as `codexsandboxoffline`, not Naufal, and returned zero
  installed targets for that isolated account. This is not proof of absence in
  Naufal's real account. The earlier actual-user PowerShell inventory matched
  all 31 requested app identities; the native normal-user path still needs a
  manual non-sandbox smoke test.
- The auxiliary read-probe project emitted NU1900 because NuGet vulnerability
  metadata was unreachable; compilation and execution succeeded. The application's
  Debug build and Native AOT publish emitted no such warning. This is not a
  certification that dependency vulnerability information is current.
- No personal app uninstall/install/re-registration, native UI interaction, or
  installer execution was used as a test. Windows 10 installation/restore and
  Store availability for all devices/regions remain untested.
- The separate earlier report of blank native UI text remains unresolved. This
  change does not claim full original-program parity or fix that rendering bug.

## Microsoft references

- [RemovePackageAsync](https://learn.microsoft.com/en-us/uwp/api/windows.management.deployment.packagemanager.removepackageasync?view=winrt-26100)
- [RegisterPackageByFamilyNameAsync](https://learn.microsoft.com/en-us/uwp/api/windows.management.deployment.packagemanager.registerpackagebyfamilynameasync?view=winrt-26100)
- [Launching Microsoft Store, ProductId and PFN links](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-store-app)
- [WinGet install: exact ID, scope, source and agreements](https://learn.microsoft.com/en-us/windows/package-manager/winget/install)
- [WinGet source export and official Store endpoint](https://learn.microsoft.com/en-us/windows/package-manager/winget/source)
- [Inbox app removal and app-data implications](https://learn.microsoft.com/en-us/windows/configuration/policy-based-inbox-app-removal/policy-based-inbox-app-removal)
