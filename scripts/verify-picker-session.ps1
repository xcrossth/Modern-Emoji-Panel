[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solutionPath = Join-Path $repositoryRoot "ModernEmojiPanel.sln"
$executablePath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\bin\Release\net10.0-windows\win-x64\ModernEmojiPicker.exe"
$temporaryReport = Join-Path ([System.IO.Path]::GetTempPath()) ("modern-emoji-picker-session-" + [Guid]::NewGuid().ToString("N") + ".json")

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
        -ArgumentList @("--picker-session-smoke", $temporaryReport) `
        -Wait `
        -PassThru `
        -WindowStyle Hidden
    Assert-Condition ($process.ExitCode -eq 0) "Picker Session smoke failed with exit code $($process.ExitCode)"
    Assert-Condition (Test-Path -LiteralPath $temporaryReport) "Picker Session report is missing"

    $report = Get-Content -Raw -LiteralPath $temporaryReport | ConvertFrom-Json
    $checks = @($report.checks.psobject.Properties)
    Assert-Condition ($report.passed -eq $true) "Picker Session report contains a failed check"
    Assert-Condition ($checks.Count -eq 12) "Expected 12 Picker Session policy checks"
    Assert-Condition (@($checks | Where-Object { $_.Value -ne $true }).Count -eq 0) "One or more Picker Session checks failed"

    $windowXaml = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "apps\picker\EmojiPicker\MainWindow.xaml")
    $windowCode = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "apps\picker\EmojiPicker\MainWindow.xaml.cs")
    $appCode = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "apps\picker\EmojiPicker\App.xaml.cs")
    Assert-Condition ($windowXaml -match 'ResizeMode="CanResizeWithGrip"') "Picker window is not resizable"
    Assert-Condition ($windowXaml -match 'AutomationProperties.LiveSetting="Assertive"') "Accessible live status is missing"
    Assert-Condition ($windowXaml -match 'IsKeyboardFocusWithin') "Visible keyboard focus indicator is missing"
    Assert-Condition ($windowCode -match 'CommitGesture\.ShiftEnter') "Shift+Enter commit gesture is not wired"
    Assert-Condition ($windowCode -match 'PickerDismissReason\.ExternalPointer') "Outside-click focus policy is not wired"
    Assert-Condition ($windowCode -match 'TryRestoreCapturedTarget') "Explicit dismissal does not restore the captured target"
    Assert-Condition ($appCode -match 'IsPickerSessionOpen') "Repeated hotkey/session signal guard is missing"

    Write-Host "Picker Session verification passed: $($checks.Count) policy checks and WPF wiring checks" -ForegroundColor Green
}
finally {
    Pop-Location
    if (Test-Path -LiteralPath $temporaryReport) {
        Remove-Item -LiteralPath $temporaryReport -Force
    }
}
