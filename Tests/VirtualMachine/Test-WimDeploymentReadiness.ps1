[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Get-WimDeploymentReadiness.ps1') -SourceWim 'D:\sources\install.wim' -Index 1
$script:checks = 0
function Assert-ReadinessTest([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:checks++
}
function Assert-ReadinessReject([scriptblock]$Action, [string]$Message) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Assert-ReadinessTest $rejected $Message
}

$id = [guid]'d0c5b1fe-5c91-4b69-9176-a330093074df'
Assert-NwuReadinessGuestIdentity $id $id.ToString() 'VirtualBox' $true
Assert-ReadinessTest $true 'The authorized elevated clone must pass the pure identity guard.'
Assert-ReadinessReject { Assert-NwuReadinessGuestIdentity ([guid]::Empty) $id.ToString() 'VirtualBox' $true } 'An empty requested UUID must fail.'
Assert-ReadinessReject { Assert-NwuReadinessGuestIdentity ([guid]'00000000-0000-0000-0000-000000000001') '00000000-0000-0000-0000-000000000001' 'VirtualBox' $true } 'A matching but unauthorized VM must fail.'
Assert-ReadinessReject { Assert-NwuReadinessGuestIdentity $id 'not-a-guid' 'VirtualBox' $true } 'Invalid observed identity must fail.'
Assert-ReadinessReject { Assert-NwuReadinessGuestIdentity $id $id.ToString() 'Host PC' $true } 'The host model must fail.'
Assert-ReadinessReject { Assert-NwuReadinessGuestIdentity $id $id.ToString() 'VirtualBox' $false } 'Non-elevated collection must fail.'

foreach ($valid in @('D:\sources\install.wim', 'E:\image folder\install.esd', 'F:\sources\INSTALL.WIM')) {
    Assert-ReadinessTest ((Assert-NwuReadinessPathSyntax $valid) -eq $valid) ('Valid syntax was rejected: ' + $valid)
}
foreach ($invalid in @('', 'sources\install.wim', '\\server\share\install.wim', '\\?\D:\sources\install.wim',
    'D:\sources\install.wim:stream', 'D:\sources\..\install.wim', 'D:\.\install.wim', 'D:/sources/install.wim',
    'D:\sources\*.wim', 'D:\sources\install?.wim', 'D:\sources\install.swm', 'D:\sources\install.iso',
    'D:\sources\"install.wim', "D:\sources\install`n.wim", "D:\sources\install`0.wim")) {
    Assert-ReadinessReject { Assert-NwuReadinessPathSyntax $invalid } ('Unsafe syntax was accepted: ' + $invalid)
}

$metadata = @'
Deployment Image Servicing and Management tool
Details for image : D:\sources\install.wim
Index : 1
Name : Windows 11 Pro
Architecture : x64
Version : 10.0.26300
Edition : Professional
Installation : Client
The operation completed successfully.
'@
$identity = Get-NwuReadinessImageIdentity $metadata 1
Assert-ReadinessTest ($identity.Index -eq 1 -and $identity.Version -eq '10.0.26300' -and $identity.Edition -eq 'Professional') 'Valid selected image identity must parse.'
Assert-ReadinessReject { Get-NwuReadinessImageIdentity $metadata 2 } 'Wrong requested image index must fail.'
Assert-ReadinessReject { Get-NwuReadinessImageIdentity ($metadata + "`nIndex : 2") 1 } 'Multiple image records must fail.'
foreach ($invalid in @(
    ($metadata -replace 'x64', 'unknown'),
    ($metadata -replace 'Client', 'Server'),
    ($metadata -replace '10.0.26300', '10.0.17763'),
    ($metadata -replace '10.0.26300', '11.0.26300'),
    ($metadata -replace 'Professional', ''),
    ($metadata -replace '10.0.26300', 'garbage')
)) { Assert-ReadinessReject { Get-NwuReadinessImageIdentity $invalid 1 } 'Unsupported/incomplete image identity must fail.' }

# Structural guard: fixture execution must never invoke guest inventory or DISM.
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Get-WimDeploymentReadiness.ps1'), [ref]$tokens, [ref]$errors)
Assert-ReadinessTest ($errors.Count -eq 0) 'Readiness collector must parse without errors.'
$commandNames = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.CommandAst] }, $true) | ForEach-Object { $_.GetCommandName() })
foreach ($forbidden in @('Restart-Computer', 'Stop-Computer', 'Mount-DiskImage', 'Mount-WindowsImage', 'Dismount-WindowsImage',
    'Initialize-Disk', 'Clear-Disk', 'Format-Volume', 'New-Partition', 'Remove-Partition', 'Set-Disk', 'Set-ItemProperty',
    'Remove-Item', 'Copy-Item', 'New-Item', 'Set-Content', 'Out-File')) {
    Assert-ReadinessTest ($commandNames -notcontains $forbidden) ('Readiness collector must not call ' + $forbidden)
}
Write-Output ("PASS: {0} pure WIM readiness assertions. No guest inventory, DISM, image mutation, deployment or reboot was run." -f $script:checks)
