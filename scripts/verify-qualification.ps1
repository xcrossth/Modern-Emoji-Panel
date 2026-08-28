[CmdletBinding()]
param(
    [switch]$SkipBuild,

    [switch]$SkipRegressionSuite,

    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solutionPath = Join-Path $repositoryRoot "ModernEmojiPanel.sln"
$buildExecutable = Join-Path $repositoryRoot "apps\picker\EmojiPicker\bin\Release\net10.0-windows\win-x64\ModernEmojiPicker.exe"
$publishRoot = Join-Path $repositoryRoot "artifacts\foundation\picker-win-x64"
$publishExecutable = Join-Path $publishRoot "ModernEmojiPicker.exe"
$runtimeSourceRoot = Join-Path $repositoryRoot "apps\picker\EmojiPicker"
$pickerXamlPath = Join-Path $runtimeSourceRoot "MainWindow.xaml"
$highContrastThemePath = Join-Path $runtimeSourceRoot "Theme\HighContrastTheme.xaml"
$themeManagerPath = Join-Path $runtimeSourceRoot "ThemeManager.cs"
$publishSizeBudgetBytes = 350MB

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Invoke-CheckedScript([string]$Name, [hashtable]$Arguments = @{}) {
    $path = Join-Path $PSScriptRoot $Name
    & $path @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed"
    }
}

function Get-DirectoryLength([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        return $null
    }

    return [long]((Get-ChildItem -LiteralPath $Path -File -Recurse | Measure-Object Length -Sum).Sum)
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path ([IO.Path]::GetTempPath()) ("modern-emoji-qualification-" + [Guid]::NewGuid().ToString("N") + ".json")
}
$OutputPath = [IO.Path]::GetFullPath($OutputPath)

