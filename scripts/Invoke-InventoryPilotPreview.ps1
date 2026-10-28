<#
.SYNOPSIS
    Manual Read-Only Preview Tool for ParkPalace CRM - Sepidar Inventory Pilot.
.DESCRIPTION
    Invokes the read-only preview tool via ParkPalace.Worker CLI with isolated run paths,
    strict data provenance, and credentials loaded securely from environment variables
    or external configuration files (never passed as CLI arguments).
    
    GUARANTEES:
    - Pure preview execution: ZERO database modifications to CRM or Sepidar.
    - No 'Apply' execution path is available in this phase.
    - Preserves CRM ReservedQuantity and IncomingQuantity.
    - Credentials are never logged, displayed, or passed via process command-line arguments.
    - Every run outputs to an independent, timestamped run directory without deleting previous history.
    - Tracks and records the exact SHA256 hash of the executing binary.
#>

[CmdletBinding()]
param(
    [string]$WarehouseCode = "WH-MAIN",
    [string]$SourceWarehouseId = "1",
    [string]$FiscalYearTitle = "1405",
    [string]$OutputJson,
    [string]$OutputReport,
    [string]$ConfigFile,
    [string]$RunId,
    [string]$CsvDir,
    [switch]$SimulatedTest,
    [switch]$VerifySemanticContract,
    [switch]$RunVerificationTests
)

$ErrorActionPreference = 'Stop'
$rootDir = Split-Path -Parent $PSScriptRoot

if (-not $RunId) {
    $RunId = "$(Get-Date -Format 'yyyyMMdd_HHmmss')_" + [Guid]::NewGuid().ToString("N").Substring(0, 6)
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " ParkPalace CRM - Limited Inventory Pilot Preview Tool" -ForegroundColor Cyan
Write-Host " Mode: Strictly Read-Only Preview (Zero Database Writes)" -ForegroundColor Yellow
Write-Host " Target Warehouse: $WarehouseCode (Source ID: $SourceWarehouseId)" -ForegroundColor Cyan
Write-Host " Target Fiscal Year: $FiscalYearTitle" -ForegroundColor Cyan
Write-Host " Execution Run ID: $RunId" -ForegroundColor Cyan

if ($CsvDir) {
    Write-Host " Data Provenance: CSV_EMPIRICAL_SAMPLES ($CsvDir)" -ForegroundColor Magenta
} elseif ($SimulatedTest) {
    Write-Host " Data Provenance: SIMULATED_TEST (نمونه آزمایشی شبیه‌سازی‌شده محلی)" -ForegroundColor Magenta
} else {
    Write-Host " Data Provenance: LIVE_READONLY_CONNECTION" -ForegroundColor Green
}
Write-Host "==========================================================" -ForegroundColor Cyan

# Step 1: Verification Tests (Optional flag)
if ($RunVerificationTests) {
    Write-Host "`n[Step 1] Running verification tests for Pilot Kernel & SQL Server..." -ForegroundColor Yellow
    dotnet test "$rootDir\tests\ParkPalace.UnitTests\ParkPalace.UnitTests.csproj" --filter "FullyQualifiedName~InventoryPilot|FullyQualifiedName~LivePilotEntryGuard|FullyQualifiedName~PilotManifestExtractor" --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "Unit tests failed. Aborting preview."
    }

    dotnet test "$rootDir\tests\ParkPalace.IntegrationTests\ParkPalace.IntegrationTests.csproj" --filter "FullyQualifiedName~InventoryPilotSqlServerTests" --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "SQL Server integration tests failed. Aborting preview."
    }
    Write-Host "  [OK] All verification tests passed successfully." -ForegroundColor Green
}

# Step 2: Validate Connection Prerequisites
# Securely load config from external file if provided
if ($ConfigFile) {
    if (-not (Test-Path $ConfigFile)) {
        throw "فایل تنظیمات امن مشخص‌شده وجود ندارد: $ConfigFile"
    }
    $configContent = Get-Content -Raw $ConfigFile | ConvertFrom-Json
    if ($configContent.CrmConnectionString) {
        $env:PARKPALACE_CRM_CONN = $configContent.CrmConnectionString
    }
    if ($configContent.SepidarConnectionString) {
        $env:PARKPALACE_SEPIDAR_CONN = $configContent.SepidarConnectionString
    }
}

if (-not $SimulatedTest -and -not $CsvDir) {
    if (-not $env:PARKPALACE_CRM_CONN) {
        throw "متغیر محیطی 'PARKPALACE_CRM_CONN' یا فایل تنظیمات امن خارجی مشخص نشده است. اتصال پیش‌فرض به CRM محلی حذف شده است."
    }
    if (-not $env:PARKPALACE_SEPIDAR_CONN) {
        throw "متغیر محیطی 'PARKPALACE_SEPIDAR_CONN' یا فایل تنظیمات امن خارجی مشخص نشده است. برای اجرای نمونه آزمایشی محلی، از سوئیچ -SimulatedTest یا پارامتر -CsvDir استفاده کنید."
    }
}

# Step 3: Build & Track Binary Provenance
Write-Host "`n[Step 2] Verifying and building latest binary..." -ForegroundColor Yellow
dotnet build "$rootDir\src\ParkPalace.Worker\ParkPalace.Worker.csproj" -c Debug --nologo -v q
if ($LASTEXITCODE -ne 0) {
    throw "کامپایل باینری Worker با خطا مواجه شد."
}

