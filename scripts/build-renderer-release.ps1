[CmdletBinding()]
param(
    [switch]$SkipInstall,
    [switch]$SkipQualification
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$extensionRoot = Join-Path $repositoryRoot "apps\renderer-extension"
$releaseRoot = Join-Path $repositoryRoot "artifacts\renderer-extension\release"

Push-Location $repositoryRoot
try {
    if (-not $SkipInstall) {
        npm --prefix $extensionRoot ci
        if ($LASTEXITCODE -ne 0) { throw "npm ci failed" }
    }

    if (-not $SkipQualification) {
        & .\scripts\verify-renderer-qualification.ps1 -SkipInstall
    }

    npm --prefix $extensionRoot run build:production
    if ($LASTEXITCODE -ne 0) { throw "Renderer production build failed" }

    npm --prefix $extensionRoot run release
    if ($LASTEXITCODE -ne 0) { throw "Renderer release packaging failed" }

    $manifest = Get-Content (Join-Path $extensionRoot "manifest.json") -Raw | ConvertFrom-Json
    $zipName = "modern-emoji-renderer-$($manifest.version).zip"
    $zipPath = Join-Path $releaseRoot $zipName
    $firstHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash.ToLowerInvariant()

    npm --prefix $extensionRoot run release
    if ($LASTEXITCODE -ne 0) { throw "Second deterministic packaging run failed" }
    $secondHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash.ToLowerInvariant()
    if ($firstHash -ne $secondHash) {
        throw "Release ZIP is not deterministic: $firstHash != $secondHash"
    }

    npm --prefix $extensionRoot run verify:release
    if ($LASTEXITCODE -ne 0) { throw "Renderer release verification failed" }

    & .\scripts\verify-renderer-chrome-load.ps1 `
        -ExtensionPath (Join-Path $releaseRoot "package") `
        -SkipBuild

    node .\apps\renderer-extension\scripts\verify-extension-font.mjs `
        --extension-root (Join-Path $releaseRoot "package")
    if ($LASTEXITCODE -ne 0) { throw "Renderer release bundled-font verification failed" }

    [ordered]@{
        schemaVersion = 1
        status = "passed"
        runs = 2
        sha256 = $secondHash
        zip = $zipName
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releaseRoot "determinism-report.json") -Encoding utf8

    Write-Host "Renderer local release passed: $zipPath" -ForegroundColor Green
    Write-Host "SHA-256: $secondHash" -ForegroundColor Green
}
finally {
    Pop-Location
}
