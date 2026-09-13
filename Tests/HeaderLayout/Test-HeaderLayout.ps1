param([switch]$NoBuild, [ValidateSet(25,50,75,100,125,150,175,200)][int]$StartupScale = 100)
$ErrorActionPreference = 'Stop'
if (-not $NoBuild) {
    & dotnet build (Join-Path $PSScriptRoot 'HeaderLayout.Tests.csproj') -c Debug -p:Platform=x64 --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Header layout test host did not build.' }
}
$output = Join-Path $PSScriptRoot 'bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64'
$exe = Join-Path $output 'HeaderLayout.Tests.exe'
$report = Join-Path $output 'header-layout-results.txt'
$started = [DateTime]::UtcNow
$testProcess = Start-Process -FilePath $exe -ArgumentList "--startup-scale=$StartupScale" -WindowStyle Hidden -PassThru
if (-not $testProcess.WaitForExit(60000)) {
    # Only this newly created, backend-free test host may be stopped.
    $testProcess.Kill()
    throw 'Native header layout test timed out after 60 seconds.'
}
if (-not (Test-Path -LiteralPath $report)) { throw 'Native header layout test produced no report.' }
$reportFile = Get-Item -LiteralPath $report
if ($reportFile.LastWriteTimeUtc -lt $started.AddSeconds(-1)) { throw 'Native layout report was stale.' }
$result = Get-Content -LiteralPath $report -Raw
Write-Output $result
if ($testProcess.ExitCode -ne 0 -or $result -match 'FAIL' -or $result -notmatch '(?m)^PASS:') {
    throw 'Native header layout assertions failed; inspect the report above.'
}
