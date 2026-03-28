param(
    [string]$AssetsDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Tokenizer.App\Assets')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$backgroundColor = [System.Drawing.Color]::FromArgb(255, 53, 183, 255)
$outlineColor = [System.Drawing.Color]::FromArgb(255, 11, 13, 16)
$glyphColor = [System.Drawing.Color]::FromArgb(255, 245, 247, 250)

function New-RoundedRectPath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = [Math]::Min([Math]::Min($Width, $Height), $Radius * 2)
    if ($diameter -le 0) {
        $path.AddRectangle([System.Drawing.RectangleF]::new($X, $Y, $Width, $Height))
        return $path
    }

    $right = $X + $Width - $diameter
    $bottom = $Y + $Height - $diameter

    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($right, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($right, $bottom, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $bottom, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-PlaceholderBitmap {
    param(
        [int]$Width,
        [int]$Height
    )

    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $canvasMin = [float][Math]::Min($Width, $Height)
        $plateScale = if ($Width -gt $Height) { 0.74 } else { 0.82 }
        $plateSize = [float][Math]::Round($canvasMin * $plateScale)
        $plateX = [float](($Width - $plateSize) / 2.0)
        $plateY = [float](($Height - $plateSize) / 2.0)
        $cornerRadius = [float][Math]::Round($plateSize * 0.22)

        $platePath = New-RoundedRectPath -X $plateX -Y $plateY -Width $plateSize -Height $plateSize -Radius $cornerRadius
        $fillBrush = [System.Drawing.SolidBrush]::new($backgroundColor)
        $borderPen = [System.Drawing.Pen]::new($outlineColor, [float][Math]::Max(1.0, $plateSize * 0.035))

        try {
            $graphics.FillPath($fillBrush, $platePath)
            $graphics.DrawPath($borderPen, $platePath)
        }
        finally {
            $fillBrush.Dispose()
            $borderPen.Dispose()
            $platePath.Dispose()
        }

        $barWidth = [float][Math]::Round($plateSize * 0.58)
        $barHeight = [float][Math]::Max(1.0, [Math]::Round($plateSize * 0.16))
        $stemWidth = [float][Math]::Max(1.0, [Math]::Round($plateSize * 0.18))
        $stemHeight = [float][Math]::Round($plateSize * 0.50)

        $barX = [float]($plateX + (($plateSize - $barWidth) / 2.0))
        $barY = [float]($plateY + ($plateSize * 0.20))
        $stemX = [float]($plateX + (($plateSize - $stemWidth) / 2.0))
        $stemY = [float]($barY + ($barHeight * 0.90))

        $glyphPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $glyphPath.AddPath((New-RoundedRectPath -X $barX -Y $barY -Width $barWidth -Height $barHeight -Radius ($barHeight * 0.45)), $false)
        $glyphPath.AddPath((New-RoundedRectPath -X $stemX -Y $stemY -Width $stemWidth -Height $stemHeight -Radius ($stemWidth * 0.45)), $false)

        $glyphBrush = [System.Drawing.SolidBrush]::new($glyphColor)
        try {
            $graphics.FillPath($glyphBrush, $glyphPath)
        }
        finally {
            $glyphBrush.Dispose()
            $glyphPath.Dispose()
        }

        return $bitmap
    }
    catch {
        $graphics.Dispose()
        $bitmap.Dispose()
        throw
    }
    finally {
        if ($graphics) {
            $graphics.Dispose()
        }
    }
}

function Save-PngAsset {
    param(
        [string]$FileName,
        [int]$Width,
        [int]$Height
    )

    $bitmap = New-PlaceholderBitmap -Width $Width -Height $Height
    try {
        $bitmap.Save((Join-Path $AssetsDirectory $FileName), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

function Write-IcoFile {
    param(
        [string]$FilePath,
        [int[]]$Sizes
    )

    $entries = @()
    foreach ($size in $Sizes) {
        $bitmap = New-PlaceholderBitmap -Width $size -Height $size
        $memoryStream = [System.IO.MemoryStream]::new()
        try {
            $bitmap.Save($memoryStream, [System.Drawing.Imaging.ImageFormat]::Png)
            $entries += [pscustomobject]@{
                Size = $size
                Data = $memoryStream.ToArray()
            }
        }
        finally {
            $memoryStream.Dispose()
            $bitmap.Dispose()
        }
    }

    $fileStream = [System.IO.File]::Open($FilePath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
    $writer = [System.IO.BinaryWriter]::new($fileStream)

    try {
        # ICO directory header + entry table followed by PNG payloads.
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$entries.Count)

        $offset = 6 + (16 * $entries.Count)
        foreach ($entry in $entries) {
            $dimension = if ($entry.Size -ge 256) { [byte]0 } else { [byte]$entry.Size }
            $writer.Write($dimension)
            $writer.Write($dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$entry.Data.Length)
            $writer.Write([UInt32]$offset)
            $offset += $entry.Data.Length
        }

        foreach ($entry in $entries) {
            $writer.Write($entry.Data)
        }
    }
    finally {
        $writer.Dispose()
        $fileStream.Dispose()
    }
}

New-Item -ItemType Directory -Force -Path $AssetsDirectory | Out-Null

$pngAssets = @(
    @{ FileName = 'Square44x44Logo.png'; Width = 44; Height = 44 }
    @{ FileName = 'Square71x71Logo.png'; Width = 71; Height = 71 }
    @{ FileName = 'Square150x150Logo.png'; Width = 150; Height = 150 }
    @{ FileName = 'Wide310x150Logo.png'; Width = 310; Height = 150 }
    @{ FileName = 'StoreLogo.png'; Width = 50; Height = 50 }
    @{ FileName = 'SplashScreen.png'; Width = 620; Height = 300 }
)

foreach ($asset in $pngAssets) {
    Save-PngAsset -FileName $asset.FileName -Width $asset.Width -Height $asset.Height
}

Write-IcoFile -FilePath (Join-Path $AssetsDirectory 'AppIcon.ico') -Sizes @(16, 20, 24, 32, 40, 48, 64, 256)

Write-Host "Generated placeholder icon assets in $AssetsDirectory"
