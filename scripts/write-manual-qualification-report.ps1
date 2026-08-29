[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$InputPath,

    [Parameter(Mandatory)]
    [string]$SessionEnvironmentPath,

    [Parameter(Mandatory)]
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$InputPath = [IO.Path]::GetFullPath($InputPath)
$SessionEnvironmentPath = [IO.Path]::GetFullPath($SessionEnvironmentPath)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)

if (-not (Test-Path -LiteralPath $InputPath -PathType Leaf)) {
    throw "Manual qualification input is missing: $InputPath"
}
if (-not (Test-Path -LiteralPath $SessionEnvironmentPath -PathType Leaf)) {
    throw "Manual qualification session environment is missing: $SessionEnvironmentPath"
}

function Read-SessionEnvironment([string]$Path) {
    $values = @{}
    foreach ($line in Get-Content -LiteralPath $Path -Encoding utf8) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#", [StringComparison]::Ordinal)) {
            continue
        }

        $separator = $line.IndexOf('=')
        if ($separator -le 0) {
            continue
        }

        $values[$line.Substring(0, $separator)] = $line.Substring($separator + 1)
    }

    return $values
}

function Get-Value([hashtable]$Values, [string]$Name, [string]$Default = "") {
    if ($Values.ContainsKey($Name)) {
        return [string]$Values[$Name]
    }

    return $Default
}

