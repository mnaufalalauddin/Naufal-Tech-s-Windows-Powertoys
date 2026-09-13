# NTFS Performance Options — readback fix

Date: 12 September 2026.

## Observed failure and cause

The user screenshot reported a failed Apply with last-access DWORD
`-2147483647` and 8dot3 DWORD `1`. Read-only registry inspection confirmed
`0x80000001` and `0x00000001`. Windows' own read-only queries returned:

```text
DisableLastAccess = 1 (User Managed, Last Access Time Updates DISABLED)
The registry state is: 1 (8dot3 name creation is DISABLED on all volumes)
```

The application compared the signed registry integer directly with `1` in two
places: the catalog status reader and Apply's post-command verifier. It therefore
rejected a valid new-format DWORD and reported OFF/partial/failed despite the
requested configuration being present. This was a verification bug, not evidence
of filesystem damage or a reason to apply more registry changes.

## Implementation

- `NtfsPerformanceVerification.cs` decodes only known last-access modes 0–3,
  with or without the `0x80000000` new-semantics bit, from actual DWORD values.
- Both Essential catalog status and Apply use that shared interpretation.
- The preset still requires **mode 1**, user-managed disabled. System-managed
  mode 3 is not silently treated as the same requested policy.
- Unknown bits, non-DWORD values, nonzero command exits, timeouts and wrong
  8dot3 state do not pass. The high-bit rule does not apply to unrelated settings.
- Diagnostics display logical mode and raw hexadecimal value, not a misleading
  signed decimal. Failures include command output and exit/timeout details.
- Restore still replays and verifies the exact original values/types. Snapshot
  handling is intentionally not normalized or migrated by this fix.
- A successful Apply verifies configuration, not every live filesystem effect;
  it tells the user to restart Windows for all changes to take effect.

## Verification

- Debug build: 0 warnings, 0 errors.
- Functional regression: 2,899 assertions passed (150 new NTFS assertions).
- Read-only production-decoder probe:
  `NtfsDisableLastAccessUpdate=1 [DWORD=0x80000001]; NtfsDisable8dot3NameCreation=0x00000001`;
  `Applied preset readback: True`.
- Tests cover all eight recognized encodings, exact requested-mode semantics,
  invalid types/bits, strict 8dot3 matching, failure/timeout propagation and
  exact high-bit snapshot preservation.
- No Apply/Restore operation, fsutil set, snapshot deletion or reboot was run.
  This is not an end-to-end mutation or visual UI certification.
- The 32-app catalog, including Microsoft OneDrive, is retained unchanged.
- Localization: 96,927 assertions across 23 languages passed.
- Catalog/routing/progress: 80 static assertions passed.
- Native AOT publish and Setup compile succeeded. Updated stage:
  `artifacts/publish/win-x64-20260912-154700-908`; version 8.0.0.0.
  All 91 published-icon checks passed; no app or installer was launched.
- Rebuilt Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`,
  38,067,260 bytes, SHA256
  `4554214F3BB79332E2BEAF9AF6A4891681DDB2D18FD7967436ED4BF42EA30BE7`.
  This replaces the previous same-named installer; prior app stages are retained.

## Microsoft references

- [Microsoft IIS Support: last-access new semantics and 0x80000000–0x80000003](https://techcommunity.microsoft.com/blog/iis-support-blog/iis-logs-current-active-logs-timestamp-shows-000000/786821).
- [Microsoft Learn: fsutil behavior, registry targets and restart requirements](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/fsutil-behavior).

The first reference identifies the high bit as new semantics and distinguishes
user-managed/system-managed modes. The second documents the fsutil operations
and their effect on NTFS last-access and short-name creation configuration.
