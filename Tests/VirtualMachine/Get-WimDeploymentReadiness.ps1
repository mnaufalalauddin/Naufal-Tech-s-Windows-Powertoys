[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceWim,
    [Parameter(Mandatory)][ValidateRange(1, 65535)][int]$Index,
    [guid]$ExpectedGuestUuid = 'd0c5b1fe-5c91-4b69-9176-a330093074df'
)

# Observation only: no image mount, export, servicing, partitioning, deployment or reboot.
# DISM may write its own diagnostic log. This script does not create a report file.
# Dot-sourcing defines helpers only; every actual inventory runs through the guest guard.
function Assert-NwuReadinessPathSyntax {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path) -or $Path -notmatch '^[A-Za-z]:\\' -or
        $Path.Substring(2).Contains(':') -or $Path.IndexOfAny([char[]]"`0`r`n`"*?<>|") -ge 0 -or
        $Path -match '(^|[\\/])\.{1,2}([\\/]|$)' -or $Path.Contains('/')) {
        throw 'Use an exact absolute local optical-media path; network/device paths, relative segments, wildcards and alternate streams are rejected.'
    }
    if ([IO.Path]::GetExtension($Path) -notin @('.wim', '.esd')) {
        throw 'Only .wim and .esd source metadata may be inspected; split images are not supported by this collector.'
    }
    return [IO.Path]::GetFullPath($Path)
}

function Get-NwuReadinessImageIdentity {
    param([string]$Text, [int]$SelectedIndex)
    $records = @(); $current = $null
    foreach ($line in ($Text -split '\r?\n')) {
        if ($line -notmatch '^\s*([^:]+?)\s*:\s*(.*?)\s*$') { continue }
        $key = $Matches[1].Trim(); $value = $Matches[2].Trim()
        if ($key -eq 'Index') { $current = @{}; $records += $current }
        if ($null -ne $current) { $current[$key] = $value }
    }
    $parsedIndex = 0; $version = $null
    if ($records.Count -ne 1 -or -not [int]::TryParse([string]$records[0]['Index'], [ref]$parsedIndex) -or $parsedIndex -ne $SelectedIndex) {
        throw 'DISM metadata did not identify exactly the requested image index.'
    }
    $record = $records[0]
    if ($record['Architecture'] -notin @('x64', 'x86', 'arm64') -or [string]::IsNullOrWhiteSpace($record['Edition']) -or
        $record['Installation'] -ne 'Client' -or -not [version]::TryParse([string]$record['Version'], [ref]$version) -or
        $version.Major -ne 10 -or $version.Build -lt 19041) {
        throw 'Unsupported or incomplete image identity: require Windows client 10.0 build 19041+, an edition, and x64/x86/ARM64 architecture.'
    }
    [pscustomobject]@{ Index = $parsedIndex; Version = $version.ToString(); Architecture = $record['Architecture']; Edition = $record['Edition']; Installation = $record['Installation'] }
}

function Assert-NwuReadinessGuestIdentity {
    param([guid]$RequestedUuid, [string]$ObservedUuid, [string]$Model, [bool]$Administrator)
    $authorizedUuid = [guid]'d0c5b1fe-5c91-4b69-9176-a330093074df'
    $observed = [guid]::Empty
    if ($RequestedUuid -ne $authorizedUuid -or -not [guid]::TryParse($ObservedUuid, [ref]$observed) -or
        $observed -ne $authorizedUuid -or $Model -cne 'VirtualBox' -or -not $Administrator) {
        throw 'Readiness collection is restricted to the authorized disposable VirtualBox clone, elevated inside that guest. No image command ran.'
    }
}

