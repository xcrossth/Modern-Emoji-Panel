[CmdletBinding()]
param([switch]$SkipInstall)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$evidenceRoot = Join-Path $repositoryRoot "artifacts\renderer-extension\evidence\ticket-09"
New-Item -ItemType Directory -Force -Path $evidenceRoot | Out-Null
Push-Location $repositoryRoot
try {
    & .\scripts\verify-renderer-foundation.ps1 -SkipInstall:$SkipInstall
    & .\scripts\verify-renderer-static-fixture.ps1 -SkipBuild
    & .\scripts\verify-renderer-dom-fixture.ps1 -SkipBuild
    & .\scripts\verify-renderer-ui.ps1 -SkipBuild
    & .\scripts\verify-renderer-performance.ps1 -SkipBuild
    & .\scripts\verify-renderer-chrome-load.ps1 -SkipBuild
    npm --prefix .\apps\renderer-extension run verify:extension-font
    if ($LASTEXITCODE -ne 0) { throw "Renderer bundled-font verification failed" }

    $vitestReport = Join-Path $evidenceRoot "vitest-report.json"
    npm --prefix .\apps\renderer-extension test -- --reporter=json --outputFile="$vitestReport"
    if ($LASTEXITCODE -ne 0) { throw "Renderer JSON test report failed" }

    node .\apps\renderer-extension\scripts\write-qualification-report.mjs
    if ($LASTEXITCODE -ne 0) { throw "Renderer qualification report failed" }
}
finally {
    Pop-Location
}