$workerDll = Join-Path $rootDir "src\ParkPalace.Worker\bin\Debug\net8.0\ParkPalace.Worker.dll"
if (Test-Path $workerDll) {
    $dllHash = (Get-FileHash -Path $workerDll -Algorithm SHA256).Hash
    $dllTime = (Get-Item $workerDll).LastWriteTimeUtc
    Write-Host "  [Binary Verified]: $workerDll" -ForegroundColor Green
    Write-Host "  [Binary SHA256]: $dllHash (Built: $dllTime UTC)" -ForegroundColor Green
} else {
    throw "باینری کامپایل‌شده Worker یافت نشد: $workerDll"
}

# Step 4: Prepare Isolated Run Paths (Never delete previous history)
$runDir = Join-Path $rootDir "docs\runs\$RunId"
if (-not (Test-Path $runDir)) {
    New-Item -ItemType Directory -Path $runDir -Force | Out-Null
}

$finalJsonPath = if ($OutputJson) { (Join-Path $rootDir $OutputJson) } else { (Join-Path $runDir "pilot_preview_output.json") }
$finalReportPath = if ($OutputReport) { (Join-Path $rootDir $OutputReport) } else { (Join-Path $runDir "pilot_preview_report.md") }

# Safety Checks: Prevent overwriting existing reports and protect sensitive configuration targets
if ($ConfigFile) {
    $fullConfig = (Resolve-Path $ConfigFile -ErrorAction SilentlyContinue)
    $configPathStr = if ($fullConfig) { $fullConfig.Path } else { [System.IO.Path]::GetFullPath($ConfigFile) }
    if ($finalJsonPath -eq $configPathStr -or $finalReportPath -eq $configPathStr) {
        throw "مسیر فایل خروجی نمی‌تواند با فایل تنظیمات حساس یکسان باشد: $ConfigFile"
    }
}

$sensitivePatterns = @("appsettings*.json", "*.config", "*.cer", "*.pfx", "*.key", "*.env*", "secrets*.json")
foreach ($pat in $sensitivePatterns) {
    if ((Split-Path -Leaf $finalJsonPath) -like $pat -or (Split-Path -Leaf $finalReportPath) -like $pat) {
        throw "مسیر خروجی مشخص‌شده یک فایل تنظیمات حساس، کلید امنیتی یا سرتیفیکیت است."
    }
}

if (Test-Path $finalJsonPath) {
    throw "فایل خروجی JSON از قبل در مسیر وجود دارد: $finalJsonPath . بازنویسی گزارش‌های قبلی حتی با شناسه اجرای یکسان مجاز نیست."
}
if (Test-Path $finalReportPath) {
    throw "فایل گزارش Markdown از قبل در مسیر وجود دارد: $finalReportPath . بازنویسی گزارش‌های قبلی حتی با شناسه اجرای یکسان مجاز نیست."
}

Write-Host "`n[Step 3] Executing Preview CLI Runner (Credentials kept in process memory)..." -ForegroundColor Yellow

$cliArgs = @(
    "run", "--project", "$rootDir\src\ParkPalace.Worker", "--no-build", "--",
    "--pilot-preview",
    "--run-id", $RunId,
    "--warehouse", $WarehouseCode,
    "--source-wh", $SourceWarehouseId,
    "--fiscal-year", $FiscalYearTitle,
    "--output-json", $finalJsonPath,
    "--output-report", $finalReportPath
)

if ($ConfigFile) {
    $cliArgs += @("--config-file", $ConfigFile)
}
if ($CsvDir) {
    $absCsvDir = if ([System.IO.Path]::IsPathRooted($CsvDir)) { $CsvDir } else { (Join-Path $rootDir $CsvDir) }
    $cliArgs += @("--csv-dir", $absCsvDir)
}
if ($SimulatedTest) {
    $cliArgs += "--simulated-test"
}
if ($VerifySemanticContract) {
    $cliArgs += "--verify-semantic-contract"
}

& dotnet $cliArgs
$exitCode = $LASTEXITCODE

if ($exitCode -ne 0 -and $exitCode -ne 2) {
    throw "Preview execution aborted with critical exit code: $exitCode"
}

# Step 5: Verify Generated Artifacts
Write-Host "`n[Step 4] Checking Generated Run Artifacts:" -ForegroundColor Yellow
if (-not (Test-Path $finalJsonPath)) {
    throw "خروجی JSON پیش‌نمایش در مسیر مورد انتظار تولید نشد: $finalJsonPath"
}
if (-not (Test-Path $finalReportPath)) {
    throw "گزارش Markdown پیش‌نمایش در مسیر مورد انتظار تولید نشد: $finalReportPath"
}

$jsonItem = Get-Item $finalJsonPath
$reportItem = Get-Item $finalReportPath

Write-Host "  [JSON Output]: $finalJsonPath ($($jsonItem.Length) bytes, Created: $($jsonItem.LastWriteTime))" -ForegroundColor Green
Write-Host "  [Markdown Report]: $finalReportPath ($($reportItem.Length) bytes, Created: $($reportItem.LastWriteTime))" -ForegroundColor Green

if ($exitCode -eq 2) {
    Write-Host "`n[Notice] پیش‌نمایش تولید شد اما به دلیل عدم اعتبارسنجی قرارداد معنایی در محیط عملیاتی، وضعیت آن 'غیرقابل اجرا' (Unexecutable) تعیین گردید." -ForegroundColor Yellow
} else {
    Write-Host "`n[Summary] Preview successfully executed and recorded under Run ID: $RunId" -ForegroundColor Cyan
}

Write-Host "Strict Read-Only Guarantee: Zero database modifications performed. No 'Apply' execution path available." -ForegroundColor Green

exit $exitCode