function Get-ProductVersion([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    return (Get-Item -LiteralPath $Path).VersionInfo.ProductVersion
}

function Escape-MarkdownCell([object]$Value) {
    if ($null -eq $Value) {
        return "—"
    }

    $text = ([string]$Value).Replace("|", "\|").Replace("`r", " ").Replace("`n", " ").Trim()
    if ([string]::IsNullOrWhiteSpace($text)) {
        return "—"
    }

    return $text
}

$session = Read-SessionEnvironment $SessionEnvironmentPath
$records = @(Import-Csv -LiteralPath $InputPath -Delimiter "`t" -Encoding utf8)
if ($records.Count -eq 0) {
    throw "Manual qualification input contains no recorded cases"
}

$allowedResults = @("ผ่าน", "ไม่ผ่าน", "ทำไม่ได้ใน environment", "ยังไม่ทดสอบ")
foreach ($record in $records) {
    if ($record.Result -notin $allowedResults) {
        throw "Unsupported result '$($record.Result)' for case '$($record.CaseId)'"
    }
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ManualQualificationDpi {
    [DllImport("user32.dll")]
    public static extern uint GetDpiForSystem();
}
'@

$computer = Get-ComputerInfo
$operatingSystem = Get-CimInstance Win32_OperatingSystem
$chromePath = "C:\Program Files\Google\Chrome\Application\chrome.exe"
$notepadPath = (Get-Command notepad.exe).Source
$codeCommand = Get-Command code.cmd -ErrorAction SilentlyContinue
$codeOutput = if ($null -eq $codeCommand) { @() } else { @(& code --version) }
$codeVersion = if ($codeOutput.Count -eq 0) { $null } else { $codeOutput[0] }
$pickerExecutable = Get-Value $session "PICKER_EXECUTABLE"
$pickerHash = if (Test-Path -LiteralPath $pickerExecutable -PathType Leaf) {
    (Get-FileHash -LiteralPath $pickerExecutable -Algorithm SHA256).Hash.ToLowerInvariant()
}
else {
    $null
}

$screens = @(
    [System.Windows.Forms.Screen]::AllScreens | ForEach-Object {
        [pscustomobject]@{
            device = $_.DeviceName
            primary = $_.Primary
            bounds = [pscustomobject]@{
                x = $_.Bounds.X
                y = $_.Bounds.Y
                width = $_.Bounds.Width
                height = $_.Bounds.Height
            }
            workingArea = [pscustomobject]@{
                x = $_.WorkingArea.X
                y = $_.WorkingArea.Y
                width = $_.WorkingArea.Width
                height = $_.WorkingArea.Height
            }
        }
    }
)

$counts = [ordered]@{}
foreach ($result in $allowedResults) {
    $counts[$result] = @($records | Where-Object Result -eq $result).Count
}

$sessionId = Get-Value $session "SESSION_ID" (Get-Date -Format "yyyyMMdd-HHmmss")
$report = [ordered]@{
    schemaVersion = 1
    sessionId = $sessionId
    recordedAt = (Get-Date).ToString("o")
    source = "human-observed manual qualification wizard"
    acceptedAutomatically = $false
    reviewerNotice = "ผลนี้ต้องให้ agent/maintainer review ก่อนนำเข้า docs/qualification/manual-matrices.md"
    repository = [ordered]@{
        commit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
        branch = (& git -C $repositoryRoot branch --show-current).Trim()
        workingTreeCleanAtReport = [string]::IsNullOrWhiteSpace((& git -C $repositoryRoot status --short) -join "")
        pickerExecutable = $pickerExecutable
        pickerSha256 = $pickerHash
    }
    tester = [ordered]@{
        name = Get-Value $session "TESTER_NAME"
        inputLanguage = Get-Value $session "INPUT_LANGUAGE"
        insertionMode = Get-Value $session "INSERTION_MODE"
        reportedDpi = Get-Value $session "REPORTED_DPI"
    }
    environment = [ordered]@{
        windowsProductName = $computer.WindowsProductName
        windowsVersion = $computer.WindowsVersion
        osBuildNumber = $operatingSystem.BuildNumber
        osArchitecture = $computer.OsArchitecture
        systemDpi = [ManualQualificationDpi]::GetDpiForSystem()
        systemScalePercent = [math]::Round(([ManualQualificationDpi]::GetDpiForSystem() / 96d) * 100d)
        screens = $screens
        applications = [ordered]@{
            notepad = Get-ProductVersion $notepadPath
            chrome = Get-ProductVersion $chromePath
            visualStudioCode = $codeVersion
            windowsTerminalAvailable = $null -ne (Get-Command wt.exe -ErrorAction SilentlyContinue)
            narratorAvailable = Test-Path "$env:WINDIR\System32\Narrator.exe"
            nvdaAvailable = $null -ne (Get-Command nvda.exe -ErrorAction SilentlyContinue)
        }
    }
    summary = $counts
    cases = @(
        $records | ForEach-Object {
            [ordered]@{
                stage = [int]$_.Stage
                caseId = $_.CaseId
                name = $_.Case
                result = $_.Result
                notes = $_.Notes
                evidence = $_.Evidence
            }
        }
    )
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$jsonPath = Join-Path $OutputDirectory "manual-qualification-$sessionId.json"
$markdownPath = Join-Path $OutputDirectory "manual-qualification-$sessionId.md"
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding utf8NoBOM

$markdown = [System.Collections.Generic.List[string]]::new()
$markdown.Add("# ผล Manual Qualification — $sessionId")
$markdown.Add("")
$markdown.Add("> รายงานนี้มาจากการสังเกตของมนุษย์และยังไม่ถูกนำเข้าตารางหลักอัตโนมัติ ต้องให้ agent/maintainer review ก่อน")
$markdown.Add("")
$markdown.Add("- ผู้ทดสอบ: $(Escape-MarkdownCell $report.tester.name)")
$markdown.Add("- เวลาออกรายงาน: $($report.recordedAt)")
$markdown.Add("- Windows: $($report.environment.windowsProductName) build $($report.environment.osBuildNumber)")
$markdown.Add("- Commit: ``$($report.repository.commit)``")
$markdown.Add("- Picker SHA-256: ``$($report.repository.pickerSha256)``")
$markdown.Add("- ภาษา input: $(Escape-MarkdownCell $report.tester.inputLanguage)")
$markdown.Add("- Insertion mode: $(Escape-MarkdownCell $report.tester.insertionMode)")
$markdown.Add("- DPI/scale: $($report.environment.systemDpi) DPI / $($report.environment.systemScalePercent)%")
$markdown.Add("")
$markdown.Add("## สรุป")
$markdown.Add("")
foreach ($entry in $counts.GetEnumerator()) {
    $markdown.Add("- $($entry.Key): $($entry.Value)")
}
$markdown.Add("")
$markdown.Add("## ผลรายกรณี")
$markdown.Add("")
$markdown.Add("| Stage | Case | ผล | หมายเหตุ | หลักฐาน |")
$markdown.Add("|---:|---|---|---|---|")
foreach ($case in $report.cases) {
    $markdown.Add("| $($case.stage) | $(Escape-MarkdownCell $case.name) | $(Escape-MarkdownCell $case.result) | $(Escape-MarkdownCell $case.notes) | $(Escape-MarkdownCell $case.evidence) |")
}
$markdown.Add("")
$markdown.Add("## ข้อจำกัด")
$markdown.Add("")
$markdown.Add("- ผล `ทำไม่ได้ใน environment` และ `ยังไม่ทดสอบ` ไม่ถือว่าผ่าน")
$markdown.Add("- socket observation ไม่ใช่ packet-capture certification")
$markdown.Add("- ห้ามแก้ manual matrix เป็น `ผ่าน` โดยไม่ review วันที่ ผู้ทดสอบ environment และหลักฐาน")
$markdown | Set-Content -LiteralPath $markdownPath -Encoding utf8NoBOM

Write-Host "Manual qualification reports written:" -ForegroundColor Green
Write-Host $jsonPath -ForegroundColor Green
Write-Host $markdownPath -ForegroundColor Green
