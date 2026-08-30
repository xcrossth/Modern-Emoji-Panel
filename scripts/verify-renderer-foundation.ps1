[CmdletBinding()]
param(
    [switch]$SkipInstall
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$extensionRoot = Join-Path $repositoryRoot "apps\renderer-extension"
$packageLock = Join-Path $extensionRoot "package-lock.json"

Push-Location $repositoryRoot
try {
    if (-not $SkipInstall) {
        if (-not (Test-Path -LiteralPath $packageLock -PathType Leaf)) {
            throw "Renderer Extension package-lock.json is missing"
        }

        & npm --prefix $extensionRoot ci
        if ($LASTEXITCODE -ne 0) {
            throw "Renderer Extension locked npm install failed"
        }
    }

    & npm --prefix $extensionRoot run verify
    if ($LASTEXITCODE -ne 0) {
        throw "Renderer Extension foundation verification failed"
    }
}
finally {
    Pop-Location
}

Write-Host "Renderer Extension foundation verification passed" -ForegroundColor Green
