[CmdletBinding()]
param(
    [ValidateSet("Stable", "Beta", "Dev", "Canary")]
    [string]$Channel = "Stable"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$toolingRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot "artifacts\tooling\chrome-for-testing"))
$manifestUri = "https://googlechromelabs.github.io/chrome-for-testing/last-known-good-versions-with-downloads.json"
$manifest = Invoke-RestMethod $manifestUri
$release = $manifest.channels.$Channel
if ($null -eq $release) {
    throw "Chrome for Testing channel was not found: $Channel"
}

$download = $release.downloads.chrome | Where-Object { $_.platform -eq "win64" } | Select-Object -First 1
if ($null -eq $download) {
    throw "Chrome for Testing win64 download was not found"
}

$versionRoot = [System.IO.Path]::GetFullPath((Join-Path $toolingRoot $release.version))
if (-not $versionRoot.StartsWith($toolingRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Chrome for Testing version path escaped the tooling root"
}

$chromePath = Join-Path $versionRoot "chrome-win64\chrome.exe"
if (Test-Path -LiteralPath $chromePath -PathType Leaf) {
    Write-Host "Chrome for Testing already installed: $chromePath" -ForegroundColor Green
    return
}

if (Test-Path -LiteralPath $versionRoot) {
    Remove-Item -LiteralPath $versionRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $versionRoot | Out-Null
$archivePath = Join-Path $versionRoot "chrome-win64.zip"
try {
    Write-Host "Downloading Chrome for Testing $($release.version)..."
    Invoke-WebRequest -Uri $download.url -OutFile $archivePath
    Expand-Archive -LiteralPath $archivePath -DestinationPath $versionRoot
}
finally {
    if (Test-Path -LiteralPath $archivePath) {
        Remove-Item -LiteralPath $archivePath -Force
    }
}

if (-not (Test-Path -LiteralPath $chromePath -PathType Leaf)) {
    throw "Chrome for Testing extraction did not produce chrome.exe"
}

Write-Host "Chrome for Testing installed: $chromePath" -ForegroundColor Green
