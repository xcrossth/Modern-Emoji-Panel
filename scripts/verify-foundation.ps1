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
$generatorLockPath = Join-Path $repositoryRoot "tools\emoji-baseline\packages.lock.json"
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
        throw "Failed to read the .NET SDK version"
    }

    $sdkVersion = [Version]$sdkVersionText
    Assert-Condition `
        ($sdkVersion.Major -eq 10 -and $sdkVersion.Minor -eq 0 -and $sdkVersion.Build -ge 400 -and $sdkVersion.Build -lt 500) `
        "Expected .NET SDK feature band 10.0.4xx, but found $sdkVersionText"

    [xml]$project = Get-Content -Raw -LiteralPath $projectPath
    $targetFramework = [string]$project.Project.PropertyGroup.TargetFramework
    $runtimeIdentifier = [string]$project.Project.PropertyGroup.RuntimeIdentifier
    Assert-Condition ($targetFramework -eq "net10.0-windows") "Picker must target net10.0-windows"
    Assert-Condition ($runtimeIdentifier -eq "win-x64") "Picker must set RuntimeIdentifier to win-x64"

    Assert-Condition (Test-Path -LiteralPath $solutionPath) "Root solution is missing"
    Assert-Condition (Test-Path -LiteralPath (Join-Path $repositoryRoot "Directory.Packages.props")) "Central package versions are missing"
    Assert-Condition (Test-Path -LiteralPath $lockPath) "Picker NuGet lock file is missing"
    Assert-Condition (Test-Path -LiteralPath $generatorLockPath) "Generator NuGet lock file is missing"

    $sourceManifest = Get-Content -Raw -LiteralPath $sourceManifestPath | ConvertFrom-Json
    $importedTree = (& git rev-parse "$($sourceManifest.importCommit):$($sourceManifest.prefix)").Trim()
    $approvedTree = (& git rev-parse "$($sourceManifest.sourceCommit)^{tree}").Trim()
    Assert-Condition ($importedTree -eq $sourceManifest.sourceTree) "Imported subtree does not match the approved tree hash"
    Assert-Condition ($approvedTree -eq $sourceManifest.sourceTree) "Upstream commit does not match the approved tree hash"
    & git merge-base --is-ancestor $sourceManifest.sourceCommit HEAD
    Assert-Condition ($LASTEXITCODE -eq 0) "Upstream history is not an ancestor of the current checkout"

    $activeWorkflowRoot = Join-Path $repositoryRoot ".github\workflows"
    if (Test-Path -LiteralPath $activeWorkflowRoot) {
        $syncPattern = "git\s+subtree|classic-upstream|Classic-EmojiPicker\.git"
        $activeSync = Get-ChildItem -LiteralPath $activeWorkflowRoot -File -Recurse |
            Select-String -Pattern $syncPattern
        Assert-Condition (-not $activeSync) "Found an active workflow that may sync Classic upstream automatically"
    }

    & (Join-Path $PSScriptRoot "build.ps1") -Configuration Release -PublishSelfContained:(-not $SkipPublish)
    if ($LASTEXITCODE -ne 0) {
        throw "Foundation build failed"
    }

    if ($SkipPublish) {
        $smokeExecutable = Join-Path $repositoryRoot "apps\picker\EmojiPicker\bin\Release\net10.0-windows\win-x64\ModernEmojiPicker.exe"
    }
    else {
        $smokeExecutable = Join-Path $repositoryRoot "artifacts\foundation\picker-win-x64\ModernEmojiPicker.exe"
    }

    Assert-Condition (Test-Path -LiteralPath $smokeExecutable) "Smoke-test executable is missing"
    $smokeProcess = Start-Process `
        -FilePath $smokeExecutable `
        -ArgumentList "--foundation-smoke" `
        -Wait `
        -PassThru `
        -WindowStyle Hidden
    if ($smokeProcess.ExitCode -ne 0) {
        throw "WPF foundation smoke test failed with exit code $($smokeProcess.ExitCode)"
    }

    & dotnet format $solutionPath --verify-no-changes --no-restore --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet format verification failed"
    }

    & git diff --exit-code -- $lockPath $generatorLockPath
    if ($LASTEXITCODE -ne 0) {
        throw "Locked restore changed packages.lock.json"
    }

    Write-Host "Foundation verification and WPF browse/search smoke test passed with SDK $sdkVersionText" -ForegroundColor Green
}
finally {
    Pop-Location
}
