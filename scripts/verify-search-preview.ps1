[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solutionPath = Join-Path $repositoryRoot "ModernEmojiPanel.sln"
$projectPath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\EmojiPicker.csproj"
$windowXamlPath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\MainWindow.xaml"
$windowCodePath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\MainWindow.xaml.cs"
$baselinePath = Join-Path $repositoryRoot "data\emoji-baseline\17.0\emoji.json"
$outputRoot = Join-Path $repositoryRoot "apps\picker\EmojiPicker\bin\Release\net10.0-windows\win-x64"
$assetRoot = Join-Path $outputRoot "EmojiBaseline"
$executablePath = Join-Path $outputRoot "ModernEmojiPicker.exe"
$smokeReportPath = Join-Path ([System.IO.Path]::GetTempPath()) ("mep-search-preview-" + [Guid]::NewGuid().ToString("N") + ".json")

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

    Assert-Condition (Test-Path -LiteralPath $executablePath -PathType Leaf) "Release executable is missing"
    $projectText = Get-Content -Raw -LiteralPath $projectPath
    Assert-Condition ($projectText -match 'vendor\\noto-emoji\\v2\.051\\png\\512') "Picker does not bundle the Noto 512 preview role"
    $windowXaml = Get-Content -Raw -LiteralPath $windowXamlPath
    $windowCode = Get-Content -Raw -LiteralPath $windowCodePath
    Assert-Condition ($windowXaml -match '(?s)<Popup x:Name="EmojiPreviewPopup".*?Focusable="False"') "Hover Preview popup may take focus"
    Assert-Condition ($windowXaml -match '(?s)x:Name="PreviewArtwork".*?DecodeSizeDip="160".*?Width="160".*?Height="160"') "Hover Preview is not configured at 160 DIP"
    Assert-Condition ($windowXaml -match 'MouseEnter" Handler="EmojiItem_MouseEnter"') "Emoji tiles do not schedule pointer preview"
    Assert-Condition ($windowXaml -match 'MouseLeave" Handler="EmojiItem_MouseLeave"') "Emoji tiles do not dismiss pointer preview"
    Assert-Condition ($windowCode -match 'e\.Key == Key\.F1') "F1 does not open the focused tile preview"
    Assert-Condition ($windowCode -match 'e\.Key == Key\.F.*ModifierKeys\.Control') "Ctrl+F does not focus search"
    Assert-Condition ($windowCode -match '(?s)private void CommitEmoji\(Emoji emoji, CommitGesture gesture\).*?HidePreview\(\)') "Starting insert does not dismiss preview"

    $baseline = Get-Content -Raw -LiteralPath $baselinePath | ConvertFrom-Json
    $entries = @($baseline.entries)
    Assert-Condition ($entries.Count -eq 3944) "Expected 3944 fully-qualified Emoji 17 entries"

    $resolvedAssetRoot = [System.IO.Path]::GetFullPath($assetRoot) + [System.IO.Path]::DirectorySeparatorChar
    $missingPreviewAssets = New-Object System.Collections.Generic.List[string]
    $canonicalPreviewCount = 0
    $sharedRegionPreviewCount = 0
    foreach ($entry in $entries) {
        $relativePath = [string]$entry.asset.png512
        $assetPath = [System.IO.Path]::GetFullPath((Join-Path $assetRoot ($relativePath -replace "/", "\")))
        Assert-Condition ($assetPath.StartsWith($resolvedAssetRoot, [System.StringComparison]::OrdinalIgnoreCase)) "Preview asset path escapes the bundle root"
        if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
            $missingPreviewAssets.Add($relativePath)
        }

        if ($relativePath -match '/png/512/') {
            $canonicalPreviewCount++
        }
        elseif ($entry.asset.sharedSourceForSizes -eq $true -and $relativePath -match '/region-flags/png/') {
            $sharedRegionPreviewCount++
        }
        else {
            throw "Preview role is neither canonical Noto 512 nor an approved shared region flag: $relativePath"
        }
    }

    Assert-Condition ($missingPreviewAssets.Count -eq 0) "Bundled preview artwork is missing for $($missingPreviewAssets.Count) entries"
    Assert-Condition ($canonicalPreviewCount -eq 3682) "Unexpected canonical Noto 512 preview count: $canonicalPreviewCount"
    Assert-Condition ($sharedRegionPreviewCount -eq 262) "Unexpected shared region-flag preview count: $sharedRegionPreviewCount"

    $smoke = Start-Process `
        -FilePath $executablePath `
        -ArgumentList @("--search-preview-smoke", ('"' + $smokeReportPath + '"')) `
        -Wait `
        -PassThru `
        -WindowStyle Hidden
    Assert-Condition ($smoke.ExitCode -eq 0) "Bilingual search and preview smoke failed with exit code $($smoke.ExitCode)"
    Assert-Condition (Test-Path -LiteralPath $smokeReportPath -PathType Leaf) "Search/preview smoke report is missing"

    $report = Get-Content -Raw -LiteralPath $smokeReportPath | ConvertFrom-Json
    Assert-Condition ($report.catalogEntries -eq 3944) "Smoke did not load the complete Emoji Baseline"
    foreach ($property in @(
        "tierOrderingPassed",
        "englishNamePassed",
        "thaiNamePassed",
        "englishKeywordPassed",
        "thaiKeywordPassed",
        "accessibleNamePassed",
        "previewDetailsPassed",
        "previewDismissed",
        "previewUses512Role"
    )) {
        Assert-Condition ($report.$property -eq $true) "Smoke assertion failed: $property"
    }

    Assert-Condition ($report.hoverOpenDelayMilliseconds -eq 0) "Hover preview does not open immediately"
    Assert-Condition ($report.hoverCloseDelayMilliseconds -eq 150) "Hover preview close grace is not 150 ms"
    Assert-Condition ($report.pointerMoveReusedPopup -eq $true) "Moving between tiles reopened the Hover Preview popup"
    Assert-Condition ($report.previewStayedOpenDuringCloseGrace -eq $true) "Hover Preview closed before its grace period"
    Assert-Condition ($report.previewClosedAfterGrace -eq $true) "Hover Preview did not close after its grace period"
    Assert-Condition ($report.previewDecodedPixelWidth -eq 160) "Preview did not decode the 512 role to 160 physical pixels at 100% DPI"
    Assert-Condition ($report.searchIterations -eq 100) "Search responsiveness sample is incomplete"
    Assert-Condition ($report.searchElapsedMilliseconds -lt 5000) "100 bilingual searches exceeded the 5000 ms non-blocking guardrail"

    Write-Host "Bilingual search/preview verification passed: 3944 entries, four deterministic tiers, 100 searches in $($report.searchElapsedMilliseconds) ms, 3944 preview assets" -ForegroundColor Green
}
finally {
    Pop-Location
    if (Test-Path -LiteralPath $smokeReportPath) {
        Remove-Item -LiteralPath $smokeReportPath -Force
    }
}
