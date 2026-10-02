$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$checks = 0
function Check([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
    $script:checks++
}
$xaml = Get-Content -LiteralPath (Join-Path $projectRoot 'MainWindow.xaml') -Raw
$main = Get-Content -LiteralPath (Join-Path $projectRoot 'MainWindow.xaml.cs') -Raw
$security = Get-Content -LiteralPath (Join-Path $projectRoot 'MainWindow.SecurityMitigations.cs') -Raw
foreach ($removed in @('ResourceAnalyzer_Click', 'BackgroundOwners_Click', 'StorageManager_Click', 'OfflineImages_Click')) {
    Check (-not $xaml.Contains($removed)) "Retired entry still present: $removed"
}
foreach ($retired in @('StorageServicing.cs', 'OfflineImageModels.cs', 'OfflineImageService.cs', 'ResourceMeasurement.cs', 'NativeResourceProbe.cs', 'BackgroundOwnerReport.cs', 'BackgroundOwnerService.cs')) {
    Check (-not (Test-Path -LiteralPath (Join-Path $projectRoot $retired))) "Retired backend still in production root: $retired"
    Check (Test-Path -LiteralPath (Join-Path $projectRoot "Tests/RetiredModules/$retired")) "Historical fixture missing: $retired"
}
Check (-not $security.Contains('SecurityMitigationPolicy.Execute')) 'UI still dispatches HVCI mutations'
Check (-not $security.Contains('new ToolWindow')) 'Security analysis still opens a popup'
Check ($security.Contains('includeHvciControls: false')) 'App report must describe HVCI as read-only'
Check ($security.Contains('LsaProtectionPolicy.Enable')) 'Existing independent LSA control was lost'
Check ([regex]::Matches($main, 'DialogMessageContent.Create\(message\)').Count -eq 2) 'Messages and confirmations must both use scrolling'
Check ([regex]::Matches($main, 'InlineAnalysisProgress progressWindow').Count -eq 7) 'All seven read-only progress routes must be inline'
Check (-not [regex]::IsMatch($main, 'CatalogProgressWindow progressWindow = new\(\s*this,\s*"(Analyzing|Reading|Collecting)"')) 'Read-only progress popup remains'
$inventory = Get-Content -LiteralPath (Join-Path $projectRoot 'MainWindow.CatalogInventory.cs') -Raw
Check (-not $inventory.Contains('ShowTableReportDialogAsync')) 'Action inventory still opens a popup'
Check ($main.Contains('progressWindow.CreateReporter(definition.Id)')) 'Mutating catalog progress was lost'
Write-Output "PASS: $checks simplified-interface source checks. No Windows changes."
