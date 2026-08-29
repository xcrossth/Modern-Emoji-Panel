[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$NoRestore,

    [switch]$PublishSelfContained
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solutionPath = Join-Path $repositoryRoot "ModernEmojiPanel.sln"
$pickerProjectPath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\EmojiPicker.csproj"

Push-Location $repositoryRoot
try {
    if (-not $NoRestore) {
        & dotnet restore $solutionPath --locked-mode
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore in locked mode failed"
        }
    }

    & dotnet build $solutionPath --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed"
    }

    if ($PublishSelfContained) {
        $artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts\foundation"))
        $publishPath = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot "picker-win-x64"))

        if (-not $publishPath.StartsWith($artifactRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Publish path is outside artifacts/foundation"
        }

        if (Test-Path -LiteralPath $publishPath) {
            Remove-Item -LiteralPath $publishPath -Recurse -Force
        }

        & dotnet publish $pickerProjectPath `
            --configuration $Configuration `
            --runtime win-x64 `
            --self-contained true `
            --no-restore `
            --output $publishPath
        if ($LASTEXITCODE -ne 0) {
            throw "Self-contained dotnet publish failed"
        }

        Write-Host "Self-contained publish: $publishPath" -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
