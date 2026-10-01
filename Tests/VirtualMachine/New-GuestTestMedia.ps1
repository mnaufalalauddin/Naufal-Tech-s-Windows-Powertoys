[CmdletBinding()]
param([Parameter(Mandatory)][string]$InstallerPath, [string]$WindowsSourceImage)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$harness = Join-Path $repo 'artifacts\vm-test-payload\Harness'
$installer = Get-Item -LiteralPath $InstallerPath
if ($installer.PSIsContainer -or $installer.Extension -ne '.exe') { throw 'An existing installer executable is required.' }
if (-not (Test-Path -LiteralPath (Join-Path $harness 'ProfileVerification.Tests.exe') -PathType Leaf)) { throw 'Publish the self-contained ProfileVerification harness first.' }
$all = @(Get-Item -LiteralPath $harness) + @(Get-ChildItem -LiteralPath $harness -Recurse -Force)
if (@($all | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Harness includes a reparse point; refusing media exposure.' }
$output = Join-Path $repo ('artifacts\vm-test-media-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N'))
if (Test-Path -LiteralPath $output) { throw 'Unique media output already exists.' }
New-Item -ItemType Directory -Path $output | Out-Null
$payload = Join-Path $output 'payload'
New-Item -ItemType Directory -Path $payload | Out-Null
Copy-Item -LiteralPath $harness -Destination (Join-Path $payload 'Harness') -Recurse
foreach ($script in @('Invoke-GuestRoundTrip.ps1','Get-GuestValidationEvidence.ps1','Get-WimDeploymentReadiness.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $script) -Destination (Join-Path $payload $script)
}
Copy-Item -LiteralPath $installer.FullName -Destination (Join-Path $payload 'Naufal-Windows-Utility-Setup.exe')
$mediaNames = @('Harness','Invoke-GuestRoundTrip.ps1','Get-GuestValidationEvidence.ps1','Get-WimDeploymentReadiness.ps1','Naufal-Windows-Utility-Setup.exe','SHA256SUMS.txt')
if ($WindowsSourceImage) {
    $sourceImage = Get-Item -LiteralPath $WindowsSourceImage -Force
    if ($sourceImage.PSIsContainer -or $sourceImage.Extension -notin @('.esd','.wim') -or $sourceImage.Length -le 0 -or
        ($sourceImage.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'An existing non-reparse WIM/ESD file is required.' }
    $imageDirectory = Join-Path $payload 'WindowsSource'
    New-Item -ItemType Directory -Path $imageDirectory | Out-Null
    $imageCopy = Join-Path $imageDirectory ('install' + $sourceImage.Extension.ToLowerInvariant())
    Copy-Item -LiteralPath $sourceImage.FullName -Destination $imageCopy
    if ((Get-FileHash -LiteralPath $sourceImage.FullName).Hash -ne (Get-FileHash -LiteralPath $imageCopy).Hash) { throw 'Windows source copy hash mismatch; do not attach this media.' }
    $mediaNames += 'WindowsSource'
}
$hashes = @(Get-ChildItem -LiteralPath $payload -File -Recurse | Sort-Object FullName | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash + '  ' + $_.FullName.Substring($payload.Length + 1)
})
[IO.File]::WriteAllLines((Join-Path $payload 'SHA256SUMS.txt'), $hashes, [Text.UTF8Encoding]::new($false))
$viso = Join-Path $output 'NWU-Guest-Tests.viso'
$recipe = @('--iprt-iso-maker-file-marker-ms=' + [guid]::NewGuid().ToString(), '--volume-id=NWU_VM_TESTS')
foreach ($name in $mediaNames) {
    $source = Join-Path $payload $name
    if ($source.Contains('"')) { throw 'Invalid media path.' }
    $recipe += '"/' + $name + '=' + $source + '"'
}
[IO.File]::WriteAllLines($viso, $recipe, [Text.UTF8Encoding]::new($false))
[pscustomobject]@{ VirtualIso=$viso; Payload=$payload; HashedFiles=$hashes.Count; InstallerSha256=(Get-FileHash -LiteralPath $installer.FullName).Hash; Note='Media only; no VM mounted, started or changed, and no guest test executed.' } | ConvertTo-Json
