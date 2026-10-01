[CmdletBinding()]
param([Parameter(Mandatory)][guid]$ExpectedGuestUuid)
$ErrorActionPreference = 'Stop'
$system = Get-CimInstance Win32_ComputerSystem
$product = Get-CimInstance Win32_ComputerSystemProduct
if ($ExpectedGuestUuid -eq [guid]::Empty -or [guid]$product.UUID -ne $ExpectedGuestUuid) { throw 'Exact guest hardware identity does not match. No test was run.' }
if ($system.Model -notmatch '^VirtualBox$') { throw 'This evidence collector is restricted to the explicitly selected VirtualBox guest.' }
$os = Get-CimInstance Win32_OperatingSystem
$security = try { Get-CimInstance -Namespace root\Microsoft\Windows\DeviceGuard -ClassName Win32_DeviceGuard | Select-Object VirtualizationBasedSecurityStatus, SecurityServicesConfigured, SecurityServicesRunning, AvailableSecurityProperties } catch { @{ Unavailable = $_.Exception.Message } }
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
[pscustomobject]@{
    CollectedUtc = [DateTime]::UtcNow.ToString('O')
    GuestUuid = $product.UUID; Model = $system.Model; WindowsVersion = $os.Version
    Administrator = $admin; DeviceGuard = $security
    Note = 'Read-only environment evidence. Does not run Apply, rollback, reboot, WIM servicing or deployment.'
    ApplyRollback = 'NOT RUN'; WimDeployment = 'NOT RUN'
} | ConvertTo-Json -Depth 6
