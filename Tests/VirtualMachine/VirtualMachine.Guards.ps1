Set-StrictMode -Version Latest

function ConvertFrom-VBoxMachineInfo {
    param([Parameter(Mandatory)][string[]]$Lines)
    $result = @{}
    foreach ($line in $Lines) {
        if ($line -match '^"?(?<Key>[^"=]+)"?=(?<Value>.*)$') {
            $value = $Matches.Value.Trim('"').Replace('\\', '\')
            $result[$Matches.Key] = $value
        }
    }
    return $result
}

function Get-VmIsolationBlockers {
    param([Parameter(Mandatory)][hashtable]$Info,
          [Parameter(Mandatory)][guid]$ExpectedId,
          [Parameter(Mandatory)][string]$OwnedDirectory)
    $issues = [Collections.Generic.List[string]]::new()
    if ($ExpectedId -eq [guid]::Empty -or $Info.UUID -ne $ExpectedId.ToString()) { $issues.Add('VM identity mismatch.') }
    if ($Info.VMState -ne 'poweroff') { $issues.Add('VM must be powered off for pre-boot validation.') }
    if (-not $Info.name -or -not $Info.name.StartsWith('NWU-Disposable-', [StringComparison]::Ordinal)) { $issues.Add('Not a dedicated disposable validation VM.') }
    $owned = [IO.Path]::GetFullPath($OwnedDirectory).TrimEnd('\') + '\'
    if ($owned -eq [IO.Path]::GetPathRoot($owned)) { $issues.Add('Owned path cannot be a drive root.') }
    if (-not $Info.CfgFile -or -not ([IO.Path]::GetFullPath($Info.CfgFile)).StartsWith($owned, [StringComparison]::OrdinalIgnoreCase)) { $issues.Add('Configuration is outside owned directory.') }
    foreach ($slot in 1..8) {
        if ($Info["nic$slot"] -ne 'none') { $issues.Add("Network adapter $slot is not disabled.") }
    }
    foreach ($pair in @(@('clipboard','disabled'), @('draganddrop','disabled'), @('vrde','off'), @('usb','off'), @('clipboard_file_transfers','off'))) {
        if ($Info[$pair[0]] -ne $pair[1]) { $issues.Add("$($pair[0]) isolation is not verified.") }
    }
    if (@($Info.Keys | Where-Object { $_ -like 'SharedFolder*' }).Count -gt 0) { $issues.Add('Shared folders are configured.') }
    return $issues.ToArray()
}
