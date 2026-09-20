[CmdletBinding()]
param([Parameter(Mandatory)][string]$ProjectRoot, [Parameter(Mandatory)][string]$Destination)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$assets = Get-Content -LiteralPath (Join-Path $ProjectRoot 'obj\project.assets.json') -Raw | ConvertFrom-Json
$null = New-Item -ItemType Directory -Path $Destination -Force
$inventory = [Collections.Generic.List[string]]::new()
$inventory.Add('Third-party package license materials from the restored build inputs.')
$inventory.Add('The project MIT License does not replace these licenses. Build-only packages are included for provenance.')
foreach ($library in $assets.libraries.PSObject.Properties) {
    if ($library.Value.type -ne 'package') { continue }
    $packageRoot = $null
    foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
        $candidate = Join-Path $folder $library.Value.path
        if (Test-Path -LiteralPath $candidate -PathType Container) { $packageRoot = $candidate; break }
    }
    if ($null -eq $packageRoot) { throw "Package unavailable for license collection: $($library.Name)" }
    $packageDestination = Join-Path $Destination ($library.Name.Replace('/', '-'))
    $null = New-Item -ItemType Directory -Path $packageDestination -Force
    $nuspec = Get-ChildItem -LiteralPath $packageRoot -Filter '*.nuspec' | Select-Object -First 1
    if ($null -eq $nuspec) { throw "Package metadata missing: $($library.Name)" }
    Copy-Item -LiteralPath $nuspec.FullName -Destination $packageDestination
    $materials = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -File | Where-Object {
        $_.Name -match '(?i)^(license|licence|notice|third.?party.?notices|copyright|sdk_license)([._-].*)?$'
    })
    foreach ($file in $materials) {
        $relative = $file.FullName.Substring($packageRoot.Length).TrimStart([char[]]'\/')
        $target = Join-Path $packageDestination $relative
        $null = New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
    [xml]$metadata = Get-Content -LiteralPath $nuspec.FullName -Raw
    $license = $metadata.SelectSingleNode('//*[local-name()="metadata"]/*[local-name()="license"]')
    $url = $metadata.SelectSingleNode('//*[local-name()="metadata"]/*[local-name()="licenseUrl"]')
    $licenseText = if ($null -ne $license) { $license.InnerText } else { '' }
    $licenseUrl = if ($null -ne $url) { $url.InnerText } else { '' }
    $inventory.Add("$($library.Name): $($materials.Count) license/notice files; license=$licenseText; URL=$licenseUrl")
}
[IO.File]::WriteAllLines((Join-Path $Destination 'PACKAGE-NOTICES.txt'), $inventory, [Text.UTF8Encoding]::new($false))
$dotnetRoot = Split-Path -Parent (Get-Command dotnet -ErrorAction Stop).Source
$dotnetDestination = Join-Path $Destination 'Microsoft.NET'
$null = New-Item -ItemType Directory -Path $dotnetDestination -Force
foreach ($name in @('LICENSE.txt', 'ThirdPartyNotices.txt')) {
    $file = Join-Path $dotnetRoot $name
    if (!(Test-Path -LiteralPath $file)) { throw "Required .NET notice not found: $file" }
    Copy-Item -LiteralPath $file -Destination $dotnetDestination
}
$innoRoot = @((Join-Path $env:ProgramFiles 'Inno Setup 7'), (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7')) |
    Where-Object { Test-Path -LiteralPath (Join-Path $_ 'license.txt') } | Select-Object -First 1
if ($null -ne $innoRoot) {
    $innoDestination = Join-Path $Destination 'InnoSetup'
    $null = New-Item -ItemType Directory -Path $innoDestination -Force
    Copy-Item -LiteralPath (Join-Path $innoRoot 'license.txt') -Destination $innoDestination
}
