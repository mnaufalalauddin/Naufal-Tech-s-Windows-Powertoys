[CmdletBinding()]
param(
    [string]$SourceImage = (Join-Path $PSScriptRoot '..\Assets\BrandingSource.png')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Assets'))
$source = [Drawing.Image]::FromFile((Resolve-Path -LiteralPath $SourceImage).Path)

function New-IconPng([int]$Width, [int]$Height) {
    $bitmap = [Drawing.Bitmap]::new($Width, $Height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $attributes = [Drawing.Imaging.ImageAttributes]::new()
    $stream = [IO.MemoryStream]::new()
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $attributes.SetWrapMode([Drawing.Drawing2D.WrapMode]::TileFlipXY)
        # Fit the entire supplied image: no crop, stretching, or redesign.
        $ratio = [Math]::Min($Width / $source.Width, $Height / $source.Height)
        $drawWidth = [int][Math]::Round($source.Width * $ratio)
        $drawHeight = [int][Math]::Round($source.Height * $ratio)
        $destination = [Drawing.Rectangle]::new(
            [int](($Width - $drawWidth) / 2), [int](($Height - $drawHeight) / 2), $drawWidth, $drawHeight)
        $graphics.DrawImage($source, $destination, 0, 0, $source.Width, $source.Height,
            [Drawing.GraphicsUnit]::Pixel, $attributes)
        $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
        return ,$stream.ToArray()
    }
    finally {
        $stream.Dispose()
        $attributes.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

try {
    $sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
    $frames = @($sizes | ForEach-Object { ,(New-IconPng $_ $_) })
    $iconStream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($iconStream)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($index = 0; $index -lt $sizes.Count; $index++) {
            $dimension = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$index].Length)
            $writer.Write([uint32]$offset)
            $offset += $frames[$index].Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
        $writer.Flush()
        [IO.File]::WriteAllBytes((Join-Path $assetDirectory 'NaufalWindowsPowertoys.ico'), $iconStream.ToArray())
    }
    finally { $writer.Dispose(); $iconStream.Dispose() }

    # Existing manifest filenames and sizes remain stable for packaged builds.
    $assets = @(
        @('Square44x44Logo.scale-200.png', 88, 88),
        @('Square44x44Logo.targetsize-24_altform-unplated.png', 24, 24),
        @('Square150x150Logo.scale-200.png', 300, 300),
        @('StoreLogo.png', 50, 50),
        @('Wide310x150Logo.scale-200.png', 620, 300),
        @('SplashScreen.scale-200.png', 1240, 600),
        @('LockScreenLogo.scale-200.png', 48, 48)
    )
    foreach ($asset in $assets) {
        [IO.File]::WriteAllBytes((Join-Path $assetDirectory $asset[0]), (New-IconPng $asset[1] $asset[2]))
    }
    Write-Output "Generated 10 ICO resolutions and 7 package assets from $SourceImage."
}
finally { $source.Dispose() }
