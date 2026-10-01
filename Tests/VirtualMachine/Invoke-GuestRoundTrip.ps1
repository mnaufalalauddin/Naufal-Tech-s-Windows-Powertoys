[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Profile','SharedSnapshot','Both','LegacyMigration','SecurityStorage')][string]$Audit,
    [ValidateSet('Prepare','Resume')][string]$Stage = 'Prepare'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($Audit -ne 'SecurityStorage' -and $Stage -ne 'Prepare') { throw 'Resume applies only to SecurityStorage.' }

# This launcher is deliberately bound to the user-authorized disposable clone.
# Never weaken this check to run the live audit on a physical PC.
$expectedUuid = [guid]'d0c5b1fe-5c91-4b69-9176-a330093074df'
$product = Get-CimInstance Win32_ComputerSystemProduct
$system = Get-CimInstance Win32_ComputerSystem
if ($system.Model -ne 'VirtualBox' -or [guid]$product.UUID -ne $expectedUuid) {
    throw 'Refusing: this is not the exact authorized disposable VirtualBox clone. No test started.'
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not ([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Open PowerShell as Administrator INSIDE the disposable guest. No test started.'
}
if (Get-Process -Name 'Naufal Windows Utility','Naufal Windows Powertoys' -ErrorAction SilentlyContinue) {
    throw 'Close Naufal Windows Utility before the live round-trip audit. No test started.'
}
$mediaRoot = [IO.Path]::GetFullPath($PSScriptRoot)
if ([IO.DriveInfo]::new([IO.Path]::GetPathRoot($mediaRoot)).DriveType -ne [IO.DriveType]::CDRom) {
    throw 'Launch this script directly from the read-only test CD/DVD, not from a writable folder.'
}
$harness = Join-Path $mediaRoot 'Harness\ProfileVerification.Tests.exe'
$manifest = Join-Path $mediaRoot 'SHA256SUMS.txt'
if (-not (Test-Path -LiteralPath $harness -PathType Leaf) -or -not (Test-Path -LiteralPath $manifest -PathType Leaf)) {
    throw 'Incomplete payload: harness and SHA256SUMS.txt are required.'
}
$verifiedPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($line in Get-Content -LiteralPath $manifest) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    if ($line -notmatch '^(?<Hash>[A-Fa-f0-9]{64})  (?<File>.+)$') { throw 'Invalid checksum manifest record.' }
    $expectedHash = $Matches.Hash
    $relative = $Matches.File
    if ([IO.Path]::IsPathRooted($relative) -or $relative.Contains(':') -or ($relative -split '[\\/]') -contains '..') { throw 'Manifest path is not a safe relative path.' }
    $file = [IO.Path]::GetFullPath((Join-Path $mediaRoot $relative))
    if (-not $file.StartsWith($mediaRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest path escapes the media root.' }
    if (-not $verifiedPaths.Add($file)) { throw 'Duplicate manifest path.' }
    if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $expectedHash) { throw "Payload hash mismatch: $relative" }
}
foreach ($required in @($harness, $PSCommandPath) + @(Get-ChildItem -LiteralPath (Join-Path $mediaRoot 'Harness') -File -Recurse | Select-Object -ExpandProperty FullName)) {
    if (-not $verifiedPaths.Contains($required)) { throw "Payload file was not covered by its checksum manifest: $required" }
}

Write-Warning 'LIVE GUEST TEST: Profile changes BCD/power/scheduling before intentional rollback. SharedSnapshot writes two HKCU settings using isolated backup storage. LegacyMigration reads production backup tags and rehearses migration on test-owned copies only. SecurityStorage disables/restores Print to PDF with retained payload and enables/restores eligible HVCI through unchanged safety gates; it stops for manual guest reboots. Failed/unknown phases require review or clone checkpoint recovery. No WIM deployment or host reboot is performed.'
$consent = Read-Host "Type RUN $Audit IN DISPOSABLE VM to continue"
if ($consent -cne "RUN $Audit IN DISPOSABLE VM") { throw 'Cancelled. No test started.' }

$base = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Naufal Windows Powertoys\VmValidation'
$fullBase = [IO.Path]::GetFullPath($base)
if ([IO.DriveInfo]::new([IO.Path]::GetPathRoot($fullBase)).DriveType -ne [IO.DriveType]::Fixed) { throw 'Reports require a fixed guest-local disk.' }
$ancestor = $fullBase
while ($ancestor) {
    if (Test-Path -LiteralPath $ancestor) {
        if ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Report path contains a reparse point.' }
    }
    $ancestor = [IO.Path]::GetDirectoryName($ancestor)
}
$runRoot = if ($Audit -eq 'SecurityStorage') {
    # Fixed location prevents accidentally replacing a pending/failed baseline with a new run.
    Join-Path $fullBase 'SecurityStorageAcceptance'
} else { Join-Path $fullBase ('Run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N')) }
if (Test-Path -LiteralPath $runRoot) {
    if ((Get-Item -LiteralPath $runRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Run root is a reparse point.' }
    if ($Stage -ne 'Resume') { throw 'Existing audit retained. Use Resume for a pending reboot stage, or review it; do not start over.' }
} elseif ($Stage -eq 'Resume') { throw 'No prepared acceptance run exists.' }
else { New-Item -ItemType Directory -Path $runRoot | Out-Null }
$tests = if ($Audit -eq 'Both') { @('SharedSnapshot','Profile') } else { @($Audit) }
foreach ($test in $tests) {
    # Recheck that the UI was not opened after consent; never test concurrently.
    if (Get-Process -Name 'Naufal Windows Utility','Naufal Windows Powertoys' -ErrorAction SilentlyContinue) { throw 'Application is now open; stop rather than run concurrently.' }
    $report = Join-Path $runRoot $test
    $arguments = if ($test -eq 'Profile') {
        @('--live-profile-audit','--expected-vm-uuid',$expectedUuid.ToString(),'--report-directory',$report,'--expected-sid',$identity.User.Value)
    } elseif ($test -eq 'LegacyMigration') {
        @('--vm-legacy-migration-audit','--expected-vm-uuid',$expectedUuid.ToString(),'--report-directory',$report)
    } elseif ($test -eq 'SecurityStorage') {
        @('--live-security-storage-audit','--audit-stage',$Stage,'--expected-vm-uuid',$expectedUuid.ToString(),'--report-directory',$report,'--expected-sid',$identity.User.Value)
    } else {
        @('--vm-shared-snapshot-audit','--expected-vm-uuid',$expectedUuid.ToString(),'--report-directory',$report)
    }
    Write-Host "Starting $test audit. Evidence: $report"
    & $harness @arguments
    if ($LASTEXITCODE -ne 0) { throw "$test audit failed (exit $LASTEXITCODE). STOP and review $report before any further test." }
}
Write-Host "Harnesses returned exit 0. This may be an awaiting-reboot stage, NOT a pass. Review the manifest and evidence: $runRoot"
if ($Audit -eq 'SecurityStorage') {
    $state = Get-Content -LiteralPath (Join-Path $runRoot 'SecurityStorage\security-storage-manifest.json') -Raw | ConvertFrom-Json
    Write-Host ('Acceptance phase: ' + $state.Phase)
    if ($state.Phase -like 'Await*Boot') { Write-Host 'Restart only this guest manually, then Resume. Never restart the host.' }
    else { Write-Host 'No further reboot requested by this audit. Skipped controls are NOT Apply/rollback passes. Preserve the evidence.' }
}
Write-Host 'No automatic reboot was requested. WIM deployment remains a separate, unexecuted test.'
