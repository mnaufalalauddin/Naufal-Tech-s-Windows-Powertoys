$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Installer\PublishStage.ps1')
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('Powertoys-Publish-Audit-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
$script:passed = 0
function Assert-Test([bool]$Value, [string]$Message) {
    if (-not $Value) { throw "FAIL: $Message" }
    $script:passed++
}
try {
    $good = Join-Path $scratch 'win-x64-001'
    $failed = Join-Path $scratch 'win-x64-002'
    $tampered = Join-Path $scratch 'win-x64-003'
    foreach ($directory in @($good, $failed, $tampered)) { New-Item -ItemType Directory -Path $directory | Out-Null }
    foreach ($directory in @($good, $tampered)) {
        [IO.File]::WriteAllText((Join-Path $directory 'Naufal Windows Powertoys.exe'), 'synthetic bytes - not an executable')
        Complete-PublishStage -Directory $directory -Version '7.8.0'
    }
    Assert-Test ((Get-CompletedPublishStage -PublishRoot $scratch -Version '7.8.0').FullName -eq $tampered) 'newest complete stage chosen'
    $assetDirectory = Join-Path $tampered 'Assets'
    New-Item -ItemType Directory -Path $assetDirectory | Out-Null
    $asset = Join-Path $assetDirectory 'brand.ico'
    [IO.File]::WriteAllText($asset, 'original icon bytes')
    Complete-PublishStage -Directory $tampered -Version '7.8.0'
    Assert-Test ((Get-CompletedPublishStage -PublishRoot $scratch -Version '7.8.0').FullName -eq $tampered) 'complete asset inventory accepted'
    [IO.File]::WriteAllText($asset, 'modified icon bytes')
    Assert-Test ((Get-CompletedPublishStage -PublishRoot $scratch -Version '7.8.0').FullName -eq $good) 'changed icon rejected even when EXE matches'
    Complete-PublishStage -Directory $tampered -Version '7.8.0'
    Remove-Item -LiteralPath $asset
    [IO.Directory]::Delete($assetDirectory, $false)
    Assert-Test ((Get-CompletedPublishStage -PublishRoot $scratch -Version '7.8.0').FullName -eq $good) 'missing asset rejected'
    Complete-PublishStage -Directory $tampered -Version '7.8.0'
    $extra = Join-Path $tampered 'unexpected.dll'
    [IO.File]::WriteAllText($extra, 'unexpected library')
    Assert-Test ((Get-CompletedPublishStage -PublishRoot $scratch -Version '7.8.0').FullName -eq $good) 'unrecorded DLL rejected'
    Remove-Item -LiteralPath $extra
    $marker = Join-Path $tampered 'publish-complete.json'
    $savedMarker = [IO.File]::ReadAllText($marker)
    [IO.File]::WriteAllText($marker, $savedMarker.Replace('"Schema": 2', '"Schema": 1'))
    Assert-Test ((Get-CompletedPublishStage -PublishRoot $scratch -Version '7.8.0').FullName -eq $good) 'legacy EXE-only marker rejected'
    [IO.File]::WriteAllText($marker, $savedMarker)
    [IO.File]::WriteAllText((Join-Path $tampered 'Naufal Windows Powertoys.exe'), 'changed synthetic bytes')
    Assert-Test ((Get-CompletedPublishStage -PublishRoot $scratch -Version '7.8.0').FullName -eq $good) 'skip modified EXE and empty failed stage'
    Assert-Test ($null -eq (Get-CompletedPublishStage -PublishRoot $scratch -Version '8.0.0')) 'version mismatch rejected'
    [IO.File]::WriteAllText((Join-Path $tampered 'publish-complete.json'), '{broken')
    Assert-Test ((Get-CompletedPublishStage -PublishRoot $scratch -Version '7.8.0').FullName -eq $good) 'corrupt marker ignored'
    Remove-Item -LiteralPath (Join-Path $good 'Naufal Windows Powertoys.exe')
    Assert-Test ($null -eq (Get-CompletedPublishStage -PublishRoot $scratch -Version '7.8.0')) 'no incomplete stage accepted'
    Write-Output "PASS: $script:passed publish-stage assertions. No app or installer executed."
} finally {
    # Exact files in this uniquely created test tree only; no recursive removal.
    foreach ($directory in Get-ChildItem -LiteralPath $scratch -Directory) {
        foreach ($file in Get-ChildItem -LiteralPath $directory.FullName -File) { Remove-Item -LiteralPath $file.FullName }
        [IO.Directory]::Delete($directory.FullName, $false)
    }
    [IO.Directory]::Delete($scratch, $false)
}
