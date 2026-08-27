[CmdletBinding()]
param(
    [string]$Revision = "HEAD",

    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$checkoutPath = [System.IO.Path]::GetFullPath((Join-Path $temporaryRoot ("modern-emoji-picker-foundation-" + [Guid]::NewGuid().ToString("N"))))

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
