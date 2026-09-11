[CmdletBinding()]
param(
    [ValidateSet('Release')]
    [string]$Configuration = 'Release',

    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '8.0.0',

    [switch]$NoRestore,

    [switch]$SkipPublish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = $PSScriptRoot
. (Join-Path $projectRoot 'Installer\PublishStage.ps1')
$projectFile = Join-Path $projectRoot "Naufal Tech's Windows Powertoys.csproj"
$installerScript = Join-Path $projectRoot 'Installer\NaufalWindowsPowertoys.iss'
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$publishRelativeDir = "artifacts\publish\win-x64-$timestamp"
$publishDir = Join-Path $projectRoot $publishRelativeDir
$installerOutputDir = Join-Path $projectRoot 'artifacts\installer'
$applicationExe = Join-Path $publishDir 'Naufal Windows Powertoys.exe'
$installerExe = Join-Path $installerOutputDir "Naufal-Windows-Powertoys-Setup-$Version-x64.exe"

if (-not $SkipPublish) {
    $publishArguments = @(
        'publish',
        $projectFile,
        '-c', $Configuration,
        '-r', 'win-x64',
        '--self-contained', 'true',
        '-p:Platform=x64',
        '-p:PublishAot=true',
        "-p:Version=$Version",
        "-p:InformationalVersion=$Version.0",
        "-p:FileVersion=$Version.0",
        "-p:AssemblyVersion=$Version.0",
        '-p:WindowsPackageType=None',
        '-p:WindowsAppSDKSelfContained=true'
    )

    if ($NoRestore) {
        $publishArguments += '--no-restore'
    }

    Write-Host 'Publishing Native AOT application...' -ForegroundColor Cyan
    & dotnet @publishArguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    # Windows App SDK's publish copy target can produce MSB3094 when PublishDir
    # is overridden to an arbitrary absolute folder. Publish to the project's
    # normal SDK directory, then stage that complete result for Inno Setup.
    [xml]$projectXml = Get-Content -LiteralPath $projectFile -Raw
    $targetFrameworkNode = $projectXml.SelectSingleNode('/Project/PropertyGroup/TargetFramework')
    if ($null -eq $targetFrameworkNode -or
        [string]::IsNullOrWhiteSpace($targetFrameworkNode.InnerText)) {
        throw 'TargetFramework could not be read from the project file.'
    }
    $targetFramework = $targetFrameworkNode.InnerText.Trim()

    $defaultPublishDir = Join-Path $projectRoot "bin\$Configuration\$targetFramework\win-x64\publish"
    $defaultApplicationExe = Join-Path $defaultPublishDir 'Naufal Windows Powertoys.exe'
    if (-not (Test-Path -LiteralPath $defaultApplicationExe -PathType Leaf)) {
        throw "The expected Native AOT executable was not generated: $defaultApplicationExe"
    }

    New-Item -ItemType Directory -Path $publishDir -ErrorAction Stop | Out-Null
    Copy-Item -Path (Join-Path $defaultPublishDir '*') -Destination $publishDir -Recurse -Force
    Complete-PublishStage -Directory $publishDir -Version $Version
}
else {
    $latestPublish = Get-CompletedPublishStage -PublishRoot (Join-Path $projectRoot 'artifacts\publish') -Version $Version

    if ($null -eq $latestPublish) {
        throw 'No complete, hash-verified publish stage matches this version. Run without -SkipPublish first.'
    }

    $publishDir = $latestPublish.FullName
    $applicationExe = Join-Path $publishDir 'Naufal Windows Powertoys.exe'
}

if (-not (Test-Path -LiteralPath $applicationExe -PathType Leaf)) {
    throw "Published executable was not found: $applicationExe"
}

# Verify the staged binary and every derived branding asset against this source
# tree before packaging, including when an older stage is chosen with -SkipPublish.
& (Join-Path $projectRoot 'Tests\ParityAudit\Test-AppIcons.ps1') -PublishDirectory $publishDir

$isccCandidates = @(
    (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe')
)

$iscc = $isccCandidates |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Select-Object -First 1

if ($null -eq $iscc) {
    $isccCommand = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($null -ne $isccCommand) {
        $iscc = $isccCommand.Source
    }
}

if ($null -eq $iscc) {
    throw @'
Inno Setup 7 was not found. Install the 64-bit edition first:
winget install --id JRSoftware.InnoSetup.7 -e -s winget -i
'@
}

New-Item -ItemType Directory -Path $installerOutputDir -Force | Out-Null

Write-Host 'Compiling Setup installer...' -ForegroundColor Cyan
& $iscc "/DSourceDir=$publishDir" "/DOutputDir=$installerOutputDir" "/DAppVersion=$Version" $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $installerExe -PathType Leaf)) {
    throw "Installer was not generated at the expected path: $installerExe"
}

$installerItem = Get-Item -LiteralPath $installerExe
$installerHash = Get-FileHash -LiteralPath $installerExe -Algorithm SHA256

Write-Host ''
Write-Host 'Installer created successfully.' -ForegroundColor Green
Write-Host "File   : $($installerItem.FullName)"
Write-Host "Size   : $($installerItem.Length) bytes"
Write-Host "SHA256 : $($installerHash.Hash)"
