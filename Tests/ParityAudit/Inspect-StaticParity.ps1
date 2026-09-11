param(
    [string]$ProjectRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent),
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ReferenceDirectory
)

# Read-only structural evidence, NOT a behavioral certification test.
# Never dot-source the reference, launch an executable, or invoke system tweaks.
$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $ReferenceDirectory 'V78.ps1'
$exePath = Join-Path $ReferenceDirectory 'Naufal Windows Powertoys V7.8.exe'
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    $scriptPath, [ref]$tokens, [ref]$parseErrors)
$functions = @($ast.FindAll({ param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst]
}, $true))

$xaml = Get-Content -LiteralPath (Join-Path $ProjectRoot 'MainWindow.xaml') -Raw
$code = Get-Content -LiteralPath (Join-Path $ProjectRoot 'MainWindow.xaml.cs') -Raw
$routeNames = @(
    'FullRepair', 'QuickRepair', 'WindowsUpdateFix', 'MicrosoftStoreFix',
    'ExplorerFix', 'DiskInfo', 'SystemReport', 'WindowsActivation',
    'OfficeActivation', 'DisableDefender', 'RestoreDefender', 'BitLockerManager',
    'SmartAppControl', 'EssentialTweaks', 'GamingTweaks', 'RuntimeCompatibility',
    'GpuDriverManager', 'Debloat', 'MsiModeUtility', 'LegacyWindowsPanels'
)
$routes = @($routeNames | ForEach-Object {
    $handler = $_ + 'Button_Click'
    [pscustomobject]@{
        Route = $_
        XamlConnected = $xaml.Contains('Click="' + $handler + '"')
        HandlerDefined = [regex]::IsMatch($code, '\bvoid\s+' + $handler + '\s*\(')
    }
})

# Decode only the native catalog's literal binary data, not C# or PowerShell code.
$catalog = @{}
foreach ($sourceName in @('UiTranslationCatalog.cs', 'SupplementalUiCatalog.cs')) {
    $translationSource = Get-Content -LiteralPath (Join-Path $ProjectRoot $sourceName) -Raw
    $field = if ($sourceName -eq 'UiTranslationCatalog.cs') { 'CatalogData' } else { 'Data' }
    $literal = [regex]::Match($translationSource, '(?s)private const string ' + $field + '\s*=\s*(.*?);')
    if (-not $literal.Success) { throw "$field declaration not found." }
    $fragments = [regex]::Matches($literal.Groups[1].Value, '"([A-Za-z0-9+/=]+)"')
    $payload = ($fragments | ForEach-Object { $_.Groups[1].Value }) -join ''
    $stream = [IO.MemoryStream]::new([Convert]::FromBase64String($payload), $false)
    $gzip = [IO.Compression.GZipStream]::new($stream, [IO.Compression.CompressionMode]::Decompress)
    $reader = [IO.BinaryReader]::new($gzip, [Text.Encoding]::UTF8)
    try {
        $languageCount = $reader.ReadInt32()
        for ($languageIndex = 0; $languageIndex -lt $languageCount; $languageIndex++) {
            $language = $reader.ReadString()
            $entryCount = $reader.ReadInt32()
            if (-not $catalog.ContainsKey($language)) {
                $catalog[$language] = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
            }
            $table = $catalog[$language]
            for ($entryIndex = 0; $entryIndex -lt $entryCount; $entryIndex++) {
                $key = $reader.ReadString()
                $table[$key] = $reader.ReadString()
            }
        }
    } finally { $reader.Dispose(); $gzip.Dispose(); $stream.Dispose() }
}
# Read the editable UTF-8 additions as literal data; never evaluate C# code.
foreach ($sourceFile in Get-ChildItem -LiteralPath $ProjectRoot -Filter 'NativeUiCatalog*.cs' -File) {
    $sourceText = Get-Content -LiteralPath $sourceFile.FullName -Raw -Encoding UTF8
    foreach ($block in [regex]::Matches($sourceText, '(?s)private const string \w+\s*=\s*"""\r?\n(.*?)\r?\n""";')) {
        $rows = @($block.Groups[1].Value -split '\r?\n' | Where-Object { $_.Length -gt 0 })
        $keys = $rows[0].Split('|')
        if ($keys[0] -ne 'en' -or $rows.Count -ne 23) { throw "Invalid localization matrix: $($sourceFile.Name)" }
        foreach ($row in $rows) {
            $cells = $row.Split('|')
            if ($cells.Count -ne $keys.Count -or -not $catalog.ContainsKey($cells[0])) {
                throw "Invalid localization row: $($sourceFile.Name) / $($cells[0])"
            }
            for ($i = 1; $i -lt $keys.Count; $i++) { $catalog[$cells[0]][$keys[$i]] = $cells[$i] }
        }
    }
}
$nativeSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'NativeUiCatalog.cs') -Raw -Encoding UTF8
foreach ($table in $catalog.Values) {
    foreach ($removed in [regex]::Matches($nativeSource, 'table\.Remove\("([^"]+)"\)')) {
        [void]$table.Remove($removed.Groups[1].Value)
    }
    foreach ($alias in [regex]::Matches($nativeSource, '\["([^"]+)"\]\s*=\s*"([^"]+)"')) {
        if ($table.ContainsKey($alias.Groups[2].Value)) { $table[$alias.Groups[1].Value] = $table[$alias.Groups[2].Value] }
    }
}
$languages = @($catalog.Keys | Sort-Object | ForEach-Object {
    [pscustomobject]@{ Code = $_; Entries = $catalog[$_].Count }
})
$samples = @('Repair selected', 'Enable selected', 'Download & install',
    'Analyze / reload', 'Select safe', 'Apply selected', 'System Report')
