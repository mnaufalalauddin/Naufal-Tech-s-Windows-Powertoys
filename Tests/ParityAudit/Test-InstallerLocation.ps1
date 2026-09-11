# Read-only static checks: no Setup, uninstaller or Windows mutation is executed.
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$scriptText = Get-Content -LiteralPath (Join-Path $root 'Installer\NaufalWindowsPowertoys.iss') -Raw
$checks = [ordered]@{
    'Company parent folder' = $scriptText.Contains("DefaultDirName={autopf}\Naufal Tech's Limited\Naufal Windows Powertoys")
    'Old location cannot override default' = $scriptText.Contains('UsePreviousAppDir=no')
    'Existing application identity retained' = $scriptText.Contains('AppId={{A75F9775-AC15-4F03-8931-43D04EA6B032}')
    'Publisher unchanged' = $scriptText.Contains('#define AppPublisher "Naufal Tech''s Ltd."')
    '64-bit Program Files' = $scriptText.Contains('ArchitecturesInstallIn64BitMode=x64compatible')
    'Migration guard before writes' = $scriptText.Contains('function PrepareToInstall(var NeedsRestart: Boolean): String;')
    'Reads existing x64 registration' = $scriptText.Contains('RegQueryStringValue(HKLM64,') -and $scriptText.Contains("'Inno Setup: App Path', PreviousInstallDir")
    'Compares normalized paths' = $scriptText.Contains('CompareText(RemoveBackslash(ExpandFileName(PreviousInstallDir)),')
    'Normal uninstall required for relocation' = $scriptText.Contains('the existing version from Windows Settings > Apps before installing in the new folder.')
    'Payload follows selected app directory' = $scriptText.Contains('DestDir: "{app}";')
    'Shortcuts follow selected app directory' = $scriptText.Contains('Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{#AppIconFile}";')
    'No scripted file deletion or automatic uninstall' = $scriptText -notmatch '(?im)^\s*(\[InstallDelete\]|DelTree\s*\(|DeleteFile\s*\(|Exec\s*\(|ShellExec\s*\()'
}
foreach ($check in $checks.GetEnumerator()) {
    if (-not $check.Value) { throw "Installer-location regression: $($check.Key)" }
}
Write-Output "PASS: $($checks.Count) static installer-location assertions. Native installation/migration not executed."
