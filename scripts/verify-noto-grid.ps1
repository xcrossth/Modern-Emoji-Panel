[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solutionPath = Join-Path $repositoryRoot "ModernEmojiPanel.sln"
$projectPath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\EmojiPicker.csproj"
$baselinePath = Join-Path $repositoryRoot "data\emoji-baseline\17.0\emoji.json"
$outputRoot = Join-Path $repositoryRoot "apps\picker\EmojiPicker\bin\Release\net10.0-windows\win-x64"
$assetRoot = Join-Path $outputRoot "EmojiBaseline"
$executablePath = Join-Path $outputRoot "ModernEmojiPicker.exe"

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

Push-Location $repositoryRoot
try {
    if (-not $SkipBuild) {
        & dotnet restore $solutionPath --locked-mode
        if ($LASTEXITCODE -ne 0) {
            throw "Locked restore failed"
        }

        & dotnet build $solutionPath --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "Release build failed"
        }
    }

    $baseline = Get-Content -Raw -LiteralPath $baselinePath | ConvertFrom-Json
    $entries = @($baseline.entries)
    Assert-Condition ($entries.Count -eq 3944) "Expected 3944 fully-qualified Emoji 17 entries"
    Assert-Condition (@($entries.id | Sort-Object -Unique).Count -eq $entries.Count) "Emoji IDs are not unique"
    Assert-Condition (@($entries.canonicalSequence | Sort-Object -Unique).Count -eq $entries.Count) "Canonical Emoji sequences are not unique"

    $expectedGroups = @(
        "Activities",
        "Animals & Nature",
        "Flags",
        "Food & Drink",
        "Objects",
        "People & Body",
        "Smileys & Emotion",
        "Symbols",
        "Travel & Places"
    )
    $actualGroups = @($entries.group | Sort-Object -Unique)
    Assert-Condition (($actualGroups -join "|") -eq ($expectedGroups -join "|")) "Standard category mapping is incomplete"

    $resolvedAssetRoot = [System.IO.Path]::GetFullPath($assetRoot) + [System.IO.Path]::DirectorySeparatorChar
    $missingAssets = New-Object System.Collections.Generic.List[string]
    foreach ($entry in $entries) {
        $relativePath = [string]$entry.asset.png128
        $assetPath = [System.IO.Path]::GetFullPath((Join-Path $assetRoot ($relativePath -replace "/", "\")))
        Assert-Condition ($assetPath.StartsWith($resolvedAssetRoot, [System.StringComparison]::OrdinalIgnoreCase)) "Asset path escapes the bundle root"
        if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
            $missingAssets.Add($relativePath)
        }
    }
    Assert-Condition ($missingAssets.Count -eq 0) "Bundled grid artwork is missing for $($missingAssets.Count) entries"

    $projectText = Get-Content -Raw -LiteralPath $projectPath
    Assert-Condition ($projectText -notmatch "Emoji\.Wpf") "Picker still references Emoji.Wpf"
    Assert-Condition (Test-Path -LiteralPath $executablePath) "Release executable is missing"

    $smoke = Start-Process `
        -FilePath $executablePath `
        -ArgumentList "--foundation-smoke" `
        -Wait `
        -PassThru `
        -WindowStyle Hidden
    Assert-Condition ($smoke.ExitCode -eq 0) "Noto grid smoke failed with exit code $($smoke.ExitCode)"

    & (Join-Path $PSScriptRoot "verify-flag-assets.ps1") -SkipBuild -AssetRoot $assetRoot

    Write-Host "Noto grid verification passed: $($entries.Count) entries, $($actualGroups.Count) categories, DPI 100-250%, bounded lazy decode" -ForegroundColor Green
}
finally {
    Pop-Location
}
