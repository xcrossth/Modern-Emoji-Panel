[CmdletBinding()]
param(
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solutionPath = Join-Path $repositoryRoot "ModernEmojiPanel.sln"
$projectPath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\EmojiPicker.csproj"
$lockPath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\packages.lock.json"
$sourceManifestPath = Join-Path $repositoryRoot "docs\upstream\classic-picker.source.json"

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

Push-Location $repositoryRoot
try {
    $sdkVersionText = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "อ่านเวอร์ชัน .NET SDK ไม่สำเร็จ"
    }

    $sdkVersion = [Version]$sdkVersionText
    Assert-Condition `
        ($sdkVersion.Major -eq 10 -and $sdkVersion.Minor -eq 0 -and $sdkVersion.Build -ge 400 -and $sdkVersion.Build -lt 500) `
        "ต้องใช้ .NET SDK feature band 10.0.4xx แต่พบ $sdkVersionText"

    [xml]$project = Get-Content -Raw -LiteralPath $projectPath
    $targetFramework = [string]$project.Project.PropertyGroup.TargetFramework
    $runtimeIdentifier = [string]$project.Project.PropertyGroup.RuntimeIdentifier
    Assert-Condition ($targetFramework -eq "net10.0-windows") "Picker ต้อง target net10.0-windows"
    Assert-Condition ($runtimeIdentifier -eq "win-x64") "Picker ต้องกำหนด RuntimeIdentifier เป็น win-x64"

    Assert-Condition (Test-Path -LiteralPath $solutionPath) "ไม่พบ root solution"
    Assert-Condition (Test-Path -LiteralPath (Join-Path $repositoryRoot "Directory.Packages.props")) "ไม่พบ central package versions"
    Assert-Condition (Test-Path -LiteralPath $lockPath) "ไม่พบ NuGet lock file ของ Picker"

    $sourceManifest = Get-Content -Raw -LiteralPath $sourceManifestPath | ConvertFrom-Json
    $importedTree = (& git rev-parse "$($sourceManifest.importCommit):$($sourceManifest.prefix)").Trim()
    $approvedTree = (& git rev-parse "$($sourceManifest.sourceCommit)^{tree}").Trim()
    Assert-Condition ($importedTree -eq $sourceManifest.sourceTree) "subtree ที่ import ไม่ตรงกับ tree hash ที่อนุมัติ"
    Assert-Condition ($approvedTree -eq $sourceManifest.sourceTree) "upstream commit ไม่ตรงกับ tree hash ที่อนุมัติ"
    & git merge-base --is-ancestor $sourceManifest.sourceCommit HEAD
    Assert-Condition ($LASTEXITCODE -eq 0) "ประวัติ upstream ไม่ได้เป็น ancestor ของ checkout ปัจจุบัน"

    $activeWorkflowRoot = Join-Path $repositoryRoot ".github\workflows"
    if (Test-Path -LiteralPath $activeWorkflowRoot) {
        $syncPattern = "git\s+subtree|classic-upstream|Classic-EmojiPicker\.git"
        $activeSync = Get-ChildItem -LiteralPath $activeWorkflowRoot -File -Recurse |
            Select-String -Pattern $syncPattern
        Assert-Condition (-not $activeSync) "พบ active workflow ที่อาจ sync Classic upstream อัตโนมัติ"
    }

    & (Join-Path $PSScriptRoot "build.ps1") -Configuration Release -PublishSelfContained:(-not $SkipPublish)
    if ($LASTEXITCODE -ne 0) {
        throw "foundation build ล้มเหลว"
    }

    if ($SkipPublish) {
        $smokeExecutable = Join-Path $repositoryRoot "apps\picker\EmojiPicker\bin\Release\net10.0-windows\win-x64\EmojiPicker.exe"
    }
    else {
        $smokeExecutable = Join-Path $repositoryRoot "artifacts\foundation\picker-win-x64\EmojiPicker.exe"
    }

    Assert-Condition (Test-Path -LiteralPath $smokeExecutable) "ไม่พบ executable สำหรับ smoke test"
    & $smokeExecutable --foundation-smoke
    if ($LASTEXITCODE -ne 0) {
        throw "WPF foundation smoke test ล้มเหลวด้วย exit code $LASTEXITCODE"
    }

    & dotnet format $solutionPath --verify-no-changes --no-restore --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet format verification ล้มเหลว"
    }

    & git diff --exit-code -- $lockPath
    if ($LASTEXITCODE -ne 0) {
        throw "locked restore เปลี่ยน packages.lock.json"
    }

    Write-Host "Foundation verification และ WPF browse/search smoke test ผ่านด้วย SDK $sdkVersionText" -ForegroundColor Green
}
finally {
    Pop-Location
}
