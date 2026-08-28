[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solutionPath = Join-Path $repositoryRoot "ModernEmojiPanel.sln"
$testProjectPath = Join-Path $repositoryRoot "tests\EmojiPicker.DomainTests\EmojiPicker.DomainTests.csproj"
$activityCodePath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\ActivityDataStore.cs"
$windowCodePath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\MainWindow.xaml.cs"

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
        if ($LASTEXITCODE -ne 0) { throw "Locked restore failed" }
        & dotnet build $solutionPath --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Release build failed" }
    }

    & dotnet run `
        --project $testProjectPath `
        --configuration Release `
        --no-build `
        --no-restore `
        -- $repositoryRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Activity Data domain verification failed"
    }

    $activityCode = Get-Content -Raw -LiteralPath $activityCodePath
    $windowCode = Get-Content -Raw -LiteralPath $windowCodePath
    Assert-Condition ($activityCode -match 'MaxRecentEntries = 50') "Recent is not bounded to 50 entries"
    Assert-Condition ($activityCode -match 'RankingHalfLife = TimeSpan\.FromDays\(90\)') "Learned Ranking half-life is not 90 days"
    Assert-Condition ($activityCode -match 'File\.Replace') "Activity Data does not use an atomic replacement"
    Assert-Condition ($activityCode -match '\.corrupt-') "Corrupt Activity Data does not create a timestamped backup"
    Assert-Condition ($windowCode -match '(?s)RecordActivity\(emoji\).*?TryInsertAsync') "Selection is not recorded before insertion starts"
    Assert-Condition ($windowCode -notmatch 'Classic.*(?:recent|ranking)|(?:recent|ranking).*Classic') "Activity Data appears coupled to Classic"

    Write-Host "Activity Data verification passed: Recent MRU 50, resolved sequences, 90-day Learned Ranking, versioned atomic persistence, independent recovery and clear controls" -ForegroundColor Green
}
finally {
    Pop-Location
}
