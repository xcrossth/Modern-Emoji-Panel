[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$ProgressPreference = "SilentlyContinue"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$manifestPath = Join-Path $repositoryRoot "vendor\emoji-baseline\sources.lock.json"
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$stagingRoot = [IO.Path]::GetFullPath((Join-Path $temporaryRoot ("modern-emoji-baseline-update-" + [Guid]::NewGuid().ToString("N"))))

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

function Get-StagingPath {
    param([string]$RelativePath)

    Assert-Condition (-not [IO.Path]::IsPathRooted($RelativePath)) "Staging path must be relative: $RelativePath"
    $resolved = [IO.Path]::GetFullPath((Join-Path $stagingRoot $RelativePath))
    $rootWithSeparator = $stagingRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    Assert-Condition $resolved.StartsWith($rootWithSeparator, [StringComparison]::OrdinalIgnoreCase) "Path escapes the staging directory: $RelativePath"
    return $resolved
}

function Assert-File {
    param(
        [string]$Path,
        [Int64]$ExpectedLength,
        [string]$ExpectedSha256,
        [string]$SourceId
    )

    Assert-Condition (Test-Path -LiteralPath $Path -PathType Leaf) "Downloaded file is missing for $SourceId"
    $file = Get-Item -LiteralPath $Path
    Assert-Condition ($file.Length -eq $ExpectedLength) "Downloaded byte length mismatch for $SourceId"
    $sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
    Assert-Condition ($sha256 -eq $ExpectedSha256) "Downloaded SHA-256 mismatch for $SourceId"
}

Assert-Condition (Test-Path -LiteralPath $manifestPath -PathType Leaf) "Emoji Baseline source lock is missing"
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
$forbiddenUrlPattern = '(?i)(^|[/_.-])(latest|draft|beta|main|master)([/_.-]|$)'

foreach ($source in $manifest.sources) {
    Assert-Condition ($source.immutableUrl -notmatch $forbiddenUrlPattern) "Moving or pre-release URL is forbidden for $($source.id)"
    Assert-Condition ($source.sha256 -match '^[a-f0-9]{64}$') "Expected SHA-256 is invalid for $($source.id)"
}

New-Item -ItemType Directory -Path $stagingRoot | Out-Null
try {
    foreach ($source in $manifest.sources) {
        if ($source.kind -eq "file") {
            $stagedPath = Get-StagingPath $source.destination
            New-Item -ItemType Directory -Force -Path (Split-Path $stagedPath -Parent) | Out-Null
            Write-Host "Downloading $($source.id)"
            Invoke-WebRequest -UseBasicParsing -Uri $source.immutableUrl -OutFile $stagedPath
            Assert-File $stagedPath ([Int64]$source.byteLength) $source.sha256 $source.id
            continue
        }

        Assert-Condition ($source.kind -eq "git-inventory") "Unknown source kind for $($source.id)"
        Assert-Condition ($source.commit -match '^[a-f0-9]{40}$') "Pinned Git commit is invalid for $($source.id)"
        Assert-Condition ($source.immutableUrl.Contains($source.commit, [StringComparison]::OrdinalIgnoreCase)) "Immutable URL does not contain the pinned commit for $($source.id)"

        $checkoutPath = Join-Path $stagingRoot ("checkout-" + $source.id)
        New-Item -ItemType Directory -Path $checkoutPath | Out-Null
        Push-Location $checkoutPath
        try {
            & git init --quiet
            & git remote add origin $source.repositoryUrl
            & git sparse-checkout init --cone
            & git sparse-checkout set @($source.sparsePaths)
            & git -c protocol.version=2 fetch --depth=1 origin $source.commit
            Assert-Condition ($LASTEXITCODE -eq 0) "Unable to fetch pinned commit for $($source.id)"
            & git -c advice.detachedHead=false checkout --quiet FETCH_HEAD
            Assert-Condition ($LASTEXITCODE -eq 0) "Unable to check out pinned commit for $($source.id)"
            $checkedOutCommit = (& git rev-parse HEAD).Trim()
            $checkedOutTree = (& git rev-parse 'HEAD^{tree}').Trim()
            Assert-Condition ($checkedOutCommit -eq $source.commit) "Checked-out commit differs for $($source.id)"
            Assert-Condition ($checkedOutTree -eq $source.tree) "Checked-out tree differs for $($source.id)"
        }
        finally {
            Pop-Location
        }

        $inventoryPath = Get-RepositoryPath $source.inventory
        Assert-File $inventoryPath (Get-Item -LiteralPath $inventoryPath).Length $source.sha256 "$($source.id) inventory"
        $recordCount = 0L
        $aggregateLength = 0L
        foreach ($line in [IO.File]::ReadAllLines($inventoryPath)) {
            if ([string]::IsNullOrWhiteSpace($line)) {
                continue
            }
            Assert-Condition ($line -match '^([a-f0-9]{64})\t([0-9]+)\t(.+)$') "Invalid inventory record for $($source.id)"
            $recordCount++
            $relativePath = $Matches[3].Replace('\', '/')
            $sourcePath = [IO.Path]::GetFullPath((Join-Path $checkoutPath $relativePath))
            $checkoutWithSeparator = $checkoutPath.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
            Assert-Condition $sourcePath.StartsWith($checkoutWithSeparator, [StringComparison]::OrdinalIgnoreCase) "Inventory path escapes checkout for $($source.id)"
            Assert-File $sourcePath ([Int64]$Matches[2]) $Matches[1] "$($source.id):$relativePath"

            $stagedRelativePath = Join-Path $source.destinationRoot $relativePath
            $stagedPath = Get-StagingPath $stagedRelativePath
            New-Item -ItemType Directory -Force -Path (Split-Path $stagedPath -Parent) | Out-Null
            Copy-Item -LiteralPath $sourcePath -Destination $stagedPath -Force
            $aggregateLength += [Int64]$Matches[2]
        }

        Assert-Condition ($recordCount -eq [Int64]$source.fileCount) "Downloaded file count mismatch for $($source.id)"
        Assert-Condition ($aggregateLength -eq [Int64]$source.byteLength) "Downloaded aggregate length mismatch for $($source.id)"
    }

    if (-not $PSCmdlet.ShouldProcess($repositoryRoot, "replace vendored Emoji Baseline files after checksum verification")) {
        return
    }

    foreach ($source in $manifest.sources) {
        if ($source.kind -eq "file") {
            $stagedPath = Get-StagingPath $source.destination
            $destinationPath = Get-RepositoryPath $source.destination
            New-Item -ItemType Directory -Force -Path (Split-Path $destinationPath -Parent) | Out-Null
            Copy-Item -LiteralPath $stagedPath -Destination $destinationPath -Force
            continue
        }

        $destinationRoot = Get-RepositoryPath $source.destinationRoot
        $expectedPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($line in [IO.File]::ReadAllLines((Get-RepositoryPath $source.inventory))) {
            if ($line -match '^([a-f0-9]{64})\t([0-9]+)\t(.+)$') {
                [void]$expectedPaths.Add($Matches[3].Replace('\', '/'))
                $stagedPath = Get-StagingPath (Join-Path $source.destinationRoot $Matches[3])
                $destinationPath = [IO.Path]::GetFullPath((Join-Path $destinationRoot $Matches[3]))
                $destinationRootWithSeparator = $destinationRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
                Assert-Condition $destinationPath.StartsWith($destinationRootWithSeparator, [StringComparison]::OrdinalIgnoreCase) "Destination path escapes vendor root for $($source.id)"
                New-Item -ItemType Directory -Force -Path (Split-Path $destinationPath -Parent) | Out-Null
                Copy-Item -LiteralPath $stagedPath -Destination $destinationPath -Force
            }
        }

        foreach ($sparsePath in $source.sparsePaths) {
            $absoluteDirectory = [IO.Path]::GetFullPath((Join-Path $destinationRoot $sparsePath))
            if (Test-Path -LiteralPath $absoluteDirectory) {
                foreach ($file in Get-ChildItem -LiteralPath $absoluteDirectory -File -Recurse) {
                    $relativePath = [IO.Path]::GetRelativePath($destinationRoot, $file.FullName).Replace('\', '/')
                    if (-not $expectedPaths.Contains($relativePath)) {
                        Remove-Item -LiteralPath $file.FullName -Force
                    }
                }
            }
        }
    }

    & (Join-Path $PSScriptRoot "verify-emoji-baseline.ps1")
    Assert-Condition ($LASTEXITCODE -eq 0) "Vendored baseline failed offline verification after update"
    Write-Host "Emoji Baseline update completed from pinned sources" -ForegroundColor Green
}
finally {
    $resolvedStagingRoot = [IO.Path]::GetFullPath($stagingRoot)
    Assert-Condition $resolvedStagingRoot.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) "Refusing to remove a staging directory outside the system temporary directory"
    if (Test-Path -LiteralPath $resolvedStagingRoot) {
        Remove-Item -LiteralPath $resolvedStagingRoot -Recurse -Force
    }
}
