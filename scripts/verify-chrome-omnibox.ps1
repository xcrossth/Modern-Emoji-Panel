[CmdletBinding()]
param(
    [switch]$SkipBuild,

    [ValidateRange(1, 50)]
    [int]$Count = 10
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solutionPath = Join-Path $repositoryRoot "ModernEmojiPanel.sln"
$executablePath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\bin\Release\net10.0-windows\win-x64\ModernEmojiPicker.exe"
$temporaryReport = Join-Path ([System.IO.Path]::GetTempPath()) ("modern-emoji-chrome-omnibox-" + [Guid]::NewGuid().ToString("N") + ".json")

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

Push-Location $repositoryRoot
try {
    if (-not $SkipBuild) {
        & dotnet build $solutionPath --configuration Release
        if ($LASTEXITCODE -ne 0) { throw "Release build failed" }
    }

    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class ChromeOmniboxWindowActivation
{
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr window);
}
'@
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ClassNameProperty,
        "OmniboxViewViews")
    $desktop = [System.Windows.Automation.AutomationElement]::RootElement
    $windows = $desktop.FindAll(
        [System.Windows.Automation.TreeScope]::Children,
        [System.Windows.Automation.Condition]::TrueCondition)
    $omnibox = $null
    $targetWindow = [IntPtr]::Zero
    foreach ($window in $windows) {
        try {
            $process = Get-Process -Id $window.Current.ProcessId -ErrorAction Stop
            if ($process.ProcessName -ne "chrome") { continue }
            $candidate = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
            if ($null -ne $candidate -and $candidate.Current.IsKeyboardFocusable) {
                $omnibox = $candidate
                $targetWindow = [IntPtr]$window.Current.NativeWindowHandle
                break
            }
        }
        catch {
            continue
        }
    }
    Assert-Condition ($null -ne $omnibox) "ไม่พบ Chrome address bar ผ่าน UI Automation"

    # ให้ Chrome เป็น target จริงก่อนเปิด process smoke เหมือนผู้ใช้กด Win + .
    $focused = $null
    for ($attempt = 0; $attempt -lt 10; $attempt++) {
        [void][ChromeOmniboxWindowActivation]::SetForegroundWindow($targetWindow)
        try { $omnibox.SetFocus() } catch { }
        Start-Sleep -Milliseconds 100
        $focused = [System.Windows.Automation.AutomationElement]::FocusedElement
        if ($null -ne $focused -and
            $focused.Current.FrameworkId -eq "Chrome" -and
            $focused.Current.ClassName -eq "OmniboxViewViews") {
            break
        }
    }
    Assert-Condition (
        $null -ne $focused -and
        $focused.Current.FrameworkId -eq "Chrome" -and
        $focused.Current.ClassName -eq "OmniboxViewViews") `
        "Chrome address bar ยังไม่ได้ keyboard focus ก่อนเริ่มทดสอบ"
    $process = Start-Process `
        -FilePath $executablePath `
        -ArgumentList @(
            "--chrome-omnibox-regression-smoke",
            $temporaryReport,
            $Count,
            "hybrid",
            $targetWindow.ToInt64()) `
        -Wait `
        -PassThru
    Assert-Condition (Test-Path -LiteralPath $temporaryReport) "ไม่พบรายงาน Chrome omnibox regression"

    $report = Get-Content -Raw -LiteralPath $temporaryReport | ConvertFrom-Json
    if ($process.ExitCode -ne 0) {
        $detail = if ($null -ne $report.PSObject.Properties["error"]) {
            "$($report.error.type): $($report.error.Message)"
        }
        elseif ($null -ne $report.PSObject.Properties["samples"]) {
            ($report.samples | Where-Object { -not $_.exact } | Select-Object -First 1 | ConvertTo-Json -Compress)
        }
        else {
            "ไม่มีรายละเอียดเพิ่มเติม"
        }
        throw "Chrome omnibox regression ล้มเหลวด้วย exit code $($process.ExitCode): $detail"
    }
    Assert-Condition ($report.atomicTargetDetected -eq $true) "ไม่พบ atomic-text policy สำหรับ Chrome omnibox"
    Assert-Condition ($report.exactSequence -eq $true) "หัวใจขาวใน Chrome address bar ไม่ครบตามลำดับ"
    Assert-Condition ($report.replacementOrUnpairedSurrogate -eq $false) "พบ U+FFFD หรือ surrogate ที่ไม่ครบคู่"
    Assert-Condition ($report.errorVisible -eq $false) "Picker แสดง insertion error"
    Assert-Condition ($report.gridInteractive -eq $true) "Emoji grid ไม่ตอบสนองหลังทดสอบ"
    Assert-Condition ($report.passed -eq $true) "Chrome omnibox report ไม่ผ่าน"

    Write-Host "Chrome omnibox verification passed: หัวใจขาว $Count รายการ ไม่มี U+FFFD" -ForegroundColor Green
}
finally {
    Pop-Location
    if (Test-Path -LiteralPath $temporaryReport) {
        Remove-Item -LiteralPath $temporaryReport -Force
    }
}
