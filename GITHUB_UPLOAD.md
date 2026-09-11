# Uploading this source to GitHub

This guide uploads the **extracted repository contents**, not a ZIP file or a
published application folder. The export has not created a Git repository, commit,
remote, GitHub repository, or upload on your behalf.

## 1. Prepare the source folder

Extract the ZIP into a development directory you control. Open PowerShell in the
directory containing `README.md`, `.gitignore`, and
`Naufal Tech's Windows Powertoys.csproj`. Do not use the installed Program Files
directory or the entire old development folder with its caches and artifacts.

Keep `.gitignore` and `.gitattributes`—Windows Explorer can hide dotfiles.
Review `README.md`, `SOURCE_PACKAGE.md`, and the code before sharing. Choose the
repository's visibility and intended license yourself; no license was assigned
by this export. Do not upload credentials, certificates/private keys, crash
dumps, machine reports, or personal backup files.

## 2. Create an empty repository on GitHub

Create a repository with a name such as `Naufal-Windows-Powertoys`. For this
existing-source workflow, leave GitHub's automatic README, `.gitignore`, and
license initialization unchecked so they do not conflict with the local files.
Choose Public or Private deliberately, then copy the repository's HTTPS URL.

These steps follow [GitHub's existing local-code guide](https://docs.github.com/en/migrations/importing-source-code/using-the-command-line-to-import-source-code/adding-locally-hosted-code-to-github?platform=windows).

## 3. Review and commit locally

The following commands are for a fresh extracted folder with no `.git` directory:

```powershell
git init -b main
git add .
git status --short
git diff --cached --stat
```

Inspect the staged file list. It should include source, assets, tests, build files,
and documentation, not `bin/`, `obj/`, `artifacts/`, `.vs/`, executables, logs, or
keys. Stop and correct the selection if unexpected files appear. Then commit:

```powershell
git commit -m "Add Windows Powertoys 8.0.0 development source"
```

If Git asks for an identity, configure your intended name and email for this
repository, using your GitHub-provided private/noreply address if preferred.
No identity is automatically configured by this package.

## 4. Push

Replace `YOUR_USERNAME` and the repository name below with the URL you copied.
Do not paste a password or access token into the URL or into a tracked file.

```powershell
git remote add origin https://github.com/YOUR_USERNAME/Naufal-Windows-Powertoys.git
git remote -v
git push -u origin main
```

Complete Git's normal authentication flow. Do not force-push to an existing
repository. If your destination already contains commits, stop and integrate
with that repository normally instead of overwriting its history.

## 5. Continue development

- Work from your new Git checkout or deliberately copy reviewed changes into it;
  this export is a snapshot, not a live sync with the original development folder.
- Build and run regression checks using the root README.
- Review every future `git status`/staged diff before committing. `.gitignore`
  cannot remove secrets already committed; use GitHub's remediation guidance if
  exposure occurs.
- Keep generated Setup/app binaries out of the source tree. If you later choose
  to distribute a tested binary, attach it as a release asset separately.
- Update `CHANGELOG.md` with actual changes and verification, not intended work.
