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
$desktopReport = Join-Path ([System.IO.Path]::GetTempPath()) ("modern-emoji-picker-desktop-regression-" + [Guid]::NewGuid().ToString("N") + ".json")

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
    Assert-Condition ($checks.Count -eq 31) "Expected 31 Insertion Queue and Typing Handoff checks"
    Assert-Condition (@($checks | Where-Object { $_.Value -ne $true }).Count -eq 0) "One or more queue checks failed"

    $desktopProcess = Start-Process `
        -FilePath $executablePath `
        -ArgumentList @("--desktop-regression-smoke", $desktopReport) `
        -Wait `
        -PassThru `
        -WindowStyle Hidden
    Assert-Condition (Test-Path -LiteralPath $desktopReport) "Desktop focus/rapid-insertion report is missing"
    $desktop = Get-Content -Raw -LiteralPath $desktopReport | ConvertFrom-Json
    if ($desktopProcess.ExitCode -ne 0) {
        $failedChecks = @(
            "accessibilityFocusCaptured",
            "editableStateRestored",
            "exactSequence",
            "replacementOrUnpairedSurrogate",
            "errorVisible",
            "gridInteractive",
            "dismissWorked",
            "highContrastThemeApplied",
            "highContrastEnterWorked",
            "highContrastShiftEnterWorked"
        ) | Where-Object {
            $value = $desktop.$_
            if ($_ -in @("replacementOrUnpairedSurrogate", "errorVisible")) { $value -eq $true }
            else { $value -ne $true }
        }
        throw "Desktop focus/rapid-insertion smoke failed with exit code $($desktopProcess.ExitCode): $($failedChecks -join ', ')"
    }
    Assert-Condition ($desktop.accessibilityFocusCaptured -eq $true) "The exact accessibility focus element was not captured"
    Assert-Condition ($desktop.editableStateRestored -eq $true) "Collapsed address/search edit state was not restored"
    Assert-Condition ($desktop.exactSequence -eq $true) "Rapid pointer insertion changed or dropped Unicode sequences"
    Assert-Condition ($desktop.replacementOrUnpairedSurrogate -eq $false) "Rapid pointer insertion emitted a replacement or unpaired surrogate"
    Assert-Condition ($desktop.errorVisible -eq $false) "Rapid pointer insertion displayed an insertion error"
    Assert-Condition ($desktop.gridInteractive -eq $true) "Rapid pointer insertion left the Emoji grid non-interactive"
    Assert-Condition ($desktop.dismissWorked -eq $true) "Picker could not dismiss after rapid pointer insertion"
    Assert-Condition ($desktop.highContrastThemeApplied -eq $true) "Desktop smoke did not apply the High Contrast resource theme"
    Assert-Condition ($desktop.highContrastEnterWorked -eq $true) "Search Enter failed under the High Contrast resource theme"
    Assert-Condition ($desktop.highContrastShiftEnterWorked -eq $true) "Search Shift+Enter failed under the High Contrast resource theme"
    Assert-Condition ($desktop.passed -eq $true) "Desktop focus/rapid-insertion report failed"

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
    Assert-Condition ($windowCode -match 'SetPointerActivationSuppressed\(true\)') "Rapid insertion does not suppress pointer activation while the target owns foreground"
    Assert-Condition ($windowCode -match 'PreviousAccessibilityFocus') "Insertion does not restore the captured accessibility focus element"
    Assert-Condition ($windowCode -match 'TryInsertAsync\([\s\S]*payload\.CommittedText') "Committed text is not sent through target validation"
    Assert-Condition ($windowCode -match 'TrySendKeyStrokeAsync') "Physical keys are not sent through target validation"
    Assert-Condition ($appCode -match 'RequestProcessExit') "Tray Exit does not wait for active insertion"

    Write-Host "Insertion Queue verification passed: $($checks.Count) deterministic checks plus real WPF focus restore and 15-item rapid insertion" -ForegroundColor Green
}
finally {
    Pop-Location
    if (Test-Path -LiteralPath $temporaryReport) {
        Remove-Item -LiteralPath $temporaryReport -Force
    }
    if (Test-Path -LiteralPath $desktopReport) {
        Remove-Item -LiteralPath $desktopReport -Force
    }
}
