[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$checks = 0
function Assert-Identity([bool]$ok, [string]$message) {
    if (!$ok) { throw $message }
    $script:checks++
}
[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot "Naufal Tech's Windows Powertoys.csproj") -Raw
[xml]$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'Package.appxmanifest') -Raw
$version = $project.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
Assert-Identity ($version -match '^\d+\.\d+\.\d+\.\d+$') 'Invalid project version'
Assert-Identity ($project.SelectSingleNode('/Project/PropertyGroup/IncludeSourceRevisionInInformationalVersion').InnerText -ceq 'false') 'Product version must remain numeric without a commit suffix'
foreach ($field in @('AssemblyVersion','FileVersion','InformationalVersion')) {
    Assert-Identity ($project.SelectSingleNode("/Project/PropertyGroup/$field").InnerText -ceq '$(Version)') "$field must derive from Version"
}
Assert-Identity ($manifest.Package.Identity.Version -ceq "$version") 'MSIX identity version drift'
Assert-Identity ($project.SelectSingleNode('/Project/PropertyGroup/AssemblyName').InnerText -ceq 'Naufal Windows Utility') 'Executable name drift'
Assert-Identity ($project.SelectSingleNode('/Project/PropertyGroup/AssemblyTitle').InnerText -ceq 'Naufal Windows Utility') 'Executable title drift'
Assert-Identity ($project.SelectSingleNode('/Project/PropertyGroup/Product').InnerText -ceq 'Naufal Windows Utility') 'Product identity drift'
Assert-Identity ($project.SelectSingleNode('/Project/PropertyGroup/Company').InnerText -ceq 'Muhammad Naufal Alauddin') 'Publisher identity drift'
Assert-Identity ($project.SelectSingleNode('/Project/PropertyGroup/PackageLicenseExpression').InnerText -ceq 'MIT') 'Package license drift'
$identity = Get-Content -LiteralPath (Join-Path $projectRoot 'AppIdentity.cs') -Raw
Assert-Identity ($identity.Contains('typeof(AppIdentity).Assembly.GetName().Version')) 'About must read actual assembly version'
$license = Get-Content -LiteralPath (Join-Path $projectRoot 'LICENSE') -Raw
Assert-Identity ($license.StartsWith('MIT License')) 'Missing MIT title'
Assert-Identity ($license.Contains('Copyright (c) 2026 Muhammad Naufal Alauddin')) 'Copyright identity drift'
Assert-Identity ($license.Contains('Permission is hereby granted, free of charge')) 'Missing standard MIT grant'
Assert-Identity ($license.Contains('THE SOFTWARE IS PROVIDED "AS IS"')) 'Missing standard MIT warranty disclaimer'
$installer = Get-Content -LiteralPath (Join-Path $projectRoot 'Installer\NaufalWindowsPowertoys.iss') -Raw
Assert-Identity ($installer.Contains('#error AppVersion must be supplied')) 'Installer version must not silently default'
Assert-Identity ($installer.Contains('#define AppPublisher "Muhammad Naufal Alauddin"')) 'Installer publisher drift'
Assert-Identity ($installer.Contains('LicenseFile=..\LICENSE')) 'Installer must display LICENSE'
Assert-Identity ($installer.Contains('#include ProgramInfoFile')) 'Installer program information missing'

# Generate an isolated build artifact; never run an installer or mutate Windows.
$temporaryDirectory = Join-Path $projectRoot ('artifacts\identity-test\' + [Guid]::NewGuid().ToString('N'))
$output = Join-Path $temporaryDirectory 'ProgramInformation.generated.iss'
& (Join-Path $projectRoot 'Installer\Generate-ProgramInformation.ps1') -ProjectRoot $projectRoot -OutputFile $output -Version "$version"
$generated = Get-Content -LiteralPath $output -Raw -Encoding UTF8
Assert-Identity ($generated.Contains('procedure LoadProgramInformation;')) 'English information procedure missing'
Assert-Identity (!$installer.Contains('ProgramInfoLanguage')) 'Installer language selector must be removed'
Assert-Identity (!$generated.Contains([char]0xFFFD)) 'Installer information contains corrupted Unicode'
Assert-Identity ([regex]::Matches($generated, [regex]::Escape('Muhammad Naufal Alauddin')).Count -eq 1) 'Developer identity drift'
Assert-Identity ($generated.Contains("Version v$version")) 'Installer must show actual version'
Assert-Identity ($generated.Contains('ProgramInfoText.Alignment := taLeftJustify;')) 'English information alignment'
[xml]$nativeManifest = Get-Content -LiteralPath (Join-Path $projectRoot 'app.manifest') -Raw
Assert-Identity ($nativeManifest.assembly.assemblyIdentity.version -ceq $version) 'Native manifest version drift'
Assert-Identity ($nativeManifest.assembly.assemblyIdentity.name -ceq 'NaufalWindowsUtility.app') 'Native manifest branding drift'
Assert-Identity ($identity.Contains('DisplayVersion => "v" + Version')) 'Display version must use v prefix without changing numeric metadata'
$about = Get-Content -LiteralPath (Join-Path $projectRoot 'MainWindow.About.cs') -Raw
Assert-Identity ($about.Contains('AppIdentity.DisplayVersion')) 'About must use display version'
Assert-Identity ($installer.Contains('AppVerName={#AppName} v{#AppVersion}')) 'Setup display version must use v prefix'
Assert-Identity ($installer.Contains('VersionInfoVersion={#AppVersion}') -and !$installer.Contains('VersionInfoVersion={#AppVersion}.0')) 'Installer numeric version must not append a fifth component'
Assert-Identity ($identity.Contains('https://github.com/mnaufalalauddin/Naufal-Windows-Utility')) 'Source URL must match renamed repository'
Write-Output "PASS: $checks project identity, license and installer information assertions. No installer was run."
