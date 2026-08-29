[CmdletBinding()]
param(
    [string]$ChromePath = "",

    [string]$ExtensionPath = "",

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($ExtensionPath)) {
    $ExtensionPath = Join-Path $repositoryRoot "artifacts\renderer-extension\unpacked"
}
$extensionPath = [System.IO.Path]::GetFullPath($ExtensionPath)
$chromeForTestingRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot "artifacts\tooling\chrome-for-testing"))
$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$profilePath = [System.IO.Path]::GetFullPath(
    (Join-Path $temporaryRoot ("modern-renderer-chrome-" + [Guid]::NewGuid().ToString("N"))))

if ([string]::IsNullOrWhiteSpace($ChromePath)) {
    $ChromePath = Get-ChildItem -LiteralPath $chromeForTestingRoot -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName "chrome-win64\chrome.exe" } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($ChromePath) -or
    -not (Test-Path -LiteralPath $ChromePath -PathType Leaf)) {
    throw "Chrome for Testing was not found. Run scripts\install-chrome-for-testing.ps1 first."
}

if (-not $profilePath.StartsWith($temporaryRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    (Split-Path $profilePath -Leaf) -notlike "modern-renderer-chrome-*") {
    throw "Temporary Chrome profile escaped its validated root"
}

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "verify-renderer-foundation.ps1") -SkipInstall
    if ($LASTEXITCODE -ne 0) {
        throw "Renderer Extension build failed before Chrome load smoke"
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $extensionPath "manifest.json") -PathType Leaf)) {
    throw "Unpacked Renderer Extension is missing"
}

$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()

New-Item -ItemType Directory -Path $profilePath | Out-Null
$arguments = @(
    "--user-data-dir=`"$profilePath`"",
    "--remote-debugging-port=$port",
    "--load-extension=`"$extensionPath`"",
    "--disable-extensions-except=`"$extensionPath`"",
    "--no-first-run",
    "--no-default-browser-check",
    "--headless=new",
    "about:blank"
)

$chromeProcess = Start-Process `
    -FilePath $ChromePath `
    -ArgumentList $arguments `
    -PassThru `
    -WindowStyle Hidden

try {
    $version = $null
    for ($attempt = 0; $attempt -lt 50 -and $null -eq $version; $attempt++) {
        try {
            $version = Invoke-RestMethod "http://127.0.0.1:$port/json/version" -TimeoutSec 1
        }
        catch {
            Start-Sleep -Milliseconds 200
        }
    }

    if ($null -eq $version) {
        throw "Chrome DevTools endpoint did not start"
    }

    Start-Sleep -Seconds 1
    $targetResponse = Invoke-WebRequest "http://127.0.0.1:$port/json/list" -TimeoutSec 3
    $parsedTargets = ConvertFrom-Json -InputObject $targetResponse.Content
    $targets = @()
    foreach ($target in $parsedTargets) {
        $targets += $target
    }
    $extensionTargets = @($targets | Where-Object { $_.url -like "chrome-extension://*" })
    if ($extensionTargets.Count -eq 0) {
        throw "Chrome did not expose the Renderer Extension service worker"
    }

    $rendererTarget = $extensionTargets | Where-Object {
        $_.url -like "*/background/service-worker.js*"
    } | Select-Object -First 1
    if ($null -eq $rendererTarget) {
        throw "Chrome loaded an extension target, but not the Renderer Extension service worker"
    }

    Write-Host "Chrome: $($version.Browser)" -ForegroundColor Green
    Write-Host "Renderer target: $($rendererTarget.url)" -ForegroundColor Green
}
finally {
    Stop-Process -Id $chromeProcess.Id -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500

    $resolvedProfile = [System.IO.Path]::GetFullPath($profilePath)
    if ($resolvedProfile.StartsWith($temporaryRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path $resolvedProfile -Leaf) -like "modern-renderer-chrome-*") {
        Remove-Item -LiteralPath $resolvedProfile -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Renderer Extension Chrome load smoke passed" -ForegroundColor Green
