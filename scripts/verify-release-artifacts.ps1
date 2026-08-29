[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [string]$ArtifactRoot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    $ArtifactRoot = Join-Path $repositoryRoot "artifacts\release\picker-v$Version"
}
$ArtifactRoot = [System.IO.Path]::GetFullPath($ArtifactRoot)
$manifestPath = Join-Path $ArtifactRoot "release-manifest.json"
$checksumsPath = Join-Path $ArtifactRoot "SHA256SUMS.txt"
$portableName = "Modern-Emoji-Picker-v$Version-portable-win-x64.zip"
$installerName = "Modern-Emoji-Picker-v$Version-setup-win-x64.exe"
$portablePath = Join-Path $ArtifactRoot $portableName
$installerPath = Join-Path $ArtifactRoot $installerName

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Get-PeMachine {
    param([string]$Path)
    $stream = [System.IO.File]::OpenRead($Path)
    $reader = [System.IO.BinaryReader]::new($stream)
    try {
        Assert-Condition ($reader.ReadUInt16() -eq 0x5A4D) "$Path is not a PE file"
        $stream.Position = 0x3C
        $peOffset = $reader.ReadUInt32()
        $stream.Position = $peOffset
        Assert-Condition ($reader.ReadUInt32() -eq 0x00004550) "$Path has no PE signature"
        return $reader.ReadUInt16()
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

Assert-Condition (Test-Path -LiteralPath $manifestPath) "Release manifest is missing"
Assert-Condition (Test-Path -LiteralPath $checksumsPath) "SHA256SUMS.txt is missing"
Assert-Condition (Test-Path -LiteralPath $portablePath) "Portable ZIP is missing"
Assert-Condition (Test-Path -LiteralPath $installerPath) "Inno installer is missing"
Assert-Condition (-not (Get-ChildItem -LiteralPath $ArtifactRoot -File -Recurse -Filter "*.msi")) "Release output contains an MSI"
$unsupportedArtifacts = Get-ChildItem -LiteralPath $ArtifactRoot -File -Recurse |
    Where-Object Name -Match '(?i)(framework[-_. ]?dependent|setup[-_.]?lite|[-_.]lite[-_.])'
Assert-Condition (-not $unsupportedArtifacts) "Release output contains a framework-dependent or lite artifact"

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
Assert-Condition ($manifest.schemaVersion -eq 1) "Release manifest schema is unsupported"
Assert-Condition ($manifest.product -eq "Modern Emoji Picker") "Release manifest product identity is incorrect"
Assert-Condition ($manifest.version -eq $Version) "Release manifest version is incorrect"
Assert-Condition ($manifest.targetFramework -eq "net10.0-windows") "Release manifest framework is incorrect"
Assert-Condition ($manifest.runtimeIdentifier -eq "win-x64") "Release manifest RID is incorrect"
Assert-Condition ($manifest.selfContained -eq $true) "Release manifest must declare self-contained output"
Assert-Condition ($manifest.uploaded -eq $false) "Local artifact manifest must not claim an upload"

$portableHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $portablePath).Hash.ToLowerInvariant()
$installerHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $installerPath).Hash.ToLowerInvariant()
$portableRecord = @($manifest.artifacts | Where-Object type -EQ "portable-zip")
$installerRecord = @($manifest.artifacts | Where-Object type -EQ "inno-per-user-installer")
Assert-Condition ($portableRecord.Count -eq 1 -and $portableRecord[0].sha256 -eq $portableHash) "Portable ZIP hash does not match the manifest"
Assert-Condition ($installerRecord.Count -eq 1 -and $installerRecord[0].sha256 -eq $installerHash) "Installer hash does not match the manifest"
$checksums = Get-Content -Raw -LiteralPath $checksumsPath
Assert-Condition ($checksums.Contains("$portableHash  $portableName")) "Portable ZIP hash is missing from SHA256SUMS.txt"
Assert-Condition ($checksums.Contains("$installerHash  $installerName")) "Installer hash is missing from SHA256SUMS.txt"

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($portablePath)
try {
    $entries = @($archive.Entries | ForEach-Object FullName)
    foreach ($required in @("ModernEmojiPicker.exe", "LICENSE", "THIRD-PARTY-NOTICES.md", "README-th.md", "coreclr.dll")) {
        Assert-Condition ($entries -contains $required) "Portable ZIP is missing $required"
    }
    Assert-Condition (-not ($entries | Where-Object { $_ -match '(?i)ClassicEmojiPicker|\.msi$|lite|framework-dependent' })) "Portable ZIP contains a legacy or unsupported payload"

    $temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("modern-emoji-release-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
    try {
        [System.IO.Compression.ZipFile]::ExtractToDirectory($portablePath, $temporaryDirectory)
        $portableExe = Join-Path $temporaryDirectory "ModernEmojiPicker.exe"
        Assert-Condition ((Get-PeMachine -Path $portableExe) -eq 0x8664) "Portable executable is not x64"
        $smoke = Start-Process -FilePath $portableExe -ArgumentList "--product-identity-smoke",(Join-Path $temporaryDirectory "identity.json") -Wait -PassThru -WindowStyle Hidden
        Assert-Condition ($smoke.ExitCode -eq 0) "Portable product identity smoke failed"
    }
    finally {
        if (Test-Path -LiteralPath $temporaryDirectory) {
            Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
        }
    }
}
finally {
    $archive.Dispose()
}

$releaseScript = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "release.ps1")
$innoScript = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "apps\picker\installer\EmojiPicker.iss")
Assert-Condition ($releaseScript -notmatch '(?i)\bgh\b|git\s+(?:tag|push)|Invoke-WebRequest|Start-BitsTransfer') "Local package script contains upload/tag/network commands"
Assert-Condition ($innoScript -notmatch '(?i)FrameworkDependent|setup-lite|\.msi') "Inno script still exposes a framework-dependent/MSI route"
Assert-Condition ($innoScript -match 'PrivilegesRequired=lowest' -and $innoScript -notmatch 'PrivilegesRequiredOverridesAllowed') "Installer must be per-user only"
Assert-Condition ($innoScript -match 'modern-emoji-picker\.ico') "Installer does not use the new Modern product icon"
Assert-Condition ((Get-PeMachine -Path $installerPath) -in @(0x014C, 0x8664)) "Installer is not a Windows executable"

Write-Host "Release artifact verification passed: self-contained x64 ZIP, per-user Inno installer, notices, hashes and local-only policy" -ForegroundColor Green
