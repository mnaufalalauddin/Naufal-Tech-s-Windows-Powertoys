# Static integration checks only. A native Save dialog is not opened here.
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$main = Get-Content -LiteralPath (Join-Path $root 'MainWindow.xaml.cs') -Raw
$progress = Get-Content -LiteralPath (Join-Path $root 'CatalogProgressWindow.cs') -Raw
$export = Get-Content -LiteralPath (Join-Path $root 'DesktopReportExport.cs') -Raw
$checks = @(
    $main.Contains('DesktopReportExport.SaveAsync(window, suggestedFileName, plainText)'),
    $progress.Contains('DesktopReportExport.SaveAsync(_window, "Powertoys-Task-Report", report)'),
    (-not $main.Contains('using Windows.Storage.Pickers;')),
    (-not $progress.Contains('using Windows.Storage.Pickers;')),
    $export.Contains('using Microsoft.Windows.Storage.Pickers;'),
    $export.Contains('new(owner.AppWindow.Id)'),
    $export.Contains('if (result is null) return null;'),
    $export.Contains('await File.WriteAllTextAsync(result.Path, report)'),
    $main.Contains('window.IsBusy = () => savingReport;'),
    $progress.Contains('_window.IsBusy = () => !_completed || _exportInProgress;')
)
if ($checks -contains $false) { throw 'Report export routing or elevated-picker guard regressed.' }
Write-Output "PASS: $($checks.Count) static report-export assertions. Native save dialogs remain untested."
