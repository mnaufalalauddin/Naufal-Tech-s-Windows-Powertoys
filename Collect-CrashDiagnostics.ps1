[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$diagnostics = [ordered]@{
    CollectedAt = [DateTimeOffset]::Now.ToString('O')
    Computer = $env:COMPUTERNAME
    Windows = [Environment]::OSVersion.VersionString
    Notes = @('Read-only diagnostics; no repair, service, registry or package changes are performed.')
}
$notes = [Collections.Generic.List[string]]::new()
$diagnostics['Processes'] = @(Get-Process -ErrorAction SilentlyContinue |
    Where-Object { $_.ProcessName -eq 'Naufal Windows Powertoys' } |
    ForEach-Object {
        try { [pscustomobject]@{ Id = $_.Id; Path = $_.Path; Started = $_.StartTime } }
        catch { $notes.Add('Could not read a running application process: ' + $_.Exception.Message) }
    })
try {
    $events = @(Get-WinEvent -FilterHashtable @{
        LogName = 'Application'; StartTime = (Get-Date).AddDays(-2); Id = 1000, 1001, 1026
    } -MaxEvents 200 -ErrorAction Stop)
    $diagnostics['CrashEvents'] = @($events | Where-Object { $_.Message -match 'Naufal.*Powertoys' } |
        Select-Object TimeCreated, ProviderName, Id, Message)
}
catch { $notes.Add('Application event query: ' + $_.Exception.Message) }
$appLog = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Naufal Windows Powertoys\crash.log'
if (Test-Path -LiteralPath $appLog -PathType Leaf) {
    $diagnostics['ManagedCrashLogTail'] = @(Get-Content -LiteralPath $appLog -Tail 150)
}
else { $notes.Add('No application crash.log exists; native faults may not reach managed exception logging.') }
try {
    $diagnostics['EventLogService'] = Get-Service -Name EventLog | Select-Object Name, Status
}
catch { $notes.Add('EventLog service query: ' + $_.Exception.Message) }
. (Join-Path $PSScriptRoot 'Installer\PublishStage.ps1')
try {
    $stage = Get-CompletedPublishStage -PublishRoot (Join-Path $PSScriptRoot 'artifacts\publish') -Version '8.0.0'
    if ($null -ne $stage) {
        $exe = Join-Path $stage.FullName 'Naufal Windows Powertoys.exe'
        $diagnostics['LatestBuild'] = [ordered]@{
            Path = $exe
            SHA256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
            Version = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
        }
    }
}
catch { $notes.Add('Published build verification: ' + $_.Exception.Message) }
$diagnostics['Notes'] += $notes.ToArray()
$outputDirectory = Join-Path $PSScriptRoot 'artifacts\diagnostics'
$null = New-Item -ItemType Directory -Path $outputDirectory -Force
$outputFile = Join-Path $outputDirectory ('crash-diagnostics-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.json')
$diagnostics | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $outputFile -Encoding UTF8
Write-Host ('Diagnostics saved: ' + $outputFile)
Write-Host 'No Windows settings changed. Review the report before sharing; it includes local paths and crash details.'
