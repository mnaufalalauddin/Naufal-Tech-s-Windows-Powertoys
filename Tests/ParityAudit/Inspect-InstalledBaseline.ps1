param(
    [string]$InstalledDirectory = "C:\Program Files\Naufal Tech's Limited\Naufal Windows Powertoys",
    [string]$PublishRoot = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'artifacts\publish')
)

# Read-only payload audit. Never launch, install, repair, or change Program Files.
$ErrorActionPreference = 'Stop'
$installed = (Get-Item -LiteralPath $InstalledDirectory).FullName.TrimEnd('\')
$exe = Get-Item -LiteralPath (Join-Path $installed 'Naufal Windows Powertoys.exe')
$exeHash = (Get-FileHash -LiteralPath $exe.FullName -Algorithm SHA256).Hash
$matchingStage = $null
foreach ($file in Get-ChildItem -LiteralPath $PublishRoot -Filter 'publish-complete.json' -File -Recurse) {
    $record = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    if ($record.Sha256 -eq $exeHash) {
        $stageExe = Join-Path $file.DirectoryName $exe.Name
        if ((Test-Path -LiteralPath $stageExe -PathType Leaf) -and
            (Get-FileHash -LiteralPath $stageExe -Algorithm SHA256).Hash -eq $exeHash) {
            $matchingStage = $file.DirectoryName
            break
        }
    }
}
$mismatch = @()
$missing = @()
$compared = 0
if ($matchingStage) {
    foreach ($file in Get-ChildItem -LiteralPath $installed -File -Recurse) {
        # Inno creates these on installation; they are not application payload.
        if ($file.Name -match '^unins\d+\.(exe|dat)$') { continue }
        $relative = $file.FullName.Substring($installed.Length + 1)
        $expected = Join-Path $matchingStage $relative
        $compared++
        if (-not (Test-Path -LiteralPath $expected -PathType Leaf)) { $mismatch += $relative; continue }
        if ((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $expected -Algorithm SHA256).Hash) { $mismatch += $relative }
    }
    foreach ($file in Get-ChildItem -LiteralPath $matchingStage -File -Recurse) {
        if ($file.Extension -eq '.pdb') { continue } # Deliberate installer exclusion.
        $relative = $file.FullName.Substring($matchingStage.Length + 1)
        if (-not (Test-Path -LiteralPath (Join-Path $installed $relative) -PathType Leaf)) { $missing += $relative }
    }
}
[pscustomobject]@{
    EvidenceKind = 'Read-only binary/payload identity; NOT UI or behavior certification'
    InstalledDirectory = $installed
    FileVersion = $exe.VersionInfo.FileVersion
    Company = $exe.VersionInfo.CompanyName
    ProductName = $exe.VersionInfo.ProductName
    SHA256 = $exeHash
    MatchingPublishStage = $matchingStage
    ComparedFiles = $compared
    MismatchedFiles = $mismatch
    MissingPayloadFiles = $missing
    PayloadMatchesStage = ($null -ne $matchingStage -and $mismatch.Count -eq 0 -and $missing.Count -eq 0)
} | ConvertTo-Json -Depth 4
