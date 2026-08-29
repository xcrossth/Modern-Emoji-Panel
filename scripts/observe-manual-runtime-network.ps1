[CmdletBinding()]
param(
    [string]$ProcessName = "ModernEmojiPicker",

    [ValidateRange(10, 3600)]
    [int]$DurationSeconds = 900,

    [ValidateRange(100, 5000)]
    [int]$SampleIntervalMilliseconds = 500,

    [Parameter(Mandatory)]
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if (-not (Get-Command Get-NetTCPConnection -ErrorAction SilentlyContinue) -or
    -not (Get-Command Get-NetUDPEndpoint -ErrorAction SilentlyContinue)) {
    throw "Windows socket observation commands are unavailable"
}

$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$startedAt = Get-Date
$deadline = $startedAt.AddSeconds($DurationSeconds)
$sampleCount = 0
$seenProcessIds = [System.Collections.Generic.HashSet[int]]::new()
$observations = [System.Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)

while ((Get-Date) -lt $deadline) {
    $sampleCount++
    foreach ($process in @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue)) {
        [void]$seenProcessIds.Add($process.Id)

        foreach ($connection in @(Get-NetTCPConnection -OwningProcess $process.Id -ErrorAction SilentlyContinue)) {
            $key = "tcp|$($connection.LocalAddress)|$($connection.LocalPort)|$($connection.RemoteAddress)|$($connection.RemotePort)|$($connection.State)"
            if (-not $observations.ContainsKey($key)) {
                $observations[$key] = [pscustomobject]@{
                    firstSeen = (Get-Date).ToString("o")
                    processId = $process.Id
                    protocol = "tcp"
                    localAddress = $connection.LocalAddress
                    localPort = $connection.LocalPort
                    remoteAddress = $connection.RemoteAddress
                    remotePort = $connection.RemotePort
                    state = [string]$connection.State
                }
            }
        }

        foreach ($endpoint in @(Get-NetUDPEndpoint -OwningProcess $process.Id -ErrorAction SilentlyContinue)) {
            $key = "udp|$($endpoint.LocalAddress)|$($endpoint.LocalPort)"
            if (-not $observations.ContainsKey($key)) {
                $observations[$key] = [pscustomobject]@{
                    firstSeen = (Get-Date).ToString("o")
                    processId = $process.Id
                    protocol = "udp"
                    localAddress = $endpoint.LocalAddress
                    localPort = $endpoint.LocalPort
                    remoteAddress = $null
                    remotePort = $null
                    state = "bound"
                }
            }
        }
    }

    Start-Sleep -Milliseconds $SampleIntervalMilliseconds
}

$report = [ordered]@{
    schemaVersion = 1
    source = "manual resident-workflow socket observation"
    startedAt = $startedAt.ToString("o")
    endedAt = (Get-Date).ToString("o")
    requestedDurationSeconds = $DurationSeconds
    sampleIntervalMilliseconds = $SampleIntervalMilliseconds
    samples = $sampleCount
    processName = $ProcessName
    observedProcessIds = @($seenProcessIds | Sort-Object)
    observedSocketCount = $observations.Count
    observedSockets = @($observations.Values)
    passed = $seenProcessIds.Count -gt 0 -and $observations.Count -eq 0
    limitation = "Socket observation is not packet capture and cannot by itself certify the manual privacy matrix."
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM

if ($seenProcessIds.Count -eq 0) {
    Write-Error "No $ProcessName process was observed during the requested window"
}

Write-Host "Socket observation complete: $sampleCount samples, $($observations.Count) unique socket(s)" -ForegroundColor Green
Write-Host "Report: $OutputPath" -ForegroundColor Green
