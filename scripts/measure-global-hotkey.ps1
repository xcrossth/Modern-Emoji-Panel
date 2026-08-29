[CmdletBinding()]
param(
    [string]$OutputPath = "artifacts\ticket-13\global-hotkey-win10-19045.json",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot "ModernEmojiPanel.sln"
$executablePath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\bin\Release\net10.0-windows\win-x64\ModernEmojiPicker.exe"
$resolvedOutputPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputPath))
$outputDirectory = Split-Path -Parent $resolvedOutputPath

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw $Message
    }
}

Assert-Condition ($IsWindows) "Global hotkey qualification requires Windows"
Assert-Condition (-not (Get-Process -Name "ModernEmojiPicker" -ErrorAction SilentlyContinue)) `
    "Close the running Modern Emoji Picker before the isolated global-hotkey measurement"

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

Assert-Condition (Test-Path -LiteralPath $executablePath -PathType Leaf) "ModernEmojiPicker.exe is missing"
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
if (Test-Path -LiteralPath $resolvedOutputPath) {
    Remove-Item -LiteralPath $resolvedOutputPath -Force
}

if (-not ("ModernEmojiPicker.Qualification.ForegroundWindow" -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
namespace ModernEmojiPicker.Qualification {
    public static class ForegroundWindow {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
"@
}

$notepad = $null
try {
    $notepad = Start-Process -FilePath "$env:WINDIR\System32\notepad.exe" -PassThru
    [void]$notepad.WaitForInputIdle(5000)
    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        $notepad.Refresh()
        if ($notepad.MainWindowHandle -ne [IntPtr]::Zero) {
            break
        }
        Start-Sleep -Milliseconds 50
    } while ([DateTime]::UtcNow -lt $deadline)

    Assert-Condition ($notepad.MainWindowHandle -ne [IntPtr]::Zero) "Notepad did not expose a target window"
    [void][ModernEmojiPicker.Qualification.ForegroundWindow]::SetForegroundWindow($notepad.MainWindowHandle)
    Start-Sleep -Milliseconds 200

    $quotedReport = '"' + $resolvedOutputPath + '"'
    $smoke = Start-Process `
        -FilePath $executablePath `
        -ArgumentList @("--global-hotkey-smoke", $quotedReport, $notepad.MainWindowHandle.ToInt64().ToString()) `
        -Wait `
        -PassThru
    Assert-Condition ($smoke.ExitCode -eq 0) "Global-hotkey smoke failed with exit code $($smoke.ExitCode)"
    Assert-Condition (Test-Path -LiteralPath $resolvedOutputPath -PathType Leaf) "Global-hotkey report is missing"

    $report = Get-Content -Raw -LiteralPath $resolvedOutputPath | ConvertFrom-Json
    Assert-Condition ([bool]$report.passed) "Global-hotkey report did not pass"
    Assert-Condition ($report.target.processId -eq $notepad.Id) "The hook captured a different process"
    Assert-Condition ($report.measurement.samples -eq 20) "Global hotkey-to-visible sample count is incomplete"
    Assert-Condition ($report.measurement.p95Milliseconds -le $report.measurement.budgetMilliseconds) `
        "Global hotkey-to-visible P95 exceeded its budget"

    $computer = Get-ComputerInfo
    $report | Add-Member -NotePropertyName qualificationHost -NotePropertyValue ([pscustomobject]@{
        windowsProductName = $computer.WindowsProductName
        windowsVersion = $computer.WindowsVersion
        osBuildNumber = $computer.OsBuildNumber
        osArchitecture = $computer.OsArchitecture
        processor = @($computer.CsProcessors)[0].Name
        dotnetSdk = (& dotnet --version).Trim()
        gitCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    })
    $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resolvedOutputPath -Encoding utf8NoBOM

    Write-Host ("Global hotkey-to-visible verification passed against Notepad: P95 {0:N3} ms from {1} samples (budget {2} ms)" -f `
        $report.measurement.p95Milliseconds, $report.measurement.samples, $report.measurement.budgetMilliseconds) -ForegroundColor Green
    Write-Host "Report: $resolvedOutputPath"
}
finally {
    if ($notepad -and -not $notepad.HasExited) {
        [void]$notepad.CloseMainWindow()
        if (-not $notepad.WaitForExit(2000)) {
            Stop-Process -Id $notepad.Id -Force
        }
    }
}
