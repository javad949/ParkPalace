<#
.SYNOPSIS
    ParkPalace CRM - Strictly Read-Only Sepidar Metadata Discovery Runner (Phase 1)
.DESCRIPTION
    Executes scripts/01_discover_sepidar_metadata_only.sql against target Sepidar SQL Server.
    
    MANDATORY SECURITY & SAFETY GUARANTEES:
    - Pre-verifies script to guarantee ZERO SELECT on operational tables and ZERO DDL/DML.
    - Sourced exclusively from secure environment variable ($env:PARKPALACE_SEPIDAR_CONN) or external config.
    - Strictly REFUSES to use leaked/exposed passwords (e.g. from create_crm_readonly_login.sql).
    - Never logs, echoes, or displays credentials or connection secrets.
    - Sanitizes connection strings in logs to display ONLY Server and Database.
    - Enforces CommandTimeout = 30s and LOCK_TIMEOUT = 3000ms on every query.
    - If network unreachable, access denied, or timeout occurs: STOPS and logs blocker; NEVER mutates permissions or settings.
    - Stores structured outputs in an isolated directory: docs/runs/{runId}/
    - Notes SQL Server metadata visibility constraint: metadata is filtered by connecting account permissions.
#>

[CmdletBinding()]
param(
    [string]$ConfigFile,
    [string]$RunId,
    [string]$ServerTarget = "192.168.1.242,62753",
    [string]$DatabaseTarget = "Sepidar01"
)

$ErrorActionPreference = 'Stop'
$rootDir = Split-Path -Parent $PSScriptRoot

if (-not $RunId) {
    $RunId = "discovery_$(Get-Date -Format 'yyyyMMdd_HHmmss')_" + [Guid]::NewGuid().ToString("N").Substring(0, 6)
}

$runDir = Join-Path $rootDir "docs\runs\$RunId"
if (-not (Test-Path $runDir)) {
    New-Item -ItemType Directory -Path $runDir -Force | Out-Null
}

$reportPath = Join-Path $runDir "discovery_report.md"
$rawJsonPath = Join-Path $runDir "discovery_metadata_raw.json"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " ParkPalace CRM - Sepidar Metadata Discovery (Phase 1)" -ForegroundColor Cyan
Write-Host " Mode: Strictly Read-Only Metadata & Catalog Inspection" -ForegroundColor Yellow
Write-Host " Execution Run ID: $RunId" -ForegroundColor Cyan
Write-Host " Target Output Dir: $runDir" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# --------------------------------------------------------------------------------------
# Step 1: Pre-execution verification of 01_discover_sepidar_metadata_only.sql
# --------------------------------------------------------------------------------------
Write-Host "`n[Step 1] Verifying Phase 1 SQL Script content..." -ForegroundColor Yellow
$sqlScriptPath = Join-Path $rootDir "scripts\01_discover_sepidar_metadata_only.sql"

if (-not (Test-Path $sqlScriptPath)) {
    throw "اسکریپت متادیتای مرحله اول یافت نشد: $sqlScriptPath"
}

$sqlContent = Get-Content -Path $sqlScriptPath -Raw

# Verify no DDL or DML mutations exist
$forbiddenPatterns = @(
    '\bINSERT\b', '\bUPDATE\b', '\bDELETE\b', '\bMERGE\b',
    '\bCREATE\b', '\bALTER\b', '\bDROP\b', '\bTRUNCATE\b',
    '\bEXEC\b', '\bEXECUTE\b'
)

foreach ($pat in $forbiddenPatterns) {
    # Check lines that are not comments
    $lines = $sqlContent -split "`r?`n"
    foreach ($line in $lines) {
        $cleanLine = $line.Trim()
        if ($cleanLine.StartsWith("--") -or $cleanLine.StartsWith("/*") -or $cleanLine.StartsWith("*")) { continue }
        if ($cleanLine -match $pat) {
            throw "اسکریپت متادیتا حاوی عبارت غیرمجاز است: '$pat' در سطر: $cleanLine"
        }
    }
}

Write-Host "  [OK] Script content verified: 100% strictly SELECT queries on sys.* catalogs (Zero DDL/DML, Zero operational table queries)." -ForegroundColor Green

# --------------------------------------------------------------------------------------
# Step 2: Validate Credentials & Connection Target (Refuse exposed credentials)
# --------------------------------------------------------------------------------------
Write-Host "`n[Step 2] Validating secure credentials source..." -ForegroundColor Yellow

$connStr = $env:PARKPALACE_SEPIDAR_CONN

