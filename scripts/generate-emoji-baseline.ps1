[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$PreviousEmojiData
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repositoryRoot "tools\emoji-baseline\EmojiBaseline.Generator.csproj"
$outputPath = Join-Path $repositoryRoot "data\emoji-baseline\17.0"

$arguments = @(
    "run",
    "--project", $projectPath,
    "--configuration", $Configuration,
    "--no-restore",
    "--",
    "--repository-root", $repositoryRoot,
    "--output", $outputPath
)

if (-not [string]::IsNullOrWhiteSpace($PreviousEmojiData)) {
    $arguments += @("--previous", (Resolve-Path $PreviousEmojiData).Path)
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Emoji Baseline generator failed with exit code $LASTEXITCODE"
}

Write-Host "Generated committed Emoji Baseline artifacts: $outputPath" -ForegroundColor Green
