[CmdletBinding()]
param([switch]$SkipBuild)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Push-Location $repositoryRoot
try {
    if (-not $SkipBuild) {
        npm --prefix .\apps\renderer-extension run build
        if ($LASTEXITCODE -ne 0) { throw "Renderer build failed" }
    }
    npm --prefix .\apps\renderer-extension run verify:performance
    if ($LASTEXITCODE -ne 0) { throw "Renderer performance fixture failed" }
}
finally {
    Pop-Location
}
