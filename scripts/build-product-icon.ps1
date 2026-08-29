[CmdletBinding()]
param(
    [switch]$VerifyOnly
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$sourcePath = Join-Path $repositoryRoot "design\brand\modern-emoji-picker-master.png"
$generationPath = Join-Path $repositoryRoot "design\brand\icon-generation.json"
$iconPath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\Resources\modern-emoji-picker.ico"
$previewPath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\Resources\modern-emoji-picker-512.png"
$iconSizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function New-ResizedPngBytes {
    param(
        [System.Drawing.Image]$Source,
        [int]$Size
    )

    $bitmap = [System.Drawing.Bitmap]::new(
        $Size,
        $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.DrawImage($Source, 0, 0, $Size, $Size)
        }
        finally {
            $graphics.Dispose()
        }

        $stream = [System.IO.MemoryStream]::new()
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            return $stream.ToArray()
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

function Read-IcoSizes {
    param([string]$Path)

    $stream = [System.IO.File]::OpenRead($Path)
    $reader = [System.IO.BinaryReader]::new($stream)
    try {
        Assert-Condition ($reader.ReadUInt16() -eq 0) "ICO reserved field is invalid"
        Assert-Condition ($reader.ReadUInt16() -eq 1) "ICO type is not icon"
        $count = $reader.ReadUInt16()
        $sizes = for ($index = 0; $index -lt $count; $index++) {
            $width = [int]$reader.ReadByte()
            $height = [int]$reader.ReadByte()
            $null = $reader.ReadByte()
            $null = $reader.ReadByte()
            $null = $reader.ReadUInt16()
            $bits = $reader.ReadUInt16()
            $null = $reader.ReadUInt32()
            $null = $reader.ReadUInt32()
            Assert-Condition ($bits -eq 32) "ICO frame is not 32-bit"
            if ($width -eq 0) { $width = 256 }
            if ($height -eq 0) { $height = 256 }
            Assert-Condition ($width -eq $height) "ICO frame is not square"
            $width
        }

        return @($sizes)
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

Add-Type -AssemblyName System.Drawing
Assert-Condition (Test-Path -LiteralPath $sourcePath) "Product icon master is missing"
Assert-Condition (Test-Path -LiteralPath $generationPath) "Product icon generation metadata is missing"

$generation = Get-Content -Raw -LiteralPath $generationPath | ConvertFrom-Json
$sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash.ToLowerInvariant()
Assert-Condition ($sourceHash -eq [string]$generation.sourceSha256) "Product icon master hash does not match icon-generation.json"

if (-not $VerifyOnly) {
    $source = [System.Drawing.Bitmap]::FromFile($sourcePath)
    try {
        Assert-Condition ($source.Width -eq $source.Height) "Product icon master must be square"
        Assert-Condition ($source.Width -ge 1024) "Product icon master must be at least 1024 px"
        Assert-Condition ($source.GetPixel(0, 0).A -eq 0) "Product icon master must have transparent corners"

        $frames = @($iconSizes | ForEach-Object {
            [pscustomobject]@{ Size = $_; Bytes = New-ResizedPngBytes -Source $source -Size $_ }
        })
        $headerLength = 6 + (16 * $frames.Count)
        $offset = $headerLength

        $iconDirectory = Split-Path -Parent $iconPath
        New-Item -ItemType Directory -Path $iconDirectory -Force | Out-Null
        $stream = [System.IO.File]::Open($iconPath, [System.IO.FileMode]::Create)
        $writer = [System.IO.BinaryWriter]::new($stream)
        try {
            $writer.Write([uint16]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]$frames.Count)
            foreach ($frame in $frames) {
                $encodedSize = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
                $writer.Write([byte]$encodedSize)
                $writer.Write([byte]$encodedSize)
                $writer.Write([byte]0)
                $writer.Write([byte]0)
                $writer.Write([uint16]1)
                $writer.Write([uint16]32)
                $writer.Write([uint32]$frame.Bytes.Length)
                $writer.Write([uint32]$offset)
                $offset += $frame.Bytes.Length
            }

            foreach ($frame in $frames) {
                $writer.Write([byte[]]$frame.Bytes)
            }
        }
        finally {
            $writer.Dispose()
            $stream.Dispose()
        }

        [System.IO.File]::WriteAllBytes(
            $previewPath,
            (New-ResizedPngBytes -Source $source -Size 512))
    }
    finally {
        $source.Dispose()
    }
}

Assert-Condition (Test-Path -LiteralPath $iconPath) "Generated product ICO is missing"
Assert-Condition (Test-Path -LiteralPath $previewPath) "Generated 512 px product preview is missing"
$actualSizes = Read-IcoSizes -Path $iconPath
Assert-Condition ($actualSizes.Count -eq $iconSizes.Count) "Product ICO frame count is incorrect"
Assert-Condition (-not (Compare-Object $iconSizes $actualSizes)) "Product ICO sizes are incorrect"

$preview = [System.Drawing.Bitmap]::FromFile($previewPath)
try {
    Assert-Condition ($preview.Width -eq 512 -and $preview.Height -eq 512) "Product preview must be 512x512"
    Assert-Condition ($preview.GetPixel(0, 0).A -eq 0) "Product preview must preserve transparent corners"
}
finally {
    $preview.Dispose()
}

Write-Host "Product icon verification passed: $($iconSizes -join ', ') px ICO frames and 512 px preview" -ForegroundColor Green