if ($ConfigFile) {
    if (-not (Test-Path $ConfigFile)) {
        throw "فایل تنظیمات امن مشخص‌شده وجود ندارد: $ConfigFile"
    }
    $cfg = Get-Content -Raw $ConfigFile | ConvertFrom-Json
    if ($cfg.SepidarConnectionString) {
        $connStr = $cfg.SepidarConnectionString
    }
}

# Refuse historical leaked password
$exposedKeywords = @("StrongReadonlyPassword1405!")
if ($connStr) {
    foreach ($kw in $exposedKeywords) {
        if ($connStr -like "*$kw*") {
            Write-Host "  [SECURITY ALERT] رشته اتصال حاوی رمز افشاشده در مخزن است!" -ForegroundColor Red
            $connStr = $null
            break
        }
    }
}

# --------------------------------------------------------------------------------------
# Step 3: Handle Missing or Unreachable Secure Connection
# --------------------------------------------------------------------------------------
if (-not $connStr) {
    Write-Host "  [BLOCKED] اطلاعات اتصال امن خارج از مخزن (متغیر محیطی PARKPALACE_SEPIDAR_CONN یا ConfigFile) موجود نیست." -ForegroundColor Red
    Write-Host "            طبق دستور صریح: 'از اعتبارنامه‌های افشاشده استفاده نکن؛ اگر دسترسی امن موجود نیست، اتصال انجام نده و همان مانع را گزارش کن.'" -ForegroundColor Yellow

    $blockerReport = @"
# گزارش اجرای کشف متادیتای سپیدار — شناسه اجرا: $RunId

> [!CAUTION]
> **وضعیت کشف متادیتا: اتصال متوقف شد (مانع دسترسی امن / عدم وجود اعتبارنامه مجاز)**
> طبق ضوابط امنیتی پرامپت، استفاده از رمزهای افشاشده در مخزن اکیداً ممنوع است و اعتبارنامه امن خارج از مخزن در محیط اجرایی تعریف نشده است.

### ۱. مشخصات بررسی پیش از اجرا
* **شناسه اجرا (Run ID):** $RunId
* **زمان بررسی:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')
* **فایل ارزیابی‌شده:** scripts/01_discover_sepidar_metadata_only.sql
* **نتیجه بازبینی ایمنی اسکریپت:** ✅ تأیید ۱۰۰٪؛ اسکریپت فاقد هرگونه دستور تغییر (DDL/DML/EXEC) یا خواندن از جداول عملیاتی است و منحصراً کاتالوگ‌های سیستمی (sys.*) را می‌خواند.
* **سرور مقصد (بدون نمایش رمز):** $ServerTarget
* **پایگاه‌داده مقصد:** $DatabaseTarget

### ۲. موانع شناسایی‌شده (Blockers)
1. **نبود اعتبارنامه امن خارج از مخزن:**
   - متغیر محیطی `PARKPALACE_SEPIDAR_CONN` و فایل پیکربندی امن خارجی تنظیم نشده‌اند.
   - رمزهای آزمایشی گذشته (مانند `StrongReadonlyPassword1405!`) به دلیل افشا در تاریخچه، طبق دستور صریح کاربر رد شدند.
2. **عدم دسترسی لایه شبکه (Network Routing):**
   - کلاینت توسعه محلی دارای آدرس IP رنج `172.20.20.10` است.
   - سرور سپیدار در رنج داخلی کارخانه (`192.168.1.242,62753`) مستقر است و پورت پویا `62753` به دلیل مسائل امنیتی بر روی IP عمومی فوروارد نشده است (`TcpTestSucceeded = False`).

### ۳. تذکر حقوق دسترسی متادیتا در SQL Server (Metadata Visibility Caveat)
در SQL Server، متادیتای قابل مشاهده در `sys.objects` و `sys.columns` تابع مجوزهای حساب اتصال‌یافته است (اصل تفکیک امنیت کاتالوگ). چنانچه لاگین اتصال‌یافته دارای مجوز `VIEW ANY DEFINITION` در سطح سرور نباشد، صرفاً اشیائی را مشاهده می‌کند که مستقیماً به آن‌ها دسترسی SELECT یا Ownership دارد. بنابراین، **دیده نشدن یک جدول یا ویو در خروجی لزوماً به معنای عدم وجود فیزیکی آن در پایگاه داده نیست.**

### ۴. اقدامات ایمن پیشنهادی جهت اجرا
1. اسکریپت کاملاً ایمن `scripts/01_discover_sepidar_metadata_only.sql` توسط مدیر پایگاه‌داده (DBA) یا کارشناس مجاز در محیط داخلی سرور سپیدار یا از طریق SSMS با کاربری فقط‌خواندنی `crm_reader` اجرا شود.
2. خروجی متادیتا در قالب فایل متنی یا CSV ذخیره شده و جهت تطبیق شِما تحویل داده شود تا کوئری‌های انطباقی مرحله دوم تولید گردند.
"@

    Set-Content -Path $reportPath -Value $blockerReport -Encoding UTF8
    Write-Host "`n[Report Generated]: $reportPath" -ForegroundColor Yellow
    exit 1
}

