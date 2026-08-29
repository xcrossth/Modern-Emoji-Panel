[CmdletBinding()]
param(
    [switch]$FetchApprovedCommit
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$sourceManifestPath = Join-Path $repositoryRoot "docs\upstream\classic-picker.source.json"
$sourceManifest = Get-Content -Raw -LiteralPath $sourceManifestPath | ConvertFrom-Json

Push-Location $repositoryRoot
try {
    $remoteNames = @(& git remote)
    if ($remoteNames -notcontains $sourceManifest.remoteName) {
        & git remote add $sourceManifest.remoteName $sourceManifest.sourceUrl
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to add remote '$($sourceManifest.remoteName)'"
        }
    }
    else {
        $currentUrl = (& git remote get-url $sourceManifest.remoteName).Trim()
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to read URL for remote '$($sourceManifest.remoteName)'"
        }

        $normalisedCurrentUrl = $currentUrl.TrimEnd("/") -replace "\.git$", ""
        $normalisedApprovedUrl = ([string]$sourceManifest.sourceUrl).TrimEnd("/") -replace "\.git$", ""
        if (-not $normalisedCurrentUrl.Equals($normalisedApprovedUrl, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Remote '$($sourceManifest.remoteName)' points to an unapproved URL: $currentUrl"
        }
    }

    if ($FetchApprovedCommit) {
        & git fetch $sourceManifest.remoteName $sourceManifest.sourceCommit
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to fetch the approved upstream commit"
        }

        $fetchedTree = (& git rev-parse "$($sourceManifest.sourceCommit)^{tree}").Trim()
        if ($fetchedTree -ne $sourceManifest.sourceTree) {
            throw "Upstream tree hash does not match the manifest"
        }
    }

    Write-Host "Remote '$($sourceManifest.remoteName)' is ready: $($sourceManifest.sourceUrl)" -ForegroundColor Green
}
finally {
    Pop-Location
}
