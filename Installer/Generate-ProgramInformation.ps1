[CmdletBinding()]
param([Parameter(Mandatory)][string]$ProjectRoot, [Parameter(Mandatory)][string]$OutputFile, [Parameter(Mandatory)][string]$Version)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$source = Get-Content -LiteralPath (Join-Path $ProjectRoot 'EnglishUiText.cs') -Raw -Encoding UTF8
function Read-Copy([string]$Name) {
    $match = [regex]::Match($source, $Name + '\s*=\s*"([^"\r\n]+)";')
    if (!$match.Success) { throw "Missing English program information: $Name" }
    return $match.Groups[1].Value
}
function Pascal([string]$Text) { return "'" + $Text.Replace("'", "''") + "'" }
$lines = @('Naufal Windows Utility', "Version $Version", '', (Read-Copy 'AboutDescription'), '',
    (Read-Copy 'AboutPurpose'), '', 'Developed by: Muhammad Naufal Alauddin', 'Open-Source Software',
    'Licensed under the MIT License', '', 'Source Code', 'https://github.com/mnaufalalauddin/Naufal-Tech-s-Windows-Powertoys')
$expression = ($lines | ForEach-Object { Pascal $_ }) -join ' + #13#10 + '
$output = @('// Generated from EnglishUiText.cs. Do not edit.', 'procedure LoadProgramInformation;', 'begin',
    "  ProgramInfoPage.Caption := 'About';", ('  ProgramInfoText.Text := ' + $expression + ';'),
    '  ProgramInfoText.Alignment := taLeftJustify;', 'end;')
$null = New-Item -ItemType Directory -Path (Split-Path -Parent $OutputFile) -Force
[IO.File]::WriteAllLines($OutputFile, $output, [Text.UTF8Encoding]::new($false))
