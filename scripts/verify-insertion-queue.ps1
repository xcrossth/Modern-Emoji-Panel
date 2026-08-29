[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solutionPath = Join-Path $repositoryRoot "ModernEmojiPanel.sln"
$executablePath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\bin\Release\net10.0-windows\win-x64\ModernEmojiPicker.exe"
$temporaryReport = Join-Path ([System.IO.Path]::GetTempPath()) ("modern-emoji-picker-queue-" + [Guid]::NewGuid().ToString("N") + ".json")

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
        if ($LASTEXITCODE -ne 0) { throw "Locked restore failed" }
        & dotnet build $solutionPath --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Release build failed" }
    }

    $process = Start-Process `
        -FilePath $executablePath `
        -ArgumentList @("--insertion-queue-smoke", $temporaryReport) `
        -Wait `
        -PassThru `
        -WindowStyle Hidden
    Assert-Condition ($process.ExitCode -eq 0) "Insertion Queue smoke failed with exit code $($process.ExitCode)"
    Assert-Condition (Test-Path -LiteralPath $temporaryReport) "Insertion Queue report is missing"

    $report = Get-Content -Raw -LiteralPath $temporaryReport | ConvertFrom-Json
    $checks = @($report.checks.psobject.Properties)
    Assert-Condition ($report.passed -eq $true) "Insertion Queue report contains a failed check"
    Assert-Condition ($checks.Count -eq 26) "Expected 26 Insertion Queue and Typing Handoff checks"
    Assert-Condition (@($checks | Where-Object { $_.Value -ne $true }).Count -eq 0) "One or more queue checks failed"

    $windowXaml = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "apps\picker\EmojiPicker\MainWindow.xaml")
    $windowCode = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "apps\picker\EmojiPicker\MainWindow.xaml.cs")
    $appCode = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "apps\picker\EmojiPicker\App.xaml.cs")
    Assert-Condition ($windowXaml -match 'PreviewTextInput="MainWindow_PreviewTextInput"') "Committed TextInput handoff is not wired"
    Assert-Condition ($windowXaml -match 'x:Name="InsertionQueueStatusText"') "Visible queue status is missing"
    Assert-Condition ($windowXaml -match 'Insertion queue status[\s\S]*AutomationProperties.LiveSetting="Assertive"') "Queue accessibility live state is missing"
    Assert-Condition ($windowCode -match 'MaxPendingInsertions = 20') "Insertion Queue capacity is not fixed at 20"
    Assert-Condition ($windowCode -match 'TryCaptureCommittedText\(e\.Text') "Typing Handoff does not capture committed text"
    Assert-Condition ($windowCode -match 'TryCaptureKeyStroke') "Typing Handoff does not capture physical keys"
    Assert-Condition ($windowCode -match 'StopAndCancelPending') "Dismissal does not cancel pending work"
    Assert-Condition ($windowCode -match 'TryInsertAsync\([\s\S]*payload\.CommittedText') "Committed text is not sent through target validation"
    Assert-Condition ($windowCode -match 'TrySendKeyStrokeAsync') "Physical keys are not sent through target validation"
    Assert-Condition ($appCode -match 'RequestProcessExit') "Tray Exit does not wait for active insertion"

    Write-Host "Insertion Queue verification passed: $($checks.Count) deterministic queue, text, physical-key and WPF wiring checks" -ForegroundColor Green
}
finally {
    Pop-Location
    if (Test-Path -LiteralPath $temporaryReport) {
        Remove-Item -LiteralPath $temporaryReport -Force
    }
}
