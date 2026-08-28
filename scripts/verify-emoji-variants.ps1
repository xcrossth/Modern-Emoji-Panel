[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solutionPath = Join-Path $repositoryRoot "ModernEmojiPanel.sln"
$testProjectPath = Join-Path $repositoryRoot "tests\EmojiPicker.DomainTests\EmojiPicker.DomainTests.csproj"

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

    & dotnet run `
        --project $testProjectPath `
        --configuration Release `
        --no-build `
        --no-restore `
        -- $repositoryRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Emoji variant domain verification failed"
    }
}
finally {
    Pop-Location
}
