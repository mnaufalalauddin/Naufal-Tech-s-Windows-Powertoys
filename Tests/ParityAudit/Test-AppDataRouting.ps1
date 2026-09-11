# Source wiring only. This never reads/writes the user's actual AppData.
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
function Source([string]$name) { Get-Content -LiteralPath (Join-Path $root $name) -Raw }
$app = Source 'App.xaml.cs'
$display = Source 'UiDisplaySettings.cs'
$wizard = Source 'FirstRunPrerequisiteService.cs'
$paths = Source 'AppDataPaths.cs'
$checks = [ordered]@{
    'Canonical root name' = $paths.Contains('internal const string FolderName = "Naufal Windows Powertoys";')
    'Startup migration before MainWindow' = $app.Contains('AppDataPaths.MigrateKnownFiles(AppDataPaths.LocalBase)')
    'Second instance cannot migrate live data' = $app.Contains("if (_ownsSingleInstanceMutex)`r`n                foreach (string warning in AppDataPaths.MigrateKnownFiles") -or $app.Contains("if (_ownsSingleInstanceMutex)`n                foreach (string warning in AppDataPaths.MigrateKnownFiles")
    'Migration warnings retained' = $app.Contains('"AppData migration warning"')
    'Canonical crash log' = $app.Contains('string directory = AppDataPaths.LocalRoot;')
    'Canonical preferences' = $display.Contains('PreferenceDirectory = AppDataPaths.SettingsDirectory;')
    'No former preference writer' = -not $display.Contains('"WindowsPowerToysV78"')
    'Canonical wizard state' = $wizard.Contains('StateDirectory = AppDataPaths.SettingsDirectory;')
    'Canonical wizard temporary package' = $wizard.Contains('AppDataPaths.GetTemporaryDirectory()')
    'Canonical runtime cache' = (Source 'GamingRuntimeInstallerService.cs').Contains('string path = AppDataPaths.RuntimeCacheDirectory;')
    'Canonical GPU cache' = (Source 'GpuDriverService.cs').Contains('AppDataPaths.RuntimeCacheDirectory,')
    'Debloat legacy snapshot compatibility' = (Source 'DebloatService.cs').Contains('AppDataPaths.ResolveLegacyBackup("Debloat_LowRisk_Original.json")')
    'Performance legacy snapshot compatibility' = (Source 'PerformanceLabService.cs').Contains('AppDataPaths.ResolveLegacyBackup("performance-lab-original.json")')
    'Service legacy snapshot compatibility' = (Source 'PreviousServiceSnapshot.cs').Contains('AppDataPaths.ResolveLegacyBackup("performance-lab-original.json")')
    'Privilege temporary files grouped' = -not (Source 'GamingActionsService.cs').Contains('Path.GetTempPath()')
    'Windows temp cleanup target not renamed' = (Source 'EssentialActionsService.cs').Contains('Path.GetTempPath()')
    'Migration never overwrites destination' = $paths.Contains('File.Move(temporary, destination, overwrite: false);')
    'Migration copies instead of moving source' = $paths.Contains('File.Copy(source, temporary, overwrite: false);')
}
foreach ($check in $checks.GetEnumerator()) {
    if (-not $check.Value) { throw "AppData routing regression: $($check.Key)" }
}
Write-Output "PASS: $($checks.Count) static AppData-routing assertions. No user AppData changed."
