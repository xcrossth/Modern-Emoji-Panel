[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repositoryRoot "tools\emoji-baseline\EmojiBaseline.Generator.csproj"
$committedOutput = Join-Path $repositoryRoot "data\emoji-baseline\17.0"
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$runId = [Guid]::NewGuid().ToString("N")
$firstOutput = [IO.Path]::GetFullPath((Join-Path $temporaryRoot "modern-emoji-generator-$runId-a"))
$secondOutput = [IO.Path]::GetFullPath((Join-Path $temporaryRoot "modern-emoji-generator-$runId-b"))

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Invoke-Generator {
    param([string]$OutputPath)

    & dotnet run `
        --project $projectPath `
        --configuration $Configuration `
        --no-build `
        --no-restore `
        -- `
        --repository-root $repositoryRoot `
        --output $OutputPath
    if ($LASTEXITCODE -ne 0) {
        throw "Emoji Baseline generator failed with exit code $LASTEXITCODE"
    }
}

function Get-FileMap {
    param([string]$Directory)

    $resolvedDirectory = [IO.Path]::GetFullPath($Directory).TrimEnd('\', '/')
    $result = @{}
    foreach ($file in Get-ChildItem -LiteralPath $resolvedDirectory -File -Recurse | Sort-Object FullName) {
        $relativePath = $file.FullName.Substring($resolvedDirectory.Length).TrimStart('\', '/').Replace('\', '/')
        $result[$relativePath] = "{0}:{1}" -f `
            (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant(), `
            $file.Length
    }

    return $result
}

function Assert-FileMapsEqual {
    param(
        [hashtable]$Expected,
        [hashtable]$Actual,
        [string]$Label
    )

    Assert-Condition ($Expected.Count -eq $Actual.Count) "$Label file count differs"
    foreach ($path in $Expected.Keys) {
        Assert-Condition $Actual.ContainsKey($path) "$Label is missing $path"
        Assert-Condition ($Expected[$path] -eq $Actual[$path]) "$Label bytes differ for $path"
    }
}

foreach ($path in @($firstOutput, $secondOutput)) {
    Assert-Condition $path.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) `
        "Temporary output escaped the system temporary directory"
}

try {
    & dotnet build $projectPath --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "Emoji Baseline generator build failed"
    }

    Invoke-Generator $firstOutput
    Invoke-Generator $secondOutput

    $firstMap = Get-FileMap $firstOutput
    $secondMap = Get-FileMap $secondOutput
    $committedMap = Get-FileMap $committedOutput
    Assert-FileMapsEqual $firstMap $secondMap "Repeated generation"
    Assert-FileMapsEqual $firstMap $committedMap "Committed generated baseline"

    $emojiData = Get-Content -Raw -LiteralPath (Join-Path $firstOutput "emoji.json") | ConvertFrom-Json
    $report = Get-Content -Raw -LiteralPath (Join-Path $firstOutput "review-report.json") | ConvertFrom-Json
    $sourceManifest = Get-Content -Raw -LiteralPath (Join-Path $firstOutput "source-manifest.json") | ConvertFrom-Json
    $entries = @($emojiData.entries)

    Assert-Condition ($emojiData.schemaVersion -eq 1) "Emoji data schema differs"
    Assert-Condition ($entries.Count -eq 3944) "Fully-qualified Emoji 17 count must be 3944"
    Assert-Condition ((@($entries.id | Sort-Object -Unique)).Count -eq $entries.Count) "Stable IDs are not unique"
    Assert-Condition ((@($entries.canonicalSequence | Sort-Object -Unique)).Count -eq $entries.Count) `
        "Canonical sequences are not unique"

    for ($index = 0; $index -lt $entries.Count; $index++) {
        $entry = $entries[$index]
        Assert-Condition ($entry.order -eq $index) "Deterministic order differs at index $index"
        Assert-Condition ($entry.id -match '^emoji-[0-9a-f-]+$') "Stable ID is invalid: $($entry.id)"
        Assert-Condition (-not [string]::IsNullOrWhiteSpace($entry.english.shortName)) `
            "English short name is missing: $($entry.id)"
        Assert-Condition (-not [string]::IsNullOrWhiteSpace($entry.thai.shortName)) `
            "Thai short name is missing: $($entry.id)"
        Assert-Condition (@($entry.english.keywords).Count -gt 0) "English keywords are missing: $($entry.id)"
        Assert-Condition (@($entry.thai.keywords).Count -gt 0) "Thai keywords are missing: $($entry.id)"
        Assert-Condition (@($entry.asset.aliases).Count -ge 3) "Asset aliases are incomplete: $($entry.id)"

        foreach ($assetPath in @($entry.asset.png128, $entry.asset.png512)) {
            $resolvedAsset = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $assetPath))
            $rootWithSeparator = $repositoryRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
            Assert-Condition $resolvedAsset.StartsWith($rootWithSeparator, [StringComparison]::OrdinalIgnoreCase) `
                "Asset path escapes the repository: $assetPath"
            Assert-Condition (Test-Path -LiteralPath $resolvedAsset -PathType Leaf) `
                "Mapped asset is missing: $assetPath"
        }
    }

    Assert-Condition ((@($entries | Where-Object { $_.group -eq 'Flags' })).Count -eq 270) `
        "Unicode Flags group coverage must be 270 entries"
    Assert-Condition ((@($entries | Where-Object { $_.asset.sourceKind -eq 'noto-region-flag' })).Count -eq 262) `
        "Noto region-flag coverage must be 262 entries"
    $resolvedFlagAliases = @{
        '1F1E7 1F1FB' = 'NO.png'
        '1F1E8 1F1F5' = 'FR.png'
        '1F1E9 1F1EC' = 'IO.png'
        '1F1EA 1F1E6' = 'ES.png'
        '1F1ED 1F1F2' = 'AU.png'
        '1F1F2 1F1EB' = 'FR.png'
        '1F1F8 1F1EF' = 'NO.png'
        '1F1FA 1F1F2' = 'US.png'
    }
    foreach ($sequence in $resolvedFlagAliases.Keys) {
        $entry = @($entries | Where-Object { $_.canonicalSequence -eq $sequence })
        Assert-Condition ($entry.Count -eq 1) "Expected one region-flag alias entry for $sequence"
        $expectedSuffix = "/third_party/region-flags/png/$($resolvedFlagAliases[$sequence])"
        Assert-Condition ([string]$entry[0].asset.png128 -like "*$expectedSuffix") `
            "Region-flag alias did not resolve to PNG artwork for $sequence"
        Assert-Condition ($entry[0].asset.png512 -eq $entry[0].asset.png128) `
            "Region-flag alias must share its grid and preview artwork for $sequence"
    }
    Assert-Condition ((@($entries | Where-Object { $_.subgroup -eq 'keycap' })).Count -eq 13) `
        "Unicode keycap subgroup coverage must be 13 entries"
    Assert-Condition ((@($entries | Where-Object { $_.codePoints -contains '20E3' })).Count -eq 12) `
        "Keycap sequence coverage must be 12 entries"
    Assert-Condition ((@($entries | Where-Object { $_.codePoints -contains '200D' })).Count -gt 0) `
        "ZWJ coverage is missing"
    Assert-Condition ((@($entries | Where-Object { $_.codePoints -contains 'FE0F' })).Count -gt 0) `
        "Variation-selector coverage is missing"
    Assert-Condition ((@($entries | Where-Object {
        (@($_.codePoints | Where-Object { $_ -in @('1F3FB', '1F3FC', '1F3FD', '1F3FE', '1F3FF') })).Count -gt 0
    })).Count -gt 0) "Skin-tone variant coverage is missing"

    Assert-Condition ($report.entryCount -eq 3944) "Review report entry count differs"
    Assert-Condition ($report.sharedFlagSourceCount -eq 262) "Review report flag count differs"
    Assert-Condition (@($report.assetAnomalies.aliasCollisions).Count -eq 37) `
        "Noto legacy alias review count differs"
    Assert-Condition (@($report.assetAnomalies.asymmetricAssets).Count -eq 0) `
        "Noto 128/512 coverage has asymmetric keys"
    Assert-Condition (@($report.assetAnomalies.unreferencedAssets).Count -eq 240) `
        "Unreferenced Noto source-asset review count differs"
    Assert-Condition ($sourceManifest.baseline.emoji -eq '17.0') "Generated source manifest version differs"

    foreach ($generatedFile in @($sourceManifest.generatedFiles)) {
        $path = Join-Path $firstOutput $generatedFile.path
        Assert-Condition ((Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant() -eq `
            $generatedFile.sha256) "Generated file hash differs: $($generatedFile.path)"
        Assert-Condition ((Get-Item -LiteralPath $path).Length -eq $generatedFile.byteLength) `
            "Generated file length differs: $($generatedFile.path)"
    }

    Write-Host "Generated Emoji Baseline verification passed: 3944 deterministic entries" -ForegroundColor Green
}
finally {
    foreach ($path in @($firstOutput, $secondOutput)) {
        $resolvedPath = [IO.Path]::GetFullPath($path)
        Assert-Condition $resolvedPath.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) `
            "Refusing to remove a path outside the system temporary directory"
        if (Test-Path -LiteralPath $resolvedPath) {
            Remove-Item -LiteralPath $resolvedPath -Recurse -Force
        }
    }
}
