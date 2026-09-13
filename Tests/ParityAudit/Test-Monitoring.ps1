# Source wiring checks only; no application launch or system mutation.
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$main = Get-Content -LiteralPath (Join-Path $root 'MainWindow.xaml.cs') -Raw
$monitor = Get-Content -LiteralPath (Join-Path $root 'MainWindow.Monitoring.cs') -Raw
$chart = Get-Content -LiteralPath (Join-Path $root 'LiveMetricChart.cs') -Raw
[xml]$window = Get-Content -LiteralPath (Join-Path $root 'MainWindow.xaml') -Raw
$count = 0
function Assert([bool]$value, [string]$name) {
    if (-not $value) { throw "FAILED: $name" }
    $script:count++
}
$nameNamespace = 'http://schemas.microsoft.com/winfx/2006/xaml'
$taskButton = @($window.SelectNodes('//*[local-name()="Button"]') | Where-Object { $_.GetAttribute('Name', $nameNamespace) -eq 'TaskStatusButton' })
Assert ($taskButton.Count -eq 1) 'one monitoring navigation button'
Assert ($taskButton[0].Content -ceq 'Task Monitoring') 'canonical monitoring label'
Assert (-not $main.Contains('TaskStatusButton.Content =')) 'operation status cannot replace navigation label'
Assert ($monitor.Contains('ToolTipService.SetToolTip')) 'operation status preserved as tooltip'
Assert ($main.Contains('_taskActivityService.RunningSnapshot()')) 'monitor uses running-only view'
Assert (-not $main.Contains('_taskActivityService.Snapshot()')) 'monitor does not show diagnostic history'
Assert ($main.Contains('No tasks are currently running.')) 'empty monitor has explicit message'
Assert ($main.Contains('refreshTimer.Stop();')) 'monitor refresh stops when closed'
foreach ($metric in @('Cpu', 'Ram', 'Gpu', 'Network')) {
    $hostName = $metric + 'GraphHost'
    $hosts = @($window.SelectNodes('//*[local-name()="Grid"]') | Where-Object { $_.GetAttribute('Name', $nameNamespace) -eq $hostName })
    Assert ($hosts.Count -eq 1) ("one graph host: $metric")
    Assert ($monitor.Contains($hostName + '.Children.Add(')) ("graph attached: $metric")
    Assert ($hosts[0].ParentNode.SelectNodes('*[local-name()="TextBlock"]').Count -ge 2) ("existing label and metric retained: $metric")
}
Assert ($main.Contains('AppendLiveCharts(snapshot);') -and $main.Contains('AppendLiveCharts(null);')) 'good and failed samples reach charts'
Assert ($main.Contains('RenderLiveCharts();')) 'charts redraw after display changes'
Assert ($chart.Contains('SizeChanged +=') -and $chart.Contains('ActualWidth')) 'chart reacts to resized layout'
Assert (-not $chart.Contains('DispatcherTimer')) 'chart reuses existing monitor timer'
Assert ($chart.Contains('StrokeDashArray')) 'transmit also distinguished by line pattern'
Assert ($chart.Contains('ElementTheme.Dark')) 'chart has explicit dark-theme colors'
Write-Output "PASS: $count monitoring source-wiring assertions. No native UI or Windows settings changed."
