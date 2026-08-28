[CmdletBinding()]
param(
    [ValidateSet("Ordinary", "Release")]
    [string]$VerificationMode = "Ordinary"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$manifestPath = Join-Path $repositoryRoot "vendor\emoji-baseline\sources.lock.json"
$expectedBaseline = @{
    unicode = "17.0.0"
    emoji = "17.0"
    cldr = "48.2"
    notoEmoji = "v2.051"
    notoCommit = "8998f5dd683424a73e2314a8c1f1e359c19e8742"
}

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-RepositoryPath {
    param([string]$RelativePath)

    Assert-Condition (-not [IO.Path]::IsPathRooted($RelativePath)) "Repository path must be relative: $RelativePath"
    $resolved = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $RelativePath))
    $rootWithSeparator = $repositoryRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    Assert-Condition $resolved.StartsWith($rootWithSeparator, [StringComparison]::OrdinalIgnoreCase) "Path escapes the repository: $RelativePath"
    return $resolved
}

function Get-Sha256 {
    param([string]$Path)

    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

function Read-Inventory {
    param(
        [string]$InventoryPath,
        [string]$SourceId
    )

    $records = [Collections.Generic.List[object]]::new()
    $lineNumber = 0
    foreach ($line in [IO.File]::ReadAllLines($InventoryPath)) {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        if ($line -notmatch '^([a-f0-9]{64})\t([0-9]+)\t(.+)$') {
            throw "Invalid inventory record in $SourceId at line $lineNumber"
        }

        $relativePath = $Matches[3].Replace('\', '/')
        Assert-Condition (-not [IO.Path]::IsPathRooted($relativePath)) "Inventory path must be relative in ${SourceId}: $relativePath"
        Assert-Condition (-not ($relativePath.Split('/') -contains '..')) "Inventory path escapes its destination in ${SourceId}: $relativePath"

        $records.Add([PSCustomObject]@{
            Sha256 = $Matches[1]
            ByteLength = [Int64]$Matches[2]
            RelativePath = $relativePath
        })
    }

    return $records
}

Assert-Condition (Test-Path -LiteralPath $manifestPath -PathType Leaf) "Emoji Baseline source lock is missing"
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
Assert-Condition ($manifest.schemaVersion -eq 1) "Unsupported Emoji Baseline source-lock schema"

foreach ($property in $expectedBaseline.Keys) {
    Assert-Condition ($manifest.baseline.$property -eq $expectedBaseline[$property]) "Baseline $property must be $($expectedBaseline[$property])"
}

$forbiddenUrlPattern = '(?i)(^|[/_.-])(latest|draft|beta|main|master)([/_.-]|$)'
$allAssetPaths = [Collections.Generic.List[string]]::new()
$verifiedFileCount = 0L
$verifiedByteLength = 0L

foreach ($source in $manifest.sources) {
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($source.sourceName)) "Source name is missing for $($source.id)"
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($source.version)) "Version is missing for $($source.id)"
    Assert-Condition ([Uri]::IsWellFormedUriString($source.immutableUrl, [UriKind]::Absolute)) "Immutable URL is invalid for $($source.id)"
    Assert-Condition ($source.immutableUrl -notmatch $forbiddenUrlPattern) "Moving or pre-release URL is forbidden for $($source.id): $($source.immutableUrl)"
    Assert-Condition ($source.sha256 -match '^[a-f0-9]{64}$') "SHA-256 is invalid for $($source.id)"
    Assert-Condition ([Int64]$source.byteLength -ge 0) "Byte length is invalid for $($source.id)"
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($source.licenseClass)) "License class is missing for $($source.id)"

    if ($source.kind -eq "file") {
        $path = Get-RepositoryPath $source.destination
        Assert-Condition (Test-Path -LiteralPath $path -PathType Leaf) "Vendored source is missing: $($source.destination)"
        $file = Get-Item -LiteralPath $path
        Assert-Condition ($file.Length -eq [Int64]$source.byteLength) "Byte length mismatch for $($source.id)"
        Assert-Condition ((Get-Sha256 $path) -eq $source.sha256) "SHA-256 mismatch for $($source.id)"
        $verifiedFileCount++
        $verifiedByteLength += $file.Length
        continue
    }

    Assert-Condition ($source.kind -eq "git-inventory") "Unknown source kind for $($source.id): $($source.kind)"
    Assert-Condition ($source.commit -match '^[a-f0-9]{40}$') "Pinned Git commit is invalid for $($source.id)"
    Assert-Condition ($source.tree -match '^[a-f0-9]{40}$') "Pinned Git tree is invalid for $($source.id)"
    Assert-Condition ($source.immutableUrl.Contains($source.commit, [StringComparison]::OrdinalIgnoreCase)) "Immutable URL does not contain the pinned commit for $($source.id)"

    $inventoryPath = Get-RepositoryPath $source.inventory
    Assert-Condition (Test-Path -LiteralPath $inventoryPath -PathType Leaf) "Inventory is missing for $($source.id)"
    Assert-Condition ((Get-Sha256 $inventoryPath) -eq $source.sha256) "Inventory SHA-256 mismatch for $($source.id)"

    $destinationRoot = Get-RepositoryPath $source.destinationRoot
    $records = @(Read-Inventory $inventoryPath $source.id)
    Assert-Condition ($records.Count -eq [Int64]$source.fileCount) "File count mismatch for $($source.id)"

    $expectedPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $sourceByteLength = 0L
    foreach ($record in $records) {
        Assert-Condition $expectedPaths.Add($record.RelativePath) "Duplicate inventory path for $($source.id): $($record.RelativePath)"
        $path = [IO.Path]::GetFullPath((Join-Path $destinationRoot $record.RelativePath))
        $destinationRootWithSeparator = $destinationRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
        Assert-Condition $path.StartsWith($destinationRootWithSeparator, [StringComparison]::OrdinalIgnoreCase) "Inventory path escapes its destination for $($source.id)"
        Assert-Condition (Test-Path -LiteralPath $path -PathType Leaf) "Vendored asset is missing: $($record.RelativePath)"
        $file = Get-Item -LiteralPath $path
        Assert-Condition ($file.Length -eq $record.ByteLength) "Asset byte length mismatch: $($record.RelativePath)"
        Assert-Condition ((Get-Sha256 $path) -eq $record.Sha256) "Asset SHA-256 mismatch: $($record.RelativePath)"
        $sourceByteLength += $file.Length
        $verifiedFileCount++
        $verifiedByteLength += $file.Length
        $allAssetPaths.Add([IO.Path]::GetRelativePath($repositoryRoot, $path).Replace('\', '/'))
    }

    Assert-Condition ($sourceByteLength -eq [Int64]$source.byteLength) "Aggregate byte length mismatch for $($source.id)"

    $actualPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($sparsePath in $source.sparsePaths) {
        $absoluteDirectory = [IO.Path]::GetFullPath((Join-Path $destinationRoot $sparsePath))
        $destinationRootWithSeparator = $destinationRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
        Assert-Condition $absoluteDirectory.StartsWith($destinationRootWithSeparator, [StringComparison]::OrdinalIgnoreCase) "Sparse path escapes its destination for $($source.id)"
        Assert-Condition (Test-Path -LiteralPath $absoluteDirectory -PathType Container) "Asset directory is missing for $($source.id): $sparsePath"
        foreach ($file in Get-ChildItem -LiteralPath $absoluteDirectory -File -Recurse) {
            [void]$actualPaths.Add([IO.Path]::GetRelativePath($destinationRoot, $file.FullName).Replace('\', '/'))
        }
    }

    Assert-Condition ($actualPaths.SetEquals($expectedPaths)) "Asset directory coverage differs from the locked inventory for $($source.id)"
}

if ($allAssetPaths.Count -gt 0) {
    $attributeOutput = $allAssetPaths | & git -C $repositoryRoot check-attr --stdin filter
    Assert-Condition ($LASTEXITCODE -eq 0) "Unable to inspect Git attributes for vendored assets"
    $lfsAttributes = @($attributeOutput | Where-Object { $_ -match ': filter: lfs$' })
    Assert-Condition ($lfsAttributes.Count -eq 0) "Git LFS must not be used for vendored Emoji assets"
}

$noticesPath = Join-Path $repositoryRoot "THIRD-PARTY-NOTICES.md"
Assert-Condition (Test-Path -LiteralPath $noticesPath -PathType Leaf) "THIRD-PARTY-NOTICES.md is missing"
$notices = Get-Content -Raw -LiteralPath $noticesPath
foreach ($requiredNotice in @("Unicode 17.0.0", "CLDR 48.2", "Noto Emoji v2.051", "region-flags", "Unicode-3.0", "Apache-2.0", "Public Domain")) {
    Assert-Condition $notices.Contains($requiredNotice, [StringComparison]::OrdinalIgnoreCase) "Third-party notice is missing: $requiredNotice"
}

if ($VerificationMode -eq "Release") {
    $maximumFile = Get-ChildItem -LiteralPath (Join-Path $repositoryRoot "vendor") -File -Recurse | Sort-Object Length -Descending | Select-Object -First 1
    Assert-Condition ($maximumFile.Length -lt 100MB) "A vendored file exceeds the GitHub 100 MiB per-file limit: $($maximumFile.FullName)"
}

Write-Host "Emoji Baseline $VerificationMode verification passed: $verifiedFileCount files, $verifiedByteLength bytes" -ForegroundColor Green
