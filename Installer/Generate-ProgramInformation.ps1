[CmdletBinding()]
param([Parameter(Mandatory)][string]$ProjectRoot, [Parameter(Mandatory)][string]$OutputFile, [Parameter(Mandatory)][string]$Version)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# Use the same reviewed resources as the application; never maintain another
# translation copy in the installer. This generates Pascal data, not executable input.
$source = Get-Content -LiteralPath (Join-Path $ProjectRoot 'NativeUiCatalog.About.cs') -Raw -Encoding UTF8
$match = [regex]::Match($source, 'AboutContent\s*=\s*"""\s*\r?\n(?<data>[\s\S]*?)\r?\n""";')
if (!$match.Success) { throw 'About localization matrix was not found.' }
$names = [ordered]@{en='English';id='Bahasa Indonesia';de='Deutsch';fr='Français';ar='العربية';tl='Tagalog';vi='Tiếng Việt';'zh-CN'='简体中文';'zh-TW'='繁體中文';th='ไทย';ru='Русский';uk='Українська';pt='Português';ja='日本語';ko='한국어';ur='اردو';ta='தமிழ்';hi='हिन्दी';ms='Bahasa Melayu';jv='Basa Jawa';ban='Basa Bali';sv='Svenska';es='Español'}
$rows = @($match.Groups['data'].Value -split '\r?\n' | Where-Object { $_.Trim().Length -gt 0 })
if ($rows.Count -ne 23) { throw 'Installer About requires all 23 languages.' }
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
function Pascal([string]$Text) { return "'" + $Text.Replace("'", "''") + "'" }
$output = [Collections.Generic.List[string]]::new()
$output.Add('// Generated from NativeUiCatalog.About.cs. Do not edit.')
$output.Add('procedure LoadProgramInformation(Index: Integer);')
$output.Add('begin')
$output.Add('  case Index of')
$index = 0
foreach ($row in $rows) {
    $cells = $row.Split('|')
    if ($cells.Count -ne 9 -or !$names.Contains($cells[0]) -or !$seen.Add($cells[0])) { throw 'Invalid installer About resource row.' }
    $text = @('Naufal Windows Powertoys', $cells[6].Replace('{0}', $Version), '', $cells[7], '', $cells[8], '', ($cells[2] + ': Muhammad Naufal Alauddin'), $cells[3], $cells[4], '', $cells[5], 'https://github.com/mnaufalalauddin/Naufal-Tech-s-Windows-Powertoys')
    $expression = ($text | ForEach-Object { Pascal $_ }) -join ' + #13#10 + '
    $output.Add("    $index`: begin")
    $output.Add('      ProgramInfoPage.Caption := ' + (Pascal $cells[1]) + ';')
    $output.Add('      ProgramInfoText.Text := ' + $expression + ';')
    $alignment = if ($cells[0] -in @('ar','ur')) { 'taRightJustify' } else { 'taLeftJustify' }
    $output.Add('      ProgramInfoText.Alignment := ' + $alignment + ';')
    $output.Add('    end;')
    $index++
}
$output.Add('  end;')
$output.Add('end;')
$output.Add('procedure LoadProgramLanguages;')
$output.Add('begin')
$index = 0
foreach ($row in $rows) {
    $code = $row.Split('|')[0]
    $output.Add('  ProgramInfoLanguage.Items.Add(' + (Pascal $names[$code]) + ');')
    $output.Add('  if CompareText(ActiveLanguage, ' + (Pascal $code) + ") = 0 then ProgramInfoLanguage.ItemIndex := $index;")
    $index++
}
$output.Add('end;')
$null = New-Item -ItemType Directory -Path (Split-Path -Parent $OutputFile) -Force
[IO.File]::WriteAllLines($OutputFile, $output, [Text.UTF8Encoding]::new($false))
