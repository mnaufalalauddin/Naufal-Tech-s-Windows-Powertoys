$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\VirtualMachine.Guards.ps1"
$count = 0
function Assert([bool]$value, [string]$message) { $script:count++; if (-not $value) { throw $message } }
$id = [guid]'aadd0920-6b73-4a51-a9b8-5909c90805ae'
$info = @{ UUID=$id.ToString(); name='NWU-Disposable-Fixture'; VMState='poweroff'; CfgFile='C:\Owned\Test\test.vbox'; clipboard='disabled'; draganddrop='disabled'; vrde='off'; usb='off'; clipboard_file_transfers='off' }
foreach($n in 1..8) { $info["nic$n"]='none' }
Assert (@(Get-VmIsolationBlockers $info $id 'C:\Owned\Test').Count -eq 0) 'Isolated fixture must pass.'
foreach($key in @('UUID','name','VMState','CfgFile','clipboard','draganddrop','vrde','usb','clipboard_file_transfers') + (1..8 | ForEach-Object { "nic$_" })) {
    $changed = $info.Clone(); $changed[$key]='invalid'
    Assert (@(Get-VmIsolationBlockers $changed $id 'C:\Owned\Test').Count -gt 0) "Unsafe $key must block."
}
$changed=$info.Clone(); $changed.SharedFolderNameMachineMapping1='shared'
Assert (@(Get-VmIsolationBlockers $changed $id 'C:\Owned\Test').Count -gt 0) 'Shared folder must block.'
$changed=$info.Clone(); $changed.CfgFile='C:\Owned\Test-Other\test.vbox'
Assert (@(Get-VmIsolationBlockers $changed $id 'C:\Owned\Test').Count -gt 0) 'Sibling prefix is outside target.'
$parsed=ConvertFrom-VBoxMachineInfo @('UUID="fixture"','"SATA-0-0"="C:\\Owned\\test.vdi"','nic1="none"')
Assert ($parsed['SATA-0-0'] -eq 'C:\Owned\test.vdi') 'Escaped storage path parser.'
Assert ($parsed.nic1 -eq 'none') 'Network parser.'
Write-Output "PASS: $count pure VM guard assertions. No VM started or changed."
