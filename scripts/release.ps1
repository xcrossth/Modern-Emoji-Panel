[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solutionPath = Join-Path $repositoryRoot "ModernEmojiPanel.sln"
$projectPath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\EmojiPicker.csproj"
$installerScript = Join-Path $repositoryRoot "apps\picker\installer\EmojiPicker.iss"
$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts\release\picker-v$Version"))
$allowedReleaseRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts\release"))
$publishPath = Join-Path $releaseRoot "publish"
$portableName = "Modern-Emoji-Picker-v$Version-portable-win-x64.zip"
$installerName = "Modern-Emoji-Picker-v$Version-setup-win-x64.exe"
$portablePath = Join-Path $releaseRoot $portableName
$installerPath = Join-Path $releaseRoot $installerName
$manifestPath = Join-Path $releaseRoot "release-manifest.json"
$checksumsPath = Join-Path $releaseRoot "SHA256SUMS.txt"

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Get-DirectorySize {
    param([string]$Path)
    return [long]((Get-ChildItem -LiteralPath $Path -File -Recurse | Measure-Object -Property Length -Sum).Sum)
}

function Resolve-InnoCompiler {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $found = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $found) {
        throw "Inno Setup 6 compiler (ISCC.exe) is required to build the official per-user installer."
    }

    return $found
}

Push-Location $repositoryRoot
try {
    Assert-Condition `
        ($releaseRoot.StartsWith($allowedReleaseRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) `
        "Release output path is outside artifacts/release"

    $workingTree = (& git status --porcelain=v1 --untracked-files=all)
    Assert-Condition ($LASTEXITCODE -eq 0) "Could not inspect git status"
    Assert-Condition (-not $workingTree) "Release packaging requires a clean commit"
    $commit = (& git rev-parse HEAD).Trim()
    Assert-Condition ($LASTEXITCODE -eq 0 -and $commit.Length -eq 40) "Could not resolve the release commit"

    [xml]$project = Get-Content -Raw -LiteralPath $projectPath
    $projectVersion = [string]$project.Project.PropertyGroup.Version
    Assert-Condition ($projectVersion -eq $Version) "Requested version $Version does not match project Version $projectVersion"
    Assert-Condition ([string]$project.Project.PropertyGroup.TargetFramework -eq "net10.0-windows") "Release must target net10.0-windows"
    Assert-Condition ([string]$project.Project.PropertyGroup.RuntimeIdentifier -eq "win-x64") "Release must target win-x64"

    & (Join-Path $PSScriptRoot "build-product-icon.ps1") -VerifyOnly
    if ($LASTEXITCODE -ne 0) { throw "Product icon verification failed" }
    & (Join-Path $PSScriptRoot "verify-generated-emoji-baseline.ps1")
    if ($LASTEXITCODE -ne 0) { throw "Emoji Baseline lock/generator verification failed" }
    & (Join-Path $PSScriptRoot "verify-qualification.ps1")
    if ($LASTEXITCODE -ne 0) { throw "Automated qualification failed" }

    $innoCompiler = Resolve-InnoCompiler
    if (Test-Path -LiteralPath $releaseRoot) {
        Remove-Item -LiteralPath $releaseRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $publishPath -Force | Out-Null

    & dotnet publish $projectPath `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --no-restore `
        --output $publishPath
    if ($LASTEXITCODE -ne 0) { throw "Self-contained release publish failed" }

    Copy-Item -LiteralPath (Join-Path $repositoryRoot "LICENSE") -Destination (Join-Path $publishPath "LICENSE")
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "THIRD-PARTY-NOTICES.md") -Destination (Join-Path $publishPath "THIRD-PARTY-NOTICES.md")
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "docs\release\README.md") -Destination (Join-Path $publishPath "README-th.md")

    if (Test-Path -LiteralPath $portablePath) {
        Remove-Item -LiteralPath $portablePath -Force
    }
    Compress-Archive -Path (Join-Path $publishPath "*") -DestinationPath $portablePath -CompressionLevel Optimal

    & $innoCompiler `
        "/DAppVersion=$Version" `
        "/DPublishDir=$publishPath" `
        "/DOutputDir=$releaseRoot" `
        $installerScript
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup installer build failed" }

    Assert-Condition (Test-Path -LiteralPath $portablePath) "Portable ZIP was not created"
    Assert-Condition (Test-Path -LiteralPath $installerPath) "Inno installer was not created"
    Assert-Condition (-not (Get-ChildItem -LiteralPath $releaseRoot -File -Recurse -Filter "*.msi")) "MVP output must not contain MSI files"
    Assert-Condition (-not (Get-ChildItem -LiteralPath $releaseRoot -File -Recurse | Where-Object Name -Match '(?i)lite|framework')) "MVP output must not contain framework-dependent files"

    $portableHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $portablePath).Hash.ToLowerInvariant()
    $installerHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $installerPath).Hash.ToLowerInvariant()
    @(
        "$portableHash  $portableName",
        "$installerHash  $installerName"
    ) | Set-Content -LiteralPath $checksumsPath -Encoding utf8NoBOM

    $notoPath = Join-Path $repositoryRoot "vendor\noto-emoji\v2.051"
    $manifest = [ordered]@{
        schemaVersion = 1
        product = "Modern Emoji Picker"
        version = $Version
        commit = $commit
        createdAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
        targetFramework = "net10.0-windows"
        runtimeIdentifier = "win-x64"
        selfContained = $true
        signed = $false
        uploaded = $false
        sizes = [ordered]@{
            rawNotoAssetsBytes = Get-DirectorySize -Path $notoPath
            publishDirectoryBytes = Get-DirectorySize -Path $publishPath
            installerBytes = (Get-Item -LiteralPath $installerPath).Length
            portableZipBytes = (Get-Item -LiteralPath $portablePath).Length
        }
        artifacts = @(
            [ordered]@{ type = "portable-zip"; file = $portableName; sha256 = $portableHash },
            [ordered]@{ type = "inno-per-user-installer"; file = $installerName; sha256 = $installerHash }
        )
        notices = @("LICENSE", "THIRD-PARTY-NOTICES.md", "README-th.md")
    }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM

    & (Join-Path $PSScriptRoot "verify-release-artifacts.ps1") -Version $Version -ArtifactRoot $releaseRoot
    if ($LASTEXITCODE -ne 0) { throw "Release artifact verification failed" }

    & (Join-Path $PSScriptRoot "verify-qualification.ps1") `
        -SkipBuild `
        -SkipRegressionSuite `
        -ReleaseManifestPath $manifestPath `
        -OutputPath (Join-Path $releaseRoot "qualification-report.json")
    if ($LASTEXITCODE -ne 0) { throw "Artifact-aware qualification failed" }

    Write-Host "Local release artifacts passed verification: $releaseRoot" -ForegroundColor Green
    Write-Host "No tag, upload or GitHub Release was created." -ForegroundColor Yellow
}
finally {
    Pop-Location
}
