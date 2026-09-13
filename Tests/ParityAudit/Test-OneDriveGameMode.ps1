$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$taskChecks = 0
function Assert-Source([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "FAILED: $Name" }
    $script:taskChecks++
}
$oneDrive = Get-Content -LiteralPath (Join-Path $taskRoot 'OneDriveAppService.cs') -Raw
Assert-Source ($oneDrive.Contains('new OneDriveCommandSession(scope)')) 'scope-specific session'
Assert-Source (([regex]::Matches($oneDrive, 'commands.RunAsync\(')).Count -eq 2) 'source and deployment use same session'
Assert-Source (-not $oneDrive.Contains('new NativeCommandRunner()')) 'no elevated OneDrive escape path'
Assert-Source ($oneDrive.Contains('Timeout.InfiniteTimeSpan')) 'deployment remains under shared gate'
Assert-Source ($oneDrive.Contains('var after = ReadInstalled();')) 'post-operation inventory required'
$game = Get-Content -LiteralPath (Join-Path $taskRoot 'GamingTweaksService.cs') -Raw
Assert-Source ($game.Contains('GameModeSetting.Apply(targetOn,')) 'production Game Mode writes explicit requested state'
Assert-Source ($game.Contains('IsFeatureSwitch: true')) 'Game Mode marked as actual feature switch'
Assert-Source ($game.Contains('verified && !definition.IsFeatureSwitch')) 'ON preserves Game Mode snapshot'
$window = Get-Content -LiteralPath (Join-Path $taskRoot 'MainWindow.xaml.cs') -Raw
Assert-Source ($window.Contains('CatalogTogglePolicy.FromSwitch(definition, requestedOn)')) 'UI uses tested switch routing'
Assert-Source ($window.Contains('RunApplyItemsAsync(new[] { definition }, requestedOn)')) 'UI passes requested direction'
Assert-Source ($window.Contains('targetOn ? CatalogOperation.Apply : CatalogOperation.SetOff')) 'worker receives OFF operation'
foreach ($file in 'GamingRuntimeCompatibilityService.cs','GamingRuntimeInstallerService.cs') {
    $runtime = Get-Content -LiteralPath (Join-Path $taskRoot $file) -Raw
    Assert-Source (-not $runtime.Contains('WebView2')) "WebView2 removed from $file"
}
Write-Host "PASS: $taskChecks OneDrive/Game Mode/runtime wiring assertions. No Windows settings changed."
