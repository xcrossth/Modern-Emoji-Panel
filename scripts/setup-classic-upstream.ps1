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
            throw "เพิ่ม remote '$($sourceManifest.remoteName)' ไม่สำเร็จ"
        }
    }
    else {
        $currentUrl = (& git remote get-url $sourceManifest.remoteName).Trim()
        if ($LASTEXITCODE -ne 0) {
            throw "อ่าน URL ของ remote '$($sourceManifest.remoteName)' ไม่สำเร็จ"
        }

        $normalisedCurrentUrl = $currentUrl.TrimEnd("/") -replace "\.git$", ""
        $normalisedApprovedUrl = ([string]$sourceManifest.sourceUrl).TrimEnd("/") -replace "\.git$", ""
        if (-not $normalisedCurrentUrl.Equals($normalisedApprovedUrl, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "remote '$($sourceManifest.remoteName)' ชี้ไปยัง URL ที่ไม่ได้อนุมัติ: $currentUrl"
        }
    }

    if ($FetchApprovedCommit) {
        & git fetch $sourceManifest.remoteName $sourceManifest.sourceCommit
        if ($LASTEXITCODE -ne 0) {
            throw "fetch approved upstream commit ไม่สำเร็จ"
        }

        $fetchedTree = (& git rev-parse "$($sourceManifest.sourceCommit)^{tree}").Trim()
        if ($fetchedTree -ne $sourceManifest.sourceTree) {
            throw "tree hash ของ upstream ไม่ตรงกับ manifest"
        }
    }

    Write-Host "Remote '$($sourceManifest.remoteName)' พร้อมใช้งาน: $($sourceManifest.sourceUrl)" -ForegroundColor Green
}
finally {
    Pop-Location
}