$coverage = @($samples | ForEach-Object {
    $label = $_
    $translated = @($languages | Where-Object {
        $_.Code -ne 'en' -and $catalog[$_.Code].ContainsKey($label) -and
        $catalog[$_.Code][$label] -cne $label
    })
    [pscustomobject]@{ Label = $label; NonEnglishExactTranslations = $translated.Count
        NonEnglishEntries = @($languages | Where-Object { $_.Code -ne 'en' -and $catalog[$_.Code].ContainsKey($label) }).Count }
})
[xml]$project = Get-Content -LiteralPath (Join-Path $ProjectRoot "Naufal Tech's Windows Powertoys.csproj") -Raw
[xml]$manifest = Get-Content -LiteralPath (Join-Path $ProjectRoot 'Package.appxmanifest') -Raw
$inno = Get-Content -LiteralPath (Join-Path $ProjectRoot 'Installer\NaufalWindowsPowertoys.iss') -Raw
[pscustomobject]@{
    EvidenceKind = 'Static only; connected handlers and catalog entries do not prove behavior'
    LocalizationEvidence = 'Merged reference and native UTF-8 resources; comprehensive engine tests and candidate backlog are in Tests/Localization'
    ReferenceScriptSHA256 = (Get-FileHash -LiteralPath $scriptPath -Algorithm SHA256).Hash
    ReferenceExeSHA256 = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash
    ProjectScriptSHA256 = (Get-FileHash -LiteralPath (Join-Path $ProjectRoot 'V78.ps1') -Algorithm SHA256).Hash
    ParseErrors = @($parseErrors).Count
    FunctionDefinitions = $functions.Count
    RepeatedFunctionNames = @($functions | Group-Object Name | Where-Object Count -gt 1).Count
    Routes = $routes
    Languages = $languages
    SampleCoverage = $coverage
    Company = @($project.Project.PropertyGroup.Company | Where-Object { $_ })
    PackagePublisherDisplayName = [string]$manifest.Package.Properties.PublisherDisplayName
    PackageSigningIdentity = [string]$manifest.Package.Identity.Publisher
    InstallerPublisherDefined = $inno.Contains('#define AppPublisher "Naufal Tech''s Ltd."')
    InstallerCompanyLinked = $inno.Contains('VersionInfoCompany={#AppPublisher}')
} | ConvertTo-Json -Depth 5
