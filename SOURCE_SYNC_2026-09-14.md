# Source synchronization — 14 September 2026

This source-only update follows `6699787` on the existing `main` branch. It
imports the delivered **8.0.0.0** catalog expansion plus the subsequently reported
Copilot source-agreement fix, without rewriting Git history.

## Included

- 140 A–Z app entries, merged duplicate identities, recommendation badges and
  confirmation warnings; exact current-user Copilot Store-product handling.
- Privacy/browser/AI/location policy additions and merges, the BitLocker
  automatic-device-encryption option, and explicit Fast Startup OFF behavior.
- More precise restoration, inventory and Recall error verification.
- New regression coverage, 23-language legend/BitLocker labels, changelog and
  detailed implementation/delivery records.
- Dedicated Store-consent confirmation for Windows AI and Copilot app operations;
  operation-scoped acceptance, cancellation and concurrent-task isolation, an
  actionable agreement error, and all 23 translations for the new prompt.

See [the implementation record](CATALOG_EXPANSION_2026-09-14.md) for applicability,
recovery, translation and runtime-testing limitations. Counts of test assertions
are not counts of independently verified user features.

## Export handling

Application, test, project, build/configuration and asset files were synchronized
from development. Existing repository-only build/bootstrap files and sanitized
historical reports were retained. The optional private-reference auditor keeps
its explicit input-directory parameter. The changelog's private developer path
was replaced with a placeholder. No software license was assigned or changed.

The upload excludes installers, compiled applications, DLL/PDB files, build and
package caches, IDE state, private reference inputs, runtime backups, logs,
signing keys and credentials. Filename and common-secret-pattern checks are
precautions, not a comprehensive security certification.

## Verification on the GitHub checkout

- Functional regression suite: **4,382 assertions passed**.
- Localization suite: **99,732 assertions passed**, across 23 language tables.
- Main WinUI Debug x64 compiled with **0 warnings and 0 errors**.
- Static checks passed: catalog routing/progress 80, monitoring 26, OneDrive/
  Game Mode 13, AppData routing 18, installer location 12, publish staging 10,
  report export 10 and source icons 73 assertions. No interactive UI test or
  live Windows mutation was performed.
- **221 runtime/build/test/asset inputs** matched development by SHA-256.
  Repository-only documentation and historical privacy sanitization are
  intentional differences. **271 public files** passed the forbidden-path and
  common-credential-pattern screening before staging.

The fresh Native AOT/Setup was rebuilt after the source-agreement fix. Its hashes
and unsigned status are in [the latest delivery record](COPILOT_STORE_CONSENT_2026-09-14.md).
The earlier [catalog delivery record](CATALOG_EXPANSION_DELIVERY_2026-09-14.md)
is retained as history. No application/installer was run, installed or uploaded
as part of this source synchronization.
The public source commit is not a binary release and does not certify all Windows
10/11 configurations or all native Apply/Restore paths.
