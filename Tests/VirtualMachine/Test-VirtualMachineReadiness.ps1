[CmdletBinding()]
param(
    [Parameter(Mandatory)][guid]$VmId,
    [Parameter(Mandatory)][string]$OwnedDirectory,
    [string]$VBoxManage = 'C:\Program Files\Oracle\VirtualBox\VBoxManage.exe'
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\VirtualMachine.Guards.ps1"
if ($VmId -eq [guid]::Empty) { throw 'An exact disposable VM UUID is required.' }
if (-not (Test-Path -LiteralPath $VBoxManage -PathType Leaf)) { throw 'VirtualBox command-line tool not found.' }
$lines = & $VBoxManage showvminfo $VmId.ToString() --machinereadable
if ($LASTEXITCODE -ne 0) { throw 'VM inspection failed; no changes attempted.' }
$info = ConvertFrom-VBoxMachineInfo $lines
$issues = @(Get-VmIsolationBlockers $info $VmId $OwnedDirectory)
$owned = [IO.Path]::GetFullPath($OwnedDirectory).TrimEnd('\') + '\'
$disks = @()
if ($issues.Count -eq 0) {
    [xml]$configuration = Get-Content -LiteralPath $info.CfgFile -Raw
    foreach ($attachment in $configuration.SelectNodes('//*[local-name()="AttachedDevice"][@type="HardDisk"]/*[local-name()="Image"]')) {
        $diskId = [guid]$attachment.uuid.Trim('{}')
        $visited = [Collections.Generic.HashSet[guid]]::new()
        do {
            if (-not $visited.Add($diskId) -or $visited.Count -gt 32) { throw 'Invalid or excessively deep disk parent chain.' }
            $medium = & $VBoxManage showmediuminfo disk $diskId.ToString()
            if ($LASTEXITCODE -ne 0) { throw 'Attached writable disk inspection failed.' }
            $disk = @{}
            foreach ($line in $medium) {
                if ($line -match '^(?<Key>[^:]+):\s*(?<Value>.*)$') { $disk[$Matches.Key.Trim().Replace(' ','')] = $Matches.Value.Trim() }
            }
            if (-not $disk.Location -or -not ([IO.Path]::GetFullPath($disk.Location)).StartsWith($owned, [StringComparison]::OrdinalIgnoreCase)) { $issues += 'Writable disk or snapshot parent is outside the disposable directory.' }
            if ($disk.Storageformat -ne 'VDI' -or $disk.Type -notin @('normal (base)', 'normal (differencing)')) { $issues += 'Disk is not a verified normal VDI.' }
            if ($disk.ParentUUID -eq 'base') { break }
            $diskId = [guid]$disk.ParentUUID
        } while ($true)
        $disks += $disk
    }
}
if ($disks.Count -eq 0) { $issues += 'No independent writable disk was verified.' }
[pscustomobject]@{
    TimeUtc = [DateTime]::UtcNow.ToString('O'); VmId = $VmId; Name = $info.name
    State = $info.VMState; IndependentDisks = $disks.Count
    IsolationPreflightPassed = ($issues.Count -eq 0); Blockers = $issues
    ApplyRollback = 'NOT RUN'; WimDeployment = 'NOT RUN'
    Note = 'Read-only inspection, not permission to boot or proof that Windows boots.'
} | ConvertTo-Json -Depth 5
if ($issues.Count -ne 0) { exit 2 }
