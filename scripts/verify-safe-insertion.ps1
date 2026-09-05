[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solutionPath = Join-Path $repositoryRoot "ModernEmojiPanel.sln"
$executablePath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\bin\Release\net10.0-windows\win-x64\ModernEmojiPicker.exe"
$temporaryReport = Join-Path ([System.IO.Path]::GetTempPath()) ("modern-emoji-insertion-" + [Guid]::NewGuid().ToString("N") + ".json")

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

Push-Location $repositoryRoot
try {
    if (-not $SkipBuild) {
        & dotnet restore $solutionPath --locked-mode
        if ($LASTEXITCODE -ne 0) {
            throw "Locked restore failed"
        }

        & dotnet build $solutionPath --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "Release build failed"
        }
    }

    Assert-Condition (Test-Path -LiteralPath $executablePath) "Picker executable is missing"
    $process = Start-Process `
        -FilePath $executablePath `
        -ArgumentList @("--insertion-policy-smoke", $temporaryReport) `
        -Wait `
        -PassThru `
        -WindowStyle Hidden
    Assert-Condition ($process.ExitCode -eq 0) "Insertion policy smoke failed with exit code $($process.ExitCode)"
    Assert-Condition (Test-Path -LiteralPath $temporaryReport) "Insertion policy report is missing"

    $report = Get-Content -Raw -LiteralPath $temporaryReport | ConvertFrom-Json
    $checks = @($report.checks.psobject.Properties)
    Assert-Condition ($report.passed -eq $true) "One or more insertion policy checks failed"
    Assert-Condition ($checks.Count -eq 24) "Expected 24 insertion policy checks"
    Assert-Condition (@($checks | Where-Object { $_.Value -ne $true }).Count -eq 0) "Insertion report contains a failed check"

    $injector = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "apps\picker\EmojiPicker\TextInjector.cs")
    Assert-Condition ($injector -match "GetForegroundWindow\(\)") "Target foreground is not revalidated"
    Assert-Condition ($injector -match "GetClipboardSequenceNumber\(\)") "Clipboard sequence number is not checked"
    Assert-Condition ($injector -match "ExcludeClipboardContentFromMonitorProcessing") "Temporary Paste exclusion marker is missing"
    Assert-Condition ($injector -match "CopyExplicit") "Explicit Copy path is missing"

    Write-Host "Safe insertion verification passed: $($checks.Count) policy checks, no real input sent" -ForegroundColor Green
}
finally {
    Pop-Location
    if (Test-Path -LiteralPath $temporaryReport) {
        Remove-Item -LiteralPath $temporaryReport -Force
    }
}
