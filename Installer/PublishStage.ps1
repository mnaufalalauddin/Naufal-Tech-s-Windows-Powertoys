Set-StrictMode -Version Latest

function Get-PublishInventory {
    param([Parameter(Mandatory)][string]$Directory)
    $root = [IO.Path]::GetFullPath($Directory).TrimEnd('\', '/')
    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -Force -File -ErrorAction Stop | Sort-Object FullName) {
        $relative = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
        if ($relative -in @('publish-complete.json', 'publish-complete.json.pending')) { continue }
        [pscustomobject]@{
            Path = $relative
            Bytes = $file.Length
            Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256 -ErrorAction Stop).Hash
        }
    }
}

function Complete-PublishStage {
    param([Parameter(Mandatory)][string]$Directory, [Parameter(Mandatory)][string]$Version)
    $application = Join-Path $Directory 'Naufal Windows Powertoys.exe'
    if (-not (Test-Path -LiteralPath $application -PathType Leaf)) {
        throw "Missing published application: $application"
    }
    $inventory = @(Get-PublishInventory -Directory $Directory)
    $metadata = [ordered]@{
        Schema = 2
        Version = $Version
        CompletedUtc = [DateTime]::UtcNow.ToString('o')
        Sha256 = (Get-FileHash -LiteralPath $application -Algorithm SHA256).Hash
        Files = $inventory
    }
    # This marker is created only after the complete publish tree has been copied.
    $marker = Join-Path $Directory 'publish-complete.json'
    $temporaryMarker = Join-Path $Directory 'publish-complete.json.pending'
    $metadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $temporaryMarker -Encoding UTF8
    Move-Item -LiteralPath $temporaryMarker -Destination $marker -Force
}

function Get-CompletedPublishStage {
    param([Parameter(Mandatory)][string]$PublishRoot, [Parameter(Mandatory)][string]$Version)
    $stages = @(Get-ChildItem -LiteralPath $PublishRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'win-x64-*' } | Sort-Object Name -Descending)
    foreach ($stage in $stages) {
        try {
            $marker = Join-Path $stage.FullName 'publish-complete.json'
            $application = Join-Path $stage.FullName 'Naufal Windows Powertoys.exe'
            if (-not (Test-Path -LiteralPath $marker -PathType Leaf) -or
                -not (Test-Path -LiteralPath $application -PathType Leaf)) { continue }
            $metadata = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
            # Old EXE-only markers cannot prove that DLLs, PRI and assets match.
            if ($metadata.Schema -ne 2 -or $metadata.Version -ne $Version) { continue }
            $actual = @(Get-PublishInventory -Directory $stage.FullName)
            $expected = @($metadata.Files)
            if ($actual.Count -ne $expected.Count -or $actual.Count -eq 0) { continue }
            $valid = $true
            for ($index = 0; $index -lt $actual.Count; $index++) {
                if ($actual[$index].Path -cne $expected[$index].Path -or
                    $actual[$index].Bytes -ne $expected[$index].Bytes -or
                    $actual[$index].Sha256 -cne $expected[$index].Sha256) {
                    $valid = $false
                    break
                }
            }
            if (-not $valid -or
                (@($actual | Where-Object Path -EQ 'Naufal Windows Powertoys.exe')[0].Sha256 -cne $metadata.Sha256)) { continue }
            return $stage
        } catch {
            # Incomplete/corrupt stages are not installer inputs. Try an older complete stage.
            continue
        }
    }
    return $null
}
