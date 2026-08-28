[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repositoryRoot "apps\picker\EmojiPicker\EmojiPicker.csproj"
$innoPath = Join-Path $repositoryRoot "apps\picker\installer\EmojiPicker.iss"
$wixPath = Join-Path $repositoryRoot "apps\picker\installer\msi\Package.wxs"
$appSourcePath = Join-Path $repositoryRoot "apps\picker\EmojiPicker"
$artifactDirectory = Join-Path $repositoryRoot "artifacts\ticket-02"
$reportPath = Join-Path $artifactDirectory "identity-smoke.json"

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-ContainsLiteral {
    param(
        [string]$Text,
        [string]$Expected,
        [string]$Label
    )

    Assert-True ($Text.Contains($Expected, [System.StringComparison]::Ordinal)) `
        "$Label ไม่มีค่า identity ที่คาดไว้: $Expected"
}

Push-Location $repositoryRoot
try {
    [xml]$project = Get-Content -Raw -LiteralPath $projectPath
    $properties = $project.Project.PropertyGroup | Where-Object { $_.AssemblyName } | Select-Object -First 1
    Assert-True ($properties.AssemblyName -eq "ModernEmojiPicker") "AssemblyName ต้องเป็น ModernEmojiPicker"
    Assert-True ($properties.AssemblyTitle -eq "Modern Emoji Picker") "AssemblyTitle ไม่ใช่ Modern"
    Assert-True ($properties.AssemblyProduct -eq "Modern Emoji Picker") "AssemblyProduct ไม่ใช่ Modern"
    Assert-True ($properties.Company -eq "X CroSs") "Publisher ใน assembly ไม่ตรงกับเจ้าของ repository"
    Assert-True ($null -eq $properties.SelectSingleNode("ApplicationIcon")) `
        "Modern ต้องไม่ reuse application icon ของ Classic"

    $inno = Get-Content -Raw -LiteralPath $innoPath
    Assert-ContainsLiteral $inno 'AppId={{6AFB6AF4-F41A-412A-8749-9BF9FD673855}' "Inno"
    Assert-ContainsLiteral $inno '#define AppExe "ModernEmojiPicker.exe"' "Inno"
    Assert-ContainsLiteral $inno 'ValueName: "ModernEmojiPicker"' "Inno"
    Assert-ContainsLiteral $inno 'OutputBaseFilename=Modern-Emoji-Picker-' "Inno"
    Assert-True (-not $inno.Contains("B6C3E1A2-7F4D-4C9E-9B21-1E2A3C4D5E6F")) `
        "Inno AppId ยังซ้ำกับ Classic"

    $wix = Get-Content -Raw -LiteralPath $wixPath
    Assert-ContainsLiteral $wix 'UpgradeCode="EB407AE1-9D49-43A7-AA0A-208EC973479E"' "WiX"
    Assert-ContainsLiteral $wix 'Target="ModernEmojiPicker.exe"' "WiX"
    Assert-ContainsLiteral $wix 'Name="ModernEmojiPicker" Type="string"' "WiX"
    Assert-ContainsLiteral $wix 'Name="Modern Emoji Picker"' "WiX"
    Assert-True (-not $wix.Contains("899B683B-F905-46AC-A590-05616BFCA4C7")) `
        "WiX UpgradeCode ยังซ้ำกับ Classic"
    [xml]$wixDocument = $wix
    Assert-True ($null -ne $wixDocument.DocumentElement) "WiX XML ไม่สมบูรณ์"

    foreach ($fileName in @("Settings.cs", "Logger.cs", "MainWindow.xaml.cs")) {
        $content = Get-Content -Raw -LiteralPath (Join-Path $appSourcePath $fileName)
        Assert-True (-not $content.Contains('"ClassicEmojiPicker"')) `
            "$fileName ยังอ้าง data directory ของ Classic"
        Assert-ContainsLiteral $content "ProductIdentity.DataDirectory" $fileName
    }

    foreach ($activeIdentityFile in @($projectPath, $innoPath, $wixPath)) {
        $content = Get-Content -Raw -LiteralPath $activeIdentityFile
        Assert-True (-not $content.Contains("Resources\app.ico")) `
            "$activeIdentityFile ยัง reuse icon ของ Classic"
    }

    if (-not $NoBuild) {
        & dotnet restore "ModernEmojiPanel.sln" --locked-mode
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore ล้มเหลว"
        }

        & dotnet build "ModernEmojiPanel.sln" --configuration $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build ล้มเหลว"
        }
    }

    $executable = Join-Path $repositoryRoot `
        "apps\picker\EmojiPicker\bin\$Configuration\net10.0-windows\win-x64\ModernEmojiPicker.exe"
    Assert-True (Test-Path -LiteralPath $executable) "ไม่พบ ModernEmojiPicker.exe"

    New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
    $smokeStartedAt = [DateTime]::UtcNow.AddSeconds(-1)
    $process = Start-Process `
        -FilePath $executable `
        -ArgumentList @("--product-identity-smoke", "`"$reportPath`"") `
        -Wait `
        -PassThru `
        -WindowStyle Hidden
    Assert-True ($process.ExitCode -eq 0) "runtime identity smoke ล้มเหลวด้วย exit code $($process.ExitCode)"
    Assert-True (Test-Path -LiteralPath $reportPath) "runtime identity smoke ไม่สร้างรายงาน"
    Assert-True ((Get-Item -LiteralPath $reportPath).LastWriteTimeUtc -ge $smokeStartedAt) `
        "runtime identity smoke ไม่ได้อัปเดตรายงานรอบนี้"

    $report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
    Assert-True ([bool]$report.passed) "runtime identity smoke รายงานว่าไม่ผ่าน"
    Assert-True ([bool]$report.identityIsIsolated) "runtime identity ยังปะปนกับ Classic"
    Assert-True ([bool]$report.singleInstanceSignal) "secondary-launch signal ไม่ผ่าน"
    Assert-True ([bool]$report.namedMutexProbe) "Windows named-mutex probe ไม่ผ่าน"
    Assert-True ([bool]$report.conflictPositive -and [bool]$report.conflictNegative) `
        "Classic conflict detection seam ไม่ผ่าน"

    Write-Host "ตรวจ product identity และ lifecycle smoke ผ่าน" -ForegroundColor Green
    Write-Host "รายงาน: $reportPath"
}
finally {
    Pop-Location
}
