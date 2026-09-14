# Catalog expansion — 14 September 2026

## Scope and interpretation

This is an implementation record for version **8.0.0.0**, not a claim of complete
runtime parity or certification on every Windows edition and OEM device.

Built-in Windows Apps now contains **140 logical entries**, sorted by canonical
English product name A–Z. All 131 requested list positions are represented:
old/new Teams share one entry; Bing News/Microsoft News share one entry; the
pre-existing apps are retained and matching identities merged. Clock/Alarms,
Bing/Search, Weather, Family, Cross Device/Mobile Devices and Widgets Experience
reuse stable IDs so previous audit identities are not discarded.
The missing Solitaire display name and punctuation in the submitted Skype,
Bing Health and Duolingo identifiers were normalized.

### Removal legend

These are conservative product judgments, not Microsoft endorsements.

- **Green — Recommended (26):** typically promotional games, shopping/onboarding
  apps or obsolete optional experiences; remove only if unused.
- **Yellow — Optional (79):** media, productivity, social or convenience apps;
  keep if their features are used.
- **Red — Not Recommended (35):** codecs, data-bearing apps and hardware,
  printing, battery, security or Xbox dependencies. Examples include HP Power
  Manager, Lenovo Vantage, HP Printer Control, Xbox Identity Provider, OneDrive,
  Sticky Notes and image/video extensions.

Nothing is preselected. Selecting a red entry adds an explicit warning with the
affected names to the confirmation. Yellow badges use black text in both themes.
The legend, reasons and BitLocker entry/title have translations in all 23 tables.
New detailed policy descriptions and technical diagnostic messages can still
fall back to English; the localization tests are not human linguistic review.

### Identity and recovery rules

The executable catalog is in `BuiltInAppsCatalog.cs` and
`BuiltInAppsExpansion.cs`. The supplied IDs include both complete package names
and legacy name fragments; fragments are **not** used as wildcard removal
expressions. Explicit aliases are compared against AppX Identity.Name, and
the exact observed full package identity is rechecked before removal.
Microsoft name aliases additionally require a Microsoft publisher-family suffix.
Third-party aliases use the explicit names and expose the observed family in
the preview and audit; the list is not a certificate audit of every OEM package.

Store apps are removed for the current Windows account only. No framework,
resource package, provisioned Windows image or another user's registration is
removed by this catalog. Windows Store and App Installer are not targets.
Classic Win32 variants of the listed OEM/Store apps are not automatically treated
as equivalent. OneDrive retains its separately confirmed user/machine scope.

Copilot's `XP9CXNGPPJ97XX` is a Store product ID, not an AppX name:
the catalog handles Microsoft.Copilot AppX and the exact current-user WinGet
Store product. The online lookup is deferred until that app is selected.
Unknown/failed lookup blocks that item and is never counted as absent.
WinGet source identity, exact product ID and current-user scope are checked.
Store installations and per-user Copilot removal use the validated same-account
standard-user token, with no elevated or different-user fallback.

Restore first checks a healthy current registration and local staged identities.
Only entries with an explicitly mapped Store product ID get automatic Store
installation. Other entries expose manual Store recovery/search; opening a page
does not count as restoration. Retired, paid, licensed, region-restricted or
device-specific apps may be unavailable. Restore cannot recreate deleted app
data. An absent uninstall target is neutral; failed inventory, permission
failure or unverifiable restoration is not converted into success.

## Requested policy mapping

| Requested capability | Location / merge |
| --- | --- |
| Telemetry and tracking | Existing Telemetry |
| Targeted ads | Existing Advertising ID / Personalized Experiences |
| Tips, suggestions and lock-screen tips | Windows Recommendations |
| Settings Home Microsoft 365 promotions | Windows Recommendations; supported Enterprise/Education editions only |
| Bing web search / Cortana | Windows Recommendations; Copilot removal remains in Windows AI |
| Store suggestions in Search | Existing Store Search child of Windows Recommendations |
| Location services and app access | Existing Location merged with App Location Access |
| Find My Device | New Find My Device row |
| Edge ads, shopping and new-tab news | New Edge Ads & Newsfeed row |
| Edge Copilot and AI | New Edge AI Features row |
| Copilot, Recall, Click to Do and related analysis | Existing Windows AI bundle |
| AI service automatic start, Notepad AI | Existing Windows AI bundle |
| Paint Cocreator / Generative Fill / Image Creator | New policy child merged into Windows AI |
| Phone Link in Start | Taskbar Buttons / Clutter |
| Xbox recording and Game Bar | Existing Xbox Components; this bundle also removes Xbox components, so review its warning |
| Brave AI, Wallet, Rewards, VPN, Talk and News | New Brave AI, Crypto & Extras row |
| Fast Startup | Existing Essential Windows Tweaks entry; OFF now writes and verifies DWORD zero, Restore remains separate |
| Automatic device encryption | New BitLocker Manager entry |