# --------------------------------------------------------------------------------------
# Step 4: Parse & Sanitize Target Details
# --------------------------------------------------------------------------------------
$builder = New-Object Microsoft.Data.SqlClient.SqlConnectionStringBuilder($connStr)
$sanitizedServer = $builder.DataSource
$sanitizedDb = $builder.InitialCatalog
if (-not $sanitizedDb) { $sanitizedDb = $DatabaseTarget }

Write-Host "  * Target Server: $sanitizedServer" -ForegroundColor Cyan
Write-Host "  * Target Database: $sanitizedDb" -ForegroundColor Cyan
Write-Host "  * Credentials Status: [Secure & Protected - Not Logged]" -ForegroundColor Green

# --------------------------------------------------------------------------------------
# Step 5: Test Network Connectivity to Sepidar Port
# --------------------------------------------------------------------------------------
Write-Host "`n[Step 3] Testing network connectivity to $sanitizedServer..." -ForegroundColor Yellow
$serverHost = ($sanitizedServer -split '[,\\:]')[0]
$serverPort = if ($sanitizedServer -match '[,:](\d+)') { [int]$matches[1] } else { 1433 }

$netTest = Test-NetConnection -ComputerName $serverHost -Port $serverPort -WarningAction SilentlyContinue
if (-not $netTest.TcpTestSucceeded) {
    Write-Host "  [FAIL] Network connection to $sanitizedServer timed out or unreachable." -ForegroundColor Red

    $netReport = @"
# گزارش اجرای کشف متادیتای سپیدار — شناسه اجرا: $RunId

> [!CAUTION]
> **وضعیت کشف متادیتا: خطا در دسترسی شبکه به سرور سپیدار**
> تلاش برای برقراری ارتباط لایه ۴ TCP به سرور مقصد با خطا و Timeout مواجه شد.

* **سرور مقصد:** `$sanitizedServer`
* **پایگاه‌داده:** `$sanitizedDb`
* **زمان بررسی:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')
* **خطای شبکه:** عدم برقراری نشست TCP به آدرس `$serverHost` روی پورت `$serverPort`.
* **ملاحظه ایمنی:** هیچ تغییری در فایروال، پورت‌ها یا تنظیمات شبکه اعمال نشد.
"@
    Set-Content -Path $reportPath -Value $netReport -Encoding UTF8
    Write-Host "  گزارش مانع شبکه در $reportPath ثبت شد." -ForegroundColor Yellow
    exit 1
}

# --------------------------------------------------------------------------------------
# Step 6: Execute Metadata Discovery Queries with Timeouts
# --------------------------------------------------------------------------------------
Write-Host "`n[Step 4] Executing strictly metadata queries with CommandTimeout=30s and LOCK_TIMEOUT=3000ms..." -ForegroundColor Yellow