Push-Location $repositoryRoot
try {
    if (-not $SkipBuild) {
        & dotnet restore $solutionPath --locked-mode
        if ($LASTEXITCODE -ne 0) { throw "Locked restore failed" }
        Invoke-CheckedScript "build.ps1" @{ Configuration = "Release"; NoRestore = $true; PublishSelfContained = $true }
    }

    if (-not $SkipRegressionSuite) {
        Invoke-CheckedScript "verify-product-identity.ps1" @{ Configuration = "Release"; NoBuild = $true }
        Invoke-CheckedScript "verify-generated-emoji-baseline.ps1"
        foreach ($verification in @(
            "verify-noto-grid.ps1",
            "verify-safe-insertion.ps1",
            "verify-search-preview.ps1",
            "verify-emoji-variants.ps1",
            "verify-picker-session.ps1",
            "verify-activity-data.ps1",
            "verify-insertion-queue.ps1",
            "verify-settings-privacy.ps1"
        )) {
            Invoke-CheckedScript $verification @{ SkipBuild = $true }
        }
    }

    $executablePath = if (Test-Path -LiteralPath $publishExecutable -PathType Leaf) {
        $publishExecutable
    }
    else {
        $buildExecutable
    }
    Assert-Condition (Test-Path -LiteralPath $executablePath -PathType Leaf) "Release executable is missing"

    $pickerXaml = Get-Content -Raw -LiteralPath $pickerXamlPath
    $highContrastTheme = Get-Content -Raw -LiteralPath $highContrastThemePath
    $themeManager = Get-Content -Raw -LiteralPath $themeManagerPath
    Assert-Condition ($pickerXaml -match 'AutomationProperties[.]Name" Value="[{]Binding Name[}]"') "Emoji tiles do not expose localized accessible names"
    Assert-Condition ($pickerXaml -match 'AutomationProperties[.]LiveSetting="Assertive"') "Accessible live state is missing"
    Assert-Condition ($pickerXaml -match 'IsKeyboardFocusWithin') "Visible keyboard focus indicator is missing"
    Assert-Condition ($pickerXaml -match 'VirtualizingPanel[.]IsVirtualizing="True"' -and $pickerXaml -match 'VirtualizationMode="Recycling"') "Grid virtualization is missing"
    Assert-Condition ($themeManager -match 'SystemParameters[.]HighContrast' -and $themeManager -match 'HighContrastTheme[.]xaml') "Theme selection does not honor Windows High Contrast"
    Assert-Condition ($highContrastTheme -match 'SystemColors[.]WindowColorKey' -and $highContrastTheme -match 'SystemColors[.]HighlightColorKey') "High Contrast theme does not use Windows system colors"

    $runtimeSources = (Get-ChildItem -LiteralPath $runtimeSourceRoot -Filter *.cs -Recurse |
        ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"
    $runtimeNetworkPattern = 'System[.]Net[.]Http|HttpClient|WebRequest|WebClient|TcpClient|UdpClient|TelemetryClient|ApplicationInsights|Sentry|UploadAsync|SyncProvider'
    Assert-Condition ($runtimeSources -notmatch $runtimeNetworkPattern) "Runtime source contains network, telemetry, upload or sync APIs"

    $reportDirectory = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($reportDirectory)) {
        New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    }
    if (Test-Path -LiteralPath $OutputPath) {
        Remove-Item -LiteralPath $OutputPath -Force
    }

    $quotedReportPath = '"' + $OutputPath + '"'
    $process = Start-Process `
        -FilePath $executablePath `
        -ArgumentList @("--qualification-smoke", $quotedReportPath, "2000") `
        -PassThru `
        -WindowStyle Hidden

    $socketMonitorAvailable = (Get-Command Get-NetTCPConnection -ErrorAction SilentlyContinue) -and
        (Get-Command Get-NetUDPEndpoint -ErrorAction SilentlyContinue)
    Assert-Condition ($null -ne $socketMonitorAvailable) "Windows socket monitor commands are unavailable"

    $networkObservations = New-Object System.Collections.Generic.List[object]
    $networkSampleCount = 0
    while (-not $process.HasExited) {
        $networkSampleCount++
        foreach ($connection in @(Get-NetTCPConnection -OwningProcess $process.Id -ErrorAction SilentlyContinue)) {
            $networkObservations.Add([pscustomobject]@{
                protocol = "tcp"
                local = "$($connection.LocalAddress):$($connection.LocalPort)"
                remote = "$($connection.RemoteAddress):$($connection.RemotePort)"
                state = [string]$connection.State
            })
        }
        foreach ($endpoint in @(Get-NetUDPEndpoint -OwningProcess $process.Id -ErrorAction SilentlyContinue)) {
            $networkObservations.Add([pscustomobject]@{
                protocol = "udp"
                local = "$($endpoint.LocalAddress):$($endpoint.LocalPort)"
                remote = $null
                state = "bound"
            })
        }
        Start-Sleep -Milliseconds 50
        $process.Refresh()
    }

    Assert-Condition ($process.ExitCode -eq 0) "Qualification smoke failed with exit code $($process.ExitCode)"
    Assert-Condition (Test-Path -LiteralPath $OutputPath -PathType Leaf) "Qualification smoke report is missing"
    Assert-Condition ($networkSampleCount -ge 10) "Runtime socket observation window was too short"
    Assert-Condition ($networkObservations.Count -eq 0) "Runtime qualification observed a TCP connection or UDP endpoint"

    $report = Get-Content -Raw -LiteralPath $OutputPath | ConvertFrom-Json
    Assert-Condition ($report.passed -eq $true) "One or more Modern performance/accessibility smoke budgets failed"

    $publishBytes = Get-DirectoryLength $publishRoot
    $rawAssetBytes = Get-DirectoryLength (Join-Path $repositoryRoot "vendor\noto-emoji\v2.051")
    if ($null -ne $publishBytes) {
        Assert-Condition ($publishBytes -le $publishSizeBudgetBytes) "Self-contained publish exceeds the 350 MiB qualification budget"
    }

    $computer = Get-ComputerInfo
    [object[]]$networkObservationArray = $networkObservations.ToArray()
    $report | Add-Member -NotePropertyName qualificationHost -NotePropertyValue ([pscustomobject]@{
        windowsProductName = $computer.WindowsProductName
        windowsVersion = $computer.WindowsVersion
        osBuildNumber = $computer.OsBuildNumber
        osArchitecture = $computer.OsArchitecture
        processor = @($computer.CsProcessors)[0].Name
        totalPhysicalMemoryBytes = [long]$computer.CsTotalPhysicalMemory
        dotnetSdk = (& dotnet --version).Trim()
        gitCommit = (& git rev-parse HEAD).Trim()
    })
    $report | Add-Member -NotePropertyName runtimeNetwork -NotePropertyValue ([pscustomobject]@{
        staticRuntimeSourceScanPassed = $true
        socketMonitor = "Get-NetTCPConnection + Get-NetUDPEndpoint"
        sampleIntervalMilliseconds = 50
        samples = $networkSampleCount
        observedSocketCount = $networkObservations.Count
        observedSockets = $networkObservationArray
        passed = $networkObservations.Count -eq 0
        interpretation = "ไม่พบ TCP connection หรือ UDP endpoint ของ process ระหว่าง qualification smoke; ผลนี้ไม่ใช่ packet-capture certification"
    })
    $report | Add-Member -NotePropertyName packages -NotePropertyValue ([pscustomobject]@{
        rawNotoAssetBytes = $rawAssetBytes
        selfContainedPublishBytes = $publishBytes
        selfContainedPublishBudgetBytes = $publishSizeBudgetBytes
        selfContainedPublishPassed = $null -ne $publishBytes -and $publishBytes -le $publishSizeBudgetBytes
        portableZipBytes = $null
        installerBytes = $null
    })
    $report | Add-Member -NotePropertyName upstreamBaseline -NotePropertyValue ([pscustomobject]@{
        source = "apps/picker/CHANGELOG.md (imported Classic history)"
        reportedWarmOpenApproxMilliseconds = 35
        reportedSteadyStateOpenApproxMilliseconds = 40
        reportedIdleWorkingSetApproxMiB = 20
        searchLatency = $null
        scrollStalls = $null
        decodeCache = $null
        packageSizes = $null
    })
    $report | Add-Member -NotePropertyName automatedRegressionSuite -NotePropertyValue ([pscustomobject]@{
        runInThisInvocation = -not $SkipRegressionSuite
        scopes = @("generator", "search tiers", "ranking", "variants", "Recent", "persistence recovery", "queue", "target validation", "insertion modes", "clipboard rules", "settings/privacy")
        releasePreconditions = "รอ Ticket 14: release script, installer และ portable ZIP ยังไม่มีใน commit นี้"
    })
    $report | Add-Member -NotePropertyName unresolvedQualification -NotePropertyValue @(
        "warm hotkey-to-visible จริงยังต้องวัดด้วย global hotkey และ foreground app จริง",
        "upstream ไม่มีตัวเลข search, scroll, decode/cache และ package ที่ทำซ้ำได้ใน repository",
        "installer และ portable ZIP size รอ Ticket 14",
        "manual app/OS/accessibility/DPI/input/clipboard matrices ยังต้องทดสอบตาม docs/qualification/manual-matrices.md"
    )

    $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM

    Write-Host "Qualification automation passed: performance budgets, accessibility wiring, self-contained publish and $networkSampleCount runtime socket samples" -ForegroundColor Green
    Write-Host "Report: $OutputPath" -ForegroundColor Green
}
finally {
    Pop-Location
}