Advanced has **45 toggle rows**, plus its existing action entries, including
Built-in Windows Apps. Browser absence, known edition/build restrictions and
missing companion features are neutral unavailable states. Permission/read
errors remain errors. Supported registry read-back proves configuration, not
that every browser/Windows version implements every policy.

Paint uses the documented `...CurrentVersion\Policies\Paint` location with
edition/build gates. Its new snapshot is separate from the older Windows AI
bundle so existing AI backups remain usable. New policy plans capture all values
before the first write. Exact saved state is restored when present; otherwise
the explicit overrides are removed (Not configured). Every restored value is
read back; a partially OFF bundle is no longer accepted as complete restoration.
Managed policy refresh can reapply an organization's values.

Windows AI removal is now limited to the exact Microsoft Copilot identity for
this account, without wildcard name matching, forced uninstall or all-user
deprovisioning. The Store-product route replaces the old display-name command.
Recall read timeouts and unrelated DISM errors no longer imply absence; only
the unknown-feature error is accepted as absent. AI policy applicability varies;
Click to Do policy documentation currently identifies Insider applicability.
Recall history and removed app data may be deleted and are not backed up by this
tool. This is not a universal switch disabling all text/image analysis.

### BitLocker distinction

Apply writes only `HKLM\SYSTEM\CurrentControlSet\Control\BitLocker\PreventDeviceEncryption=1`.
It prevents future automatic device encryption; it does **not** decrypt already
encrypted volumes, delete recovery keys, or alter TPM settings. Existing Decrypt
remains a separate operation. The new entry has a high-risk warning and the
shared confirmation/progress/restore workflow. Restoring a missing backup removes
this override; it does not force encryption or bypass organizational policy.

## Verification boundary

- Functional tests use injected/fake backends and static wiring checks.
- Native current-account AppX inventory was read without uninstalling,
  registering, installing, changing settings or querying the online Store.
  That validates the reader on this PC, not every catalog identity.
- Localization tests cover table integrity and regression behavior, not
  complete translation of all new descriptive prose.
- Full native UI click/layout tests and live Apply/Restore of these additions
  have **not** been performed on the user's Windows installation.
- The read-only inventory test reported NU1900: NuGet vulnerability data could
  not be fetched. Its build and inventory probe completed; that warning is not
  a successful online dependency-vulnerability audit.
- Installer hashes and final build/test results are recorded in the delivery
  entry after packaging. Do not use an older installer as evidence of this batch.

## Sources checked

- [Microsoft WinGet list: exact identity and scope](https://learn.microsoft.com/en-us/windows/package-manager/winget/list)
- [Microsoft WinGet uninstall](https://learn.microsoft.com/en-us/windows/package-manager/winget/uninstall)
- [Microsoft WinGet source identity/export](https://learn.microsoft.com/en-us/windows/package-manager/winget/source)
- [Microsoft WinGet return codes](https://github.com/microsoft/winget-cli/blob/master/doc/windows/package-manager/winget/returnCodes.md)
- [Microsoft Copilot Store product](https://apps.microsoft.com/detail/xp9cxngppj97xx)
- [Microsoft WindowsAI policies and Paint applicability](https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-windowsai)
- [Microsoft Notepad management](https://learn.microsoft.com/en-us/windows/client-management/manage-notepad)
- [Microsoft Experience policies: Find My Device and consumer content](https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-experience)
- [Microsoft Privacy policies](https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-privacy)
- [Microsoft device encryption configuration](https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/oem-bitlocker)
- [Microsoft Edge policy catalog](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies)
- [Brave Group Policy](https://support.brave.app/hc/en-us/articles/360039248271-Group-Policy)
- [Microsoft Phone Link in Start](https://support.microsoft.com/en-us/windows/apps/phonelink/mobile-device-in-start-menu)

Legacy application-name examples and undocumented per-user preference mappings
were cross-checked against the [Win11Debloat source repository](https://github.com/Raphire/Win11Debloat).
Community preferences are not represented as guaranteed Microsoft policies;
future Windows/browser versions may ignore them. No remote script was executed.
