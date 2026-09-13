# Development delivery requirements

## Setup installer after changes

User requirement recorded on 12 September 2026: always deliver a Setup Installer
`.exe` whenever development changes are made to this application.

- Before handing off each completed batch of development changes, run the
  relevant checks, publish the updated Release x64 Native AOT application, and
  build its Setup EXE with `build-installer.ps1`.
- Use the normal publish-and-package path after application source, resources,
  project settings, or dependency changes. Do not package a stale publish stage.
- `-SkipPublish` is only for packaging a complete hash-verified stage whose
  application inputs are unchanged, such as retrying packaging after installing
  Inno Setup or making documentation/installer-only changes.
- Verify the generated installer path, version, size, and SHA-256. Link the
  actual generated Setup EXE in the final handoff and disclose unsigned status.
- If publishing or packaging fails, report that blocker explicitly. Never
  describe a pre-existing installer as containing the current changes.
- Building an installer does not authorize running it, installing the app,
  changing Windows settings, committing, pushing, or uploading a release.
- Read-only investigations with no development changes do not require a new
  installer. Keep the application version unless the user requests a change.
