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
    throw "ตำแหน่ง clean checkout อยู่นอก temporary directory"
}

Push-Location $repositoryRoot
try {
    & git rev-parse --verify "$Revision^{commit}" *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Revision '$Revision' ไม่ใช่ commit ที่ใช้งานได้"
    }

    & git worktree add --detach $checkoutPath $Revision
    if ($LASTEXITCODE -ne 0) {
        throw "สร้าง temporary worktree ไม่สำเร็จ"
    }

    & (Join-Path $checkoutPath "scripts\verify-foundation.ps1") -SkipPublish:$SkipPublish
    if ($LASTEXITCODE -ne 0) {
        throw "regression verification จาก clean checkout ล้มเหลว"
    }
}
finally {
    Pop-Location

    if (Test-Path -LiteralPath $checkoutPath) {
        Push-Location $repositoryRoot
        try {
            & git worktree remove --force $checkoutPath
            if ($LASTEXITCODE -ne 0) {
                throw "ลบ temporary worktree ไม่สำเร็จ: $checkoutPath"
            }
        }
        finally {
            Pop-Location
        }
    }
}

Write-Host "Clean-checkout verification ผ่านสำหรับ $Revision" -ForegroundColor Green
