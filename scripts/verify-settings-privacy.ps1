[CmdletBinding()]
param([switch]$SkipBuild)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

Push-Location $repositoryRoot
try {
    if (-not $SkipBuild) {
        & dotnet build ModernEmojiPanel.sln -c Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Release build failed" }
    }

    & dotnet run --project tests/EmojiPicker.DomainTests/EmojiPicker.DomainTests.csproj -c Release --no-build -- $repositoryRoot
    if ($LASTEXITCODE -ne 0) { throw "Settings/privacy domain verification failed" }

    $settings = Get-Content -Raw apps/picker/EmojiPicker/Settings.cs
    $app = Get-Content -Raw apps/picker/EmojiPicker/App.xaml.cs
    $logger = Get-Content -Raw apps/picker/EmojiPicker/Logger.cs
    $window = Get-Content -Raw apps/picker/EmojiPicker/SettingsWindow.xaml
    $welcome = Get-Content -Raw apps/picker/EmojiPicker/WelcomeWindow.xaml
    $startup = Get-Content -Raw apps/picker/EmojiPicker/StartupManager.cs
    $runtimeSources = (Get-ChildItem apps/picker/EmojiPicker -Filter *.cs -Recurse |
        ForEach-Object { Get-Content -Raw $_.FullName }) -join "`n"

    foreach ($field in @(
        'hotkeyEnabled', 'hotkeyGesture', 'uiLanguage', 'theme', 'globalSkinTone',
        'emojiInsertMode', 'pasteRestoreDelayMs', 'diagnosticLoggingEnabled', 'welcomeShown')) {
        Assert-Condition ($settings.Contains($field)) "Settings is missing '$field'"
    }

    Assert-Condition ($app -match 'SettingsControlModel[.]From' -and $app -match 'new SettingsWindow') "Tray does not open the single Settings control model"
    Assert-Condition ($app -match 'ShowWelcomeIfNeeded' -and $welcome -match 'Win [+] [.]' -and $welcome -match 'Classic' -and $welcome -match 'Temporary Paste') "First-run Welcome is incomplete"
    Assert-Condition ($window -match 'Clear Recent' -and $window -match 'Reset learned ranking' -and $window -match 'Clear all activity') "Activity Data controls are missing"
    Assert-Condition ($app -match 'picker[.]ClearRecentActivity' -and $app -match 'picker[.]ResetLearnedRanking' -and $app -match 'picker[.]ClearAllActivity') "Settings is not wired to Ticket 11 Activity Data APIs"
    Assert-Condition ($startup -match 'ProductIdentity[.]RunValueName' -and $startup -match 'IsInstallerManaged') "Autostart is not scoped to Modern or installer readiness"
    $setStartupCalls = ([regex]::Matches($app, 'SetUserEnabled')).Count
    Assert-Condition ($setStartupCalls -eq 1 -and $app -match '(?s)private void ShowSettings[(][)].*SetUserEnabled') "Portable startup enables autostart without user action"

    Assert-Condition ($logger -match 'public static void Initialize[(]bool enabled[)]' -and $logger -notmatch 'debug[.]enabled') "Diagnostic logging is not controlled by the opt-in setting"
    Assert-Condition ($settings -match 'DiagnosticLoggingEnabled [{] get; set; [}]') "Diagnostic logging setting is not present"
    Assert-Condition ($app -notmatch 'Logger[.]Log.*target=' -and $runtimeSources -notmatch 'Logger[.]Log(?:Always)?[(].*(?:SearchBox[.]Text|emoji[.]Character|clipboardText|windowTitle)') "A diagnostic event includes prohibited content"
    Assert-Condition ($runtimeSources -notmatch 'HttpClient|WebRequest|TelemetryClient|ApplicationInsights|Sentry|UploadAsync|SyncProvider') "Runtime contains network, telemetry, upload or sync code"
    Assert-Condition ($window -match 'no account, sync, telemetry, provider, or upload code' -and $window -match 'never search queries, selected emoji, clipboard/text, or target window names') "Settings does not explain local-only privacy limits"

    Write-Host "Settings/privacy verification passed: one Settings model, Welcome, bilingual fallback, theme/hotkey/autostart/insertion controls, Activity Data commands and opt-in metadata-only logging" -ForegroundColor Green
}
finally {
    Pop-Location
}
