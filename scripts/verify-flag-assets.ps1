[CmdletBinding()]
param(
    [switch]$SkipBuild,

    [string]$AssetRoot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\EmojiPicker.csproj"
$baselinePath = Join-Path $repositoryRoot "data\emoji-baseline\17.0\emoji.json"

if ([string]::IsNullOrWhiteSpace($AssetRoot)) {
    $AssetRoot = Join-Path $repositoryRoot "apps\picker\EmojiPicker\bin\Release\net10.0-windows\win-x64\EmojiBaseline"
}

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

Push-Location $repositoryRoot
try {
    if (-not $SkipBuild) {
        & dotnet build $projectPath --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "Release build failed"
        }
    }

    Add-Type -AssemblyName PresentationCore
    $resolvedAssetRoot = [IO.Path]::GetFullPath($AssetRoot)
    $resolvedAssetRootWithSeparator = $resolvedAssetRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    Assert-Condition (Test-Path -LiteralPath $resolvedAssetRoot -PathType Container) `
        "Flag asset root does not exist: $resolvedAssetRoot"

    $baseline = Get-Content -Raw -LiteralPath $baselinePath | ConvertFrom-Json
    $flags = @($baseline.entries | Where-Object { $_.group -eq "Flags" })
    Assert-Condition ($flags.Count -eq 270) "Expected 270 Emoji 17 flag entries"

    $failures = [Collections.Generic.List[object]]::new()
    foreach ($entry in $flags) {
        $relativePath = [string]$entry.asset.png128
        $path = [IO.Path]::GetFullPath((Join-Path $resolvedAssetRoot ($relativePath -replace "/", "\")))
        Assert-Condition $path.StartsWith($resolvedAssetRootWithSeparator, [StringComparison]::OrdinalIgnoreCase) `
            "Flag asset path escapes the bundle root: $relativePath"
        try {
            $stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
            try {
                $bitmap = [Windows.Media.Imaging.BitmapImage]::new()
                $bitmap.BeginInit()
                $bitmap.CacheOption = [Windows.Media.Imaging.BitmapCacheOption]::OnLoad
                $bitmap.DecodePixelWidth = 32
                $bitmap.StreamSource = $stream
                $bitmap.EndInit()
                $bitmap.Freeze()
                if ($bitmap.PixelWidth -le 0 -or $bitmap.PixelHeight -le 0) {
                    throw "Decoded bitmap has no pixels"
                }
            }
            finally {
                $stream.Dispose()
            }
        }
        catch {
            $failures.Add([pscustomobject]@{
                Text = [string]$entry.text
                Sequence = [string]$entry.canonicalSequence
                Asset = $relativePath
                Error = $_.Exception.Message
            })
        }
    }

    if ($failures.Count -gt 0) {
        $failures | Format-Table Text, Sequence, Asset, Error -AutoSize -Wrap | Out-Host
        throw "WPF could not decode $($failures.Count) of $($flags.Count) bundled flag assets"
    }

    Write-Host "Flag asset verification passed: $($flags.Count) WPF-decodable Emoji 17 flags" -ForegroundColor Green
}
finally {
    Pop-Location
}