function Invoke-NwuWimDeploymentReadiness {
    param([string]$Source, [int]$SelectedIndex, [guid]$ExpectedUuid)
    $ErrorActionPreference = 'Stop'
    $systems = @(Get-CimInstance -ClassName Win32_ComputerSystem)
    $products = @(Get-CimInstance -ClassName Win32_ComputerSystemProduct)
    if ($systems.Count -ne 1 -or $products.Count -ne 1) { throw 'Guest identity is ambiguous. No image command ran.' }
    $administrator = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    Assert-NwuReadinessGuestIdentity $ExpectedUuid $products[0].UUID $systems[0].Model $administrator
    if (-not [Environment]::Is64BitProcess) { throw 'Run the guest 64-bit PowerShell so native DISM and registry evidence are unambiguous.' }
    if ($SelectedIndex -lt 1 -or $SelectedIndex -gt 65535) { throw 'A positive explicit image index is required.' }
    $sourcePath = Assert-NwuReadinessPathSyntax $Source
    $driveRoot = [IO.Path]::GetPathRoot($sourcePath)
    $drive = [IO.DriveInfo]::new($driveRoot)
    if ($drive.DriveType -ne [IO.DriveType]::CDRom -or -not $drive.IsReady) { throw 'Source must be on a ready guest optical drive; fixed disks, network drives and removable data drives are rejected.' }
    $sourceItem = Get-Item -LiteralPath $sourcePath -Force
    if ($sourceItem.PSIsContainer -or $sourceItem.Length -le 0) { throw 'Source image is missing or empty.' }
    for ($part = $sourcePath; $null -ne $part; $part = [IO.Path]::GetDirectoryName($part)) {
        if (((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Reparse points are not accepted in the image path.' }
    }

    $blockers = [Collections.Generic.List[string]]::new()
    $pending = [ordered]@{}
    $pendingChecks = [ordered]@{
        ComponentServicingReboot = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending'
        ComponentServicingPackages = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\PackagesPending'
        WindowsUpdateReboot = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired'
    }
    foreach ($name in $pendingChecks.Keys) {
        try { $pending[$name] = [bool](Test-Path -LiteralPath $pendingChecks[$name] -ErrorAction Stop) }
        catch { $pending[$name] = 'Unknown'; $blockers.Add('Could not read pending-servicing indicator: ' + $name) }
    }
    try {
        $sessionManager = Get-ItemProperty -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager' -ErrorAction Stop
        # Report presence only, never the queued filenames.
        foreach ($entry in @(@('FileRenameOperations','PendingFileRenameOperations'), @('FileRenameOperations2','PendingFileRenameOperations2'))) {
            $property = $sessionManager.PSObject.Properties[$entry[1]]
            $pending[$entry[0]] = if ($null -eq $property) { $false } else { @($property.Value | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count -gt 0 }
        }
    } catch { $pending['FileRenameOperations'] = 'Unknown'; $blockers.Add('Could not read pending file-operation indicators.') }
    if (@($pending.Values | Where-Object { $_ -eq $true }).Count -gt 0) { $blockers.Add('Guest reports pending servicing/restart; resolve and recollect before any later deployment work.') }

    $volumes = @(); $disks = @()
    $floorBytes = [decimal]$sourceItem.Length * 2 + 10GB
    try {
        $volumes = @(Get-CimInstance -ClassName Win32_LogicalDisk -Filter 'DriveType=3' | ForEach-Object {
            [pscustomobject]@{ Drive = $_.DeviceID; FileSystem = $_.FileSystem; SizeBytes = $_.Size; FreeBytes = $_.FreeSpace; MeetsCloneFloor = ($null -ne $_.FreeSpace -and [decimal]$_.FreeSpace -ge $floorBytes) }
        })
        if (@($volumes | Where-Object MeetsCloneFloor).Count -eq 0) { $blockers.Add('No observed fixed volume meets the conservative clone floor (2x source size + 10 GiB). Mount/export/deployment can need more.') }
    } catch { $blockers.Add('Fixed-volume free-space inventory failed.') }
    try {
        $disks = @(Get-CimInstance -Namespace root/Microsoft/Windows/Storage -ClassName MSFT_Disk | Select-Object Number, Size, BusType, PartitionStyle, NumberOfPartitions, IsBoot, IsSystem, IsOffline, IsReadOnly)
        if ($disks.Count -eq 0) { $blockers.Add('Physical-disk inventory is empty.') }
    } catch { $blockers.Add('Physical-disk inventory failed.') }
    # No disk is selected or authorized by this report; a blank disk is not automatically safe.
    $blockers.Add('No dedicated deployment target has been identified and approved. No partition, format, apply-image or boot test has run.')
    if ([IO.Path]::GetExtension($sourcePath) -ieq '.esd') {
        $blockers.Add('Source is ESD; the application offline workflow requires a separately exported WIM in an owned guest workspace. This collector does not export it.')
    }
    $dism = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::System)) 'dism.exe'
    $identity = $null; $dismExit = $null
    try {
        # Path syntax above rejects quotes/control characters; fixed argument vocabulary only.
        $info = [Diagnostics.ProcessStartInfo]::new()
        $info.FileName = $dism; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
        $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
        $info.Arguments = '/English /Get-WimInfo /WimFile:"' + $sourcePath + '" /Index:' + $SelectedIndex
        $process = [Diagnostics.Process]::new(); $process.StartInfo = $info
        try {
            if (-not $process.Start()) { throw 'DISM did not start.' }
            $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
            $process.WaitForExit(); $dismExit = $process.ExitCode
            $output = $stdout.GetAwaiter().GetResult() + "`n" + $stderr.GetAwaiter().GetResult()
        } finally { $process.Dispose() }
        if ($dismExit -ne 0) { $blockers.Add('Read-only DISM metadata query failed; exit ' + $dismExit + '. Inspect the guest DISM diagnostic log.') }
        else { $identity = Get-NwuReadinessImageIdentity $output $SelectedIndex }
    } catch { $blockers.Add('Image identity could not be verified: ' + $_.Exception.Message) }
    $os = Get-CimInstance -ClassName Win32_OperatingSystem
    [pscustomobject]@{
        Schema = 1; CollectedUtc = [DateTime]::UtcNow.ToString('O'); GuestUuid = $products[0].UUID
        Model = $systems[0].Model; Administrator = $administrator; WindowsVersion = $os.Version
        LastBootUtc = $os.LastBootUpTime.ToUniversalTime().ToString('O')
        Source = $sourcePath; SourceBytes = $sourceItem.Length; RequestedIndex = $SelectedIndex
        DismMetadataExit = $dismExit; ImageIdentity = $identity
        ConservativeCloneFloorBytes = $floorBytes; FixedVolumes = $volumes; Disks = $disks
        PendingServicing = $pending; Blockers = @($blockers.ToArray())
        Status = 'READINESS ONLY — DEPLOYMENT NOT RUN'
        Note = 'Metadata and free-space evidence do not verify image integrity, compatibility, isolation, successful servicing, bootability or deployment. Disk identifiers/serial numbers, network addresses and credentials are not collected.'
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    Invoke-NwuWimDeploymentReadiness -Source $SourceWim -SelectedIndex $Index -ExpectedUuid $ExpectedGuestUuid | ConvertTo-Json -Depth 8
}
