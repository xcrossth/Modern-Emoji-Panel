[CmdletBinding()]
param(
    [string]$Revision = "HEAD",

    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$checkoutPath = [System.IO.Path]::GetFullPath((Join-Path $temporaryRoot ("mep-" + [Guid]::NewGuid().ToString("N").Substring(0, 8))))

if (-not $checkoutPath.StartsWith($temporaryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Clean checkout path is outside the temporary directory"
}

Push-Location $repositoryRoot
try {
    & git rev-parse --verify "$Revision^{commit}" *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Revision '$Revision' is not a valid commit"
    }

    & git worktree add --detach $checkoutPath $Revision
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create the temporary worktree"
    }

    & (Join-Path $checkoutPath "scripts\verify-foundation.ps1") -SkipPublish:$SkipPublish
    if ($LASTEXITCODE -ne 0) {
        throw "Regression verification from the clean checkout failed"
    }

    & (Join-Path $checkoutPath "scripts\verify-generated-emoji-baseline.ps1")
    if ($LASTEXITCODE -ne 0) {
        throw "Generated Emoji Baseline verification from the clean checkout failed"
    }

    & (Join-Path $checkoutPath "scripts\verify-noto-grid.ps1") -SkipBuild
    if ($LASTEXITCODE -ne 0) {
        throw "Noto grid verification from the clean checkout failed"
    }

    & (Join-Path $checkoutPath "scripts\verify-safe-insertion.ps1") -SkipBuild
    if ($LASTEXITCODE -ne 0) {
        throw "Safe insertion verification from the clean checkout failed"
    }

    & (Join-Path $checkoutPath "scripts\verify-search-preview.ps1") -SkipBuild
    if ($LASTEXITCODE -ne 0) {
        throw "Bilingual search and preview verification from the clean checkout failed"
    }

    & (Join-Path $checkoutPath "scripts\verify-emoji-variants.ps1") -SkipBuild
    if ($LASTEXITCODE -ne 0) {
        throw "Emoji variant verification from the clean checkout failed"
    }

    & (Join-Path $checkoutPath "scripts\verify-picker-session.ps1") -SkipBuild
    if ($LASTEXITCODE -ne 0) {
        throw "Picker Session verification from the clean checkout failed"
    }

    & (Join-Path $checkoutPath "scripts\verify-activity-data.ps1") -SkipBuild
    if ($LASTEXITCODE -ne 0) {
        throw "Activity Data verification from the clean checkout failed"
    }

    & (Join-Path $checkoutPath "scripts\verify-settings-privacy.ps1") -SkipBuild
    if ($LASTEXITCODE -ne 0) {
        throw "Settings and privacy verification from the clean checkout failed"
    }
}
finally {
    Pop-Location

    if (Test-Path -LiteralPath $checkoutPath) {
        Push-Location $repositoryRoot
        try {
            & git worktree remove --force $checkoutPath
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to remove the temporary worktree: $checkoutPath"
            }
        }
        finally {
            Pop-Location
        }
    }
}

Write-Host "Clean-checkout verification passed for $Revision" -ForegroundColor Green
