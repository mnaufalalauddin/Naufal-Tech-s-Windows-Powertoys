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
Assert-Identity ($version -match '^\d+\.\d+\.\d+$') 'Invalid project version'
foreach ($field in @('AssemblyVersion','FileVersion','InformationalVersion')) {
    Assert-Identity ($project.SelectSingleNode("/Project/PropertyGroup/$field").InnerText -ceq '$(Version).0') "$field must derive from Version"
}
Assert-Identity ($manifest.Package.Identity.Version -ceq "$version.0") 'MSIX identity version drift'
Assert-Identity ($project.SelectSingleNode('/Project/PropertyGroup/Product').InnerText -ceq 'Naufal Windows Powertoys') 'Product identity drift'
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
& (Join-Path $projectRoot 'Installer\Generate-ProgramInformation.ps1') -ProjectRoot $projectRoot -OutputFile $output -Version "$version.0"
$generated = Get-Content -LiteralPath $output -Raw -Encoding UTF8
Assert-Identity ([regex]::Matches($generated, '(?m)^    \d+: begin').Count -eq 23) 'Installer requires 23 program-information translations'
Assert-Identity ([regex]::Matches($generated, 'ProgramInfoLanguage.Items.Add').Count -eq 23) 'Installer requires 23 language choices'
Assert-Identity ([regex]::Matches($generated, 'ProgramInfoText.Alignment := taRightJustify;').Count -eq 2) 'Arabic and Urdu information must be right-aligned'
Assert-Identity (!$generated.Contains([char]0xFFFD)) 'Installer information contains corrupted Unicode'
Assert-Identity ([regex]::Matches($generated, [regex]::Escape('Muhammad Naufal Alauddin')).Count -eq 23) 'Developer identity must remain intact in every language'
Assert-Identity ([regex]::Matches($generated, [regex]::Escape("$version.0")).Count -eq 23) 'All installer languages must show actual version'
Write-Output "PASS: $checks project identity, license and installer information assertions. No installer was run."