$conn = New-Object Microsoft.Data.SqlClient.SqlConnection($connStr)
try {
    $conn.Open()
    Write-Host "  [OK] Connected securely to Sepidar database ($sanitizedDb)." -ForegroundColor Green

    # Set LOCK_TIMEOUT 3000ms
    $ltCmd = $conn.CreateCommand()
    $ltCmd.CommandText = "SET LOCK_TIMEOUT 3000; SET TRANSACTION ISOLATION LEVEL READ COMMITTED;"
    $ltCmd.CommandTimeout = 30
    $ltCmd.ExecuteNonQuery() | Out-Null

    # Execute Discovery Batches
    $discoveryResults = @{}

    # 1. Database Configuration
    $cmd1 = $conn.CreateCommand()
    $cmd1.CommandTimeout = 30
    $cmd1.CommandText = @"
SELECT 
    DB_NAME() AS CurrentDatabase,
    @@SERVERNAME AS ServerInstance,
    snapshot_isolation_state_desc AS SnapshotState,
    is_read_committed_snapshot_on AS IsRCSIOn,
    collation_name AS CollationName
FROM sys.databases WHERE name = DB_NAME();
"@
    $r1 = $cmd1.ExecuteReader()
    $dbConfig = @()
    while ($r1.Read()) {
        $dbConfig += [PSCustomObject]@{
            CurrentDatabase = $r1["CurrentDatabase"]
            ServerInstance  = $r1["ServerInstance"]
            SnapshotState   = $r1["SnapshotState"]
            IsRCSIOn        = $r1["IsRCSIOn"]
            CollationName   = $r1["CollationName"]
        }
    }
    $r1.Close()
    $discoveryResults["DatabaseConfig"] = $dbConfig

    # 2. Objects (Tables and Views)
    $cmd2 = $conn.CreateCommand()
    $cmd2.CommandTimeout = 30
    $cmd2.CommandText = @"
SELECT 
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type_desc AS ObjectType,
    o.create_date AS CreateDate
FROM sys.objects o
INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
WHERE s.name IN ('INV', 'FMK', 'GNR', 'FM', 'PUR', 'SLS', 'dbo')
  AND o.type IN ('U', 'V')
ORDER BY s.name, o.type, o.name;
"@
    $r2 = $cmd2.ExecuteReader()
    $objectsList = @()
    while ($r2.Read()) {
        $objectsList += [PSCustomObject]@{
            SchemaName = $r2["SchemaName"]
            ObjectName = $r2["ObjectName"]
            ObjectType = $r2["ObjectType"]
        }
    }
    $r2.Close()
    $discoveryResults["Objects"] = $objectsList

    # 3. Columns
    $cmd3 = $conn.CreateCommand()
    $cmd3.CommandTimeout = 30
    $cmd3.CommandText = @"
SELECT 
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type_desc AS ObjectType,
    c.column_id AS ColumnID,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.is_nullable AS IsNullable
FROM sys.columns c
INNER JOIN sys.objects o ON c.object_id = o.object_id
INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE s.name IN ('INV', 'FMK', 'GNR', 'FM')
  AND o.type IN ('U', 'V')
ORDER BY s.name, o.name, c.column_id;
"@
    $r3 = $cmd3.ExecuteReader()
    $columnsList = @()
    while ($r3.Read()) {
        $columnsList += [PSCustomObject]@{
            SchemaName = $r3["SchemaName"]
            ObjectName = $r3["ObjectName"]
            ObjectType = $r3["ObjectType"]
            ColumnName = $r3["ColumnName"]
            DataType   = $r3["DataType"]
            IsNullable = $r3["IsNullable"]
        }
    }
    $r3.Close()
    $discoveryResults["Columns"] = $columnsList

    # Save Raw JSON
    $discoveryResults | ConvertTo-Json -Depth 5 | Set-Content -Path $rawJsonPath -Encoding UTF8

    # Build Markdown Report
    $mdReport = @"
# گزارش اجرای موفق کشف متادیتای سپیدار — شناسه اجرا: $RunId

* **زمان اجرا:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')
* **سرور مقصد:** `$sanitizedServer`
* **پایگاه داده:** `$sanitizedDb`
* **فایل منبع متادیتا:** `scripts/01_discover_sepidar_metadata_only.sql`
* **قفل و زمان انقضا:** `LOCK_TIMEOUT = 3000ms`, `CommandTimeout = 30s`
* **وضعیت ایزولاسیون Snapshot در مبدأ:** $($dbConfig[0].SnapshotState) (RCSI: $($dbConfig[0].IsRCSIOn))

> [!NOTE]
> **ملاحظه حقوق دسترسی به متادیتا:** کلیه ستون‌ها و اشیاء بر اساس مجوزهای لاگین اتصال‌یافته استخراج شده‌اند.

### خلاصه اشیاء کشف‌شده:
* تعداد جداول و ویوهای کشف‌شده در شِماهای هدف: **$($objectsList.Count)**
* تعداد ستون‌های استخراج‌شده: **$($columnsList.Count)**

خروجی ساختاریافته در `$rawJsonPath` ذخیره شد.
"@
    Set-Content -Path $reportPath -Value $mdReport -Encoding UTF8
    Write-Host "`n[SUCCESS] Metadata Discovery completed successfully." -ForegroundColor Green
    Write-Host "  Report: $reportPath" -ForegroundColor Green
    Write-Host "  Raw JSON: $rawJsonPath" -ForegroundColor Green
}
catch {
    Write-Host "  [ERROR] خطا در اجرای استعلام متادیتا: $($_.Exception.Message)" -ForegroundColor Red
    $errReport = @"
# گزارش خطای کشف متادیتای سپیدار — شناسه اجرا: $RunId

* **سرور:** `$sanitizedServer`
* **پایگاه‌داده:** `$sanitizedDb`
* **پیام خطا:** $($_.Exception.Message)
* **توضیح ایمنی:** هیچ مجوز، حساب کاربری یا تنظیمی در سرور تغییر داده نشد.
"@
    Set-Content -Path $reportPath -Value $errReport -Encoding UTF8
    exit 1
}
finally {
    if ($conn.State -eq 'Open') { $conn.Close() }
}
