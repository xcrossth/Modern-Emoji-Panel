[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$templatePath = Join-Path $repositoryRoot ".agents\skills\wizard\template.sh"
$wizardPath = Join-Path $PSScriptRoot "manual-qualification-wizard.sh"
$reportWriterPath = Join-Path $PSScriptRoot "write-manual-qualification-report.ps1"
$networkObserverPath = Join-Path $PSScriptRoot "observe-manual-runtime-network.ps1"
$bashPath = "C:\Program Files\Git\bin\bash.exe"

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw $Message
    }
}

foreach ($path in @($templatePath, $wizardPath, $reportWriterPath, $networkObserverPath, $bashPath)) {
    Assert-Condition (Test-Path -LiteralPath $path -PathType Leaf) "Required manual qualification file is missing: $path"
}

$templateLines = Get-Content -LiteralPath $templatePath -Encoding utf8
$wizardLines = Get-Content -LiteralPath $wizardPath -Encoding utf8
$templateMarker = ($templateLines | Select-String '^TOTAL_STAGES=1$').LineNumber
$wizardMarker = ($wizardLines | Select-String '^TOTAL_STAGES=7$').LineNumber
Assert-Condition ($templateMarker -gt 0 -and $templateMarker -eq $wizardMarker) "Wizard library line count differs from template.sh"
for ($index = 0; $index -lt ($templateMarker - 1); $index++) {
    Assert-Condition ($templateLines[$index] -ceq $wizardLines[$index]) "Wizard library differs from template.sh at line $($index + 1)"
}

& $bashPath -n $wizardPath
Assert-Condition ($LASTEXITCODE -eq 0) "bash -n failed for manual qualification wizard"

$wizardText = Get-Content -Raw -LiteralPath $wizardPath -Encoding utf8
Assert-Condition (([regex]::Matches($wizardText, '(?m)^stage "')).Count -eq 7) "Wizard does not contain exactly seven authored stages"
Assert-Condition ($wizardText -match 'DurationSeconds 900') "Privacy observation is not fixed at 15 minutes"
Assert-Condition ($wizardText -match 'artifacts/ticket-13/manual') "Wizard does not keep reports under the ignored Ticket 13 artifact path"
foreach ($result in @("ผ่าน", "ไม่ผ่าน", "ทำไม่ได้ใน environment", "ยังไม่ทดสอบ")) {
    Assert-Condition ($wizardText.Contains($result, [StringComparison]::Ordinal)) "Wizard result vocabulary is missing '$result'"
}

foreach ($path in @($reportWriterPath, $networkObserverPath)) {
    $tokens = $null
    $parseErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$parseErrors)
    Assert-Condition (@($parseErrors).Count -eq 0) "PowerShell parser failed for $path"
}

$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testRoot = [IO.Path]::GetFullPath((Join-Path $temporaryRoot ("mep-manual-report-test-" + [Guid]::NewGuid().ToString("N"))))
Assert-Condition ($testRoot.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) "Report smoke path is outside the temporary directory"

try {
    New-Item -ItemType Directory -Path $testRoot | Out-Null
    $inputPath = Join-Path $testRoot "input.tsv"
    $sessionPath = Join-Path $testRoot "session.env"
    $outputPath = Join-Path $testRoot "out"
    $tab = [char]9
    @(
        "Stage${tab}CaseId${tab}Case${tab}Result${tab}Notes${tab}Evidence",
        "1${tab}test-case${tab}กรณีทดสอบ${tab}ผ่าน${tab}ข้อมูลจำลอง${tab}fixture"
    ) | Set-Content -LiteralPath $inputPath -Encoding utf8NoBOM
    @(
        "SESSION_ID=report-smoke",
        "TESTER_NAME=Codex smoke",
        "INPUT_LANGUAGE=Thai + English",
        "INSERTION_MODE=Hybrid",
        "REPORTED_DPI=100%",
        "PICKER_EXECUTABLE=$((Join-Path $repositoryRoot 'artifacts\foundation\picker-win-x64\ModernEmojiPicker.exe'))"
    ) | Set-Content -LiteralPath $sessionPath -Encoding utf8NoBOM

    & $reportWriterPath -InputPath $inputPath -SessionEnvironmentPath $sessionPath -OutputDirectory $outputPath
    $reportPath = Join-Path $outputPath "manual-qualification-report-smoke.json"
    Assert-Condition (Test-Path -LiteralPath $reportPath -PathType Leaf) "Report writer did not create JSON output"
    $report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
    Assert-Condition ($report.cases.Count -eq 1) "Report writer emitted the wrong case count"
    Assert-Condition ($report.cases[0].result -eq "ผ่าน") "Report writer did not preserve the Thai result"
    Assert-Condition ($report.acceptedAutomatically -eq $false) "Manual report must never be accepted automatically"
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}

Write-Host "Manual qualification wizard verification passed: template library, seven stages, syntax, vocabulary and report smoke" -ForegroundColor Green
