# Localization audit and repair — 7 September 2026

Follow-up for previous-language leftovers: see
`LANGUAGE_SWITCH_FIX_2026-09-07.md` for the later lifetime/cleanup repair, expanded
tests, current output hashes, and the Computer Use verification limit. Build
outputs and assertion counts below describe the earlier audit checkpoint.

## Result and scope

All 23 configured language tables were inspected and exercised. This is **not**
a claim that every native feature description, diagnostic message, or screen is
fully localized. The audit identified both engine defects and resource gaps;
the shared engine and the resource groups below are repaired in this build.
Remaining gaps are listed explicitly below and in the machine-readable report.

Languages: English, Bahasa Indonesia, Deutsch, Français, العربية, Tagalog,
Tiếng Việt, 简体中文, 繁體中文, ไทย, Русский, Українська, Português, 日本語,
한국어, اردو, தமிழ், हिन्दी, Bahasa Melayu, Basa Jawa, Basa Bali, Svenska, Español.

## Findings fixed

1. **Wizard limited to two languages.** Removed Indonesian/English branches in
   MainWindow. Captions now retain their canonical source and use the same
   23-language engine as other windows. Skip/suppression behavior is unchanged.
2. **Live updates reverting to English.** Authored display properties have
   guarded dependency-property callbacks. New authored children are discovered
   with a coalesced layout observer. Progress, statuses and captions update
   without replacing backend operation identifiers.
3. **Repeated switches losing the original caption.** UiLocalizedValue tracks
   canonical source separately from its last rendering. ToolWindow headings
   now receive canonical text; native window titles are rendered separately.
4. **WinUI template text being rewritten.** Localization walks authored content
   and authored item containers, not generated template TextBlocks. This avoids
   capturing an already-translated template caption or replacing its binding.
5. **Popup/accessibility gaps.** Added translation of tooltips, accessible names,
   help text, toggle captions, placeholders, context menus and scale flyouts.
6. **Date hardcoded to English.** Date formatting uses the selected culture.
   Arabic/Urdu use RTL layout with isolation of Latin/numeric template arguments.
   Raw read-only TextBox logs remain LTR. Jawa/Bali keep the existing id-ID date
   culture fallback; their app text still has separate language tables.
7. **Resource gaps.** Added 23-language matrices for core task verbs, progress,
   wizard descriptions/tasks, key safety warnings, dashboard descriptions,
   27 simplified option titles, and 22 Explorer/Update/Store repair stage names.
   Main UI route captions are covered, including Legacy Windows Panels.
8. **Dynamic templates.** Added bounded matching for numeric progress, elapsed
   time, scale labels, selection summaries, numbered stages and action titles.
   Counts and placeholder argument ordering are preserved; no word-by-word
   rewriting of arbitrary commands or error text is performed.
9. **Misleading legacy resource pair.** Removed an unused description whose
   English snapshot-restore promise conflicted with a different non-English
   original-defaults description. No restoration semantics were changed.
10. **Audit missed new resources.** The static parity reader now merges the
    editable UTF-8 resource matrices and aliases. The localization harness also
    inventories interpolated strings instead of inspecting only literals.

The red LIVE GAMING STATUS indicator, percentages, product identity, raw paths,
package IDs, HRESULTs and hardware identifiers are preserved.

## Evidence

| Check | Result |
| --- | --- |
| Configured languages | 23 |
| Baseline merged keys per language | 396 |
| Current merged keys per language | 573 |
| Missing English-reference keys per language | 0 |
| Empty translations | 0 |
| Named/indexed placeholder mismatches | 0 |
| Localization assertions | 80,676 passed |
| Existing functional regression assertions | 914 passed |
| Publish-stage assertions | 5 passed |
| Debug compilation | 0 errors, 0 warnings |
| Native AOT publish / Inno Setup | Successful |

Assertions include all-table key and placeholder checks, language round trips,
dynamic source updates, culture/RTL behavior, 8 scale values, preservation of
technical values, safety labels, 20 Main UI route keys and source integration
contracts. The assertion count is **not** a count of screens visually tested.

No app, installer, repair, Apply, Restore, driver installation, registry write,
or service operation was executed for this audit. Native WinUI rendering,
wrapping/clipping at all scales, and native-speaker linguistic review are not
certified by these tests. The earlier reported native 0xc0000005 crash is still
unresolved; a successful compile does not establish that it has been fixed.

## Remaining localization work — do not label this release 100% translated

The source inventory contains 2,869 literal candidates and 683 interpolated
candidates. It includes technical constants and diagnostics, so these totals
must not be used as a translation-completion percentage.

There are 175 direct display-assignment candidates, of which 48 still have at
least one unchanged-English result. That smaller list also includes intentional
acronyms (HAGS/MMCSS), date/time samples/format strings and shared wording in
closely related languages. It requires triage, not blind translation.

Known genuine gaps include:

- New detailed Essential/Gaming/Advanced option descriptions, especially vendor
  service side effects; many no longer match the old reference catalog keys.
- Longer Apply/Restore confirmations, read-only mode instructions and result
  summaries; mixed-English fallback is still possible for these messages.
- Some MSI, GPU, BitLocker, runtime and Process Manager guidance and messages.
- Additional simplified option names and parameterized backend-stage details.
- Visual clipping/RTL behavior in actual native windows, all 8 scales, and
  linguistic review by native speakers (especially Jawa/Bali terminology).

Raw logs, paths, registry/BCD identifiers, exception text and vendor output stay
original intentionally. They must not be confused with untranslated app-authored
guidance. Never translate model IDs or infer command arguments from UI captions.

Machine-readable locations and missing-language lists:
`artifacts/localization-audit/coverage.json`.
Baseline: `artifacts/localization-audit/baseline.json`.

## Reproduce safely

```powershell
dotnet restore Tests\Localization\Localization.Tests.csproj --configfile Tests\ProfileVerification\NuGet.Config
dotnet run --project Tests\Localization\Localization.Tests.csproj --no-restore
dotnet run --project Tests\ProfileVerification\ProfileVerification.Tests.csproj -c Debug --no-restore
.\Tests\ParityAudit\Test-PublishStage.ps1
.\Tests\ParityAudit\Inspect-StaticParity.ps1
```

The localization project uses Roslyn shipped with the local .NET SDK, not a
downloaded translation service. Its default run generates the audit report.
`-- --test-only` runs assertions without the inventory; test failures return a
nonzero console exit rather than an unhandled Application Error dialog.
The original PS1/executable must not be executed to generate this evidence.

## Output

- Completed stage: `artifacts/publish/win-x64-20260907-065449-167`.
- App: `Naufal Windows Powertoys.exe`, version 7.8.0.0.
- Publisher metadata: `Naufal Tech's Softwares`.
- EXE SHA-256: `ED880D7984983F976A12EA778F4447159C073829D5C98A5B15DDB3EF48022D57`.
- Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-7.8.0-x64.exe`.
- Setup size: 36,649,955 bytes.
- Setup SHA-256: `67AF4C3B77CD26C7DBE76F2C46DFF594733522E0439580B706873F5CBD30CDA2`.
- EXE and installer remain unsigned. Stage completion/EXE hash verified.
- Same-name Setup output replaced its previous build; older publish stages
  remain available. No source/reference files or user backups were deleted.
