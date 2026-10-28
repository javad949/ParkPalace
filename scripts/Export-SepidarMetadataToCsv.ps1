<#
.SYNOPSIS
    ParkPalace CRM - Sepidar Metadata Discovery Export Script (Phase 1 to CSV)
.DESCRIPTION
    Runs scripts/01_discover_sepidar_metadata_only.sql against Sepidar01
    and exports all 6 result sets into clean, distinct CSV files.
    
    GUARANTEES:
    - Strictly Read-Only Metadata inspection (sys.* catalogs only).
    - ZERO queries on operational data tables.
    - CommandTimeout = 30 seconds for every query.
    - SET LOCK_TIMEOUT 3000 ms.
    - Zero credentials logged or written to output files.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$Server = "94.182.47.116,62753",

    [Parameter(Mandatory = $false)]
    [string]$Database = "Sepidar01",

    [Parameter(Mandatory = $false)]
    [string]$OutputDir,

    [Parameter(Mandatory = $false)]
    [switch]$UseIntegratedSecurity,

    [Parameter(Mandatory = $false)]
    [System.Management.Automation.PSCredential]$Credential
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

$runId = "sepidar_metadata_" + (Get-Date -Format 'yyyyMMdd_HHmmss')
if (-not $OutputDir) {
    $OutputDir = Join-Path $rootDir "docs\runs\$runId"
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " ParkPalace CRM - Sepidar Metadata Discovery Export" -ForegroundColor Cyan
Write-Host " Target Server: $Server" -ForegroundColor Cyan
Write-Host " Target Database: $Database" -ForegroundColor Cyan
Write-Host " Output Directory: $OutputDir" -ForegroundColor Cyan
Write-Host " CommandTimeout: 30s | LOCK_TIMEOUT: 3000ms" -ForegroundColor Cyan
Write-Host " Mode: Strictly Read-Only (sys.* catalogs only)" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan

# Load Microsoft.Data.SqlClient runtime assembly if needed
if (-not ([System.Management.Automation.PSTypeName]'Microsoft.Data.SqlClient.SqlConnection').Type) {
    $runtimeDll = Join-Path $rootDir "src\ParkPalace.Api\bin\Debug\net8.0\runtimes\win\lib\net6.0\Microsoft.Data.SqlClient.dll"
    if (Test-Path $runtimeDll) {
        Add-Type -Path $runtimeDll
    } else {
        $found = Get-ChildItem -Path (Join-Path $rootDir "src") -Filter "Microsoft.Data.SqlClient.dll" -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -like "*runtimes\win\lib\net6.0*" } | Select-Object -First 1
        if ($found) {
            Add-Type -Path $found.FullName
        }
    }
}

# Build secure connection string in-memory
$connBuilder = New-Object Microsoft.Data.SqlClient.SqlConnectionStringBuilder

if ($env:PARKPALACE_SEPIDAR_CONN) {
    $connBuilder.ConnectionString = $env:PARKPALACE_SEPIDAR_CONN
} else {
    $connBuilder["Server"] = $Server
    $connBuilder["Database"] = $Database
    $connBuilder["TrustServerCertificate"] = $true
    $connBuilder["Connect Timeout"] = 15

    if ($UseIntegratedSecurity) {
        $connBuilder["Integrated Security"] = $true
    } elseif ($Credential) {
        $connBuilder["User Id"] = $Credential.UserName
        $connBuilder["Password"] = $Credential.GetNetworkCredential().Password
    } else {
        Write-Host "`nدرخواست اعتبارنامه جهت اتصال به پایگاه داده سپیدار (صرفاً در حافظه، بدون ثبت در دیسک):" -ForegroundColor Cyan
        $userCred = Get-Credential -UserName "crm_reader" -Message "لطفاً حساب کاربری مجاز سپیدار (مانند crm_reader) را وارد کنید:"
        $connBuilder["User Id"] = $userCred.UserName
        $connBuilder["Password"] = $userCred.GetNetworkCredential().Password
    }
}

# Queries definition from 01_discover_sepidar_metadata_only.sql
$queries = @(
    @{
        Name = "01_database_config.csv"
        Title = "تنظیمات دیتابیس و وضعیت Snapshot Isolation"
        Sql = @"
SELECT 
    DB_NAME() AS CurrentDatabase,
    @@SERVERNAME AS ServerInstance,
    @@VERSION AS SqlServerVersion,
    snapshot_isolation_state_desc AS SnapshotIsolationState,
    is_read_committed_snapshot_on AS IsReadCommittedSnapshotOn,
    recovery_model_desc AS RecoveryModel,
    compatibility_level AS CompatibilityLevel,
    collation_name AS CollationName
FROM sys.databases
WHERE name = DB_NAME();
"@
    },
    @{
        Name = "02_schemas_summary.csv"
        Title = "خلاصه شِماها و تعداد جدول‌ها و ویوها"
        Sql = @"
SELECT 
    s.name AS SchemaName,
    COUNT(CASE WHEN o.type = 'U' THEN 1 END) AS TableCount,
    COUNT(CASE WHEN o.type = 'V' THEN 1 END) AS ViewCount
FROM sys.schemas s
LEFT JOIN sys.objects o ON s.schema_id = o.schema_id AND o.type IN ('U', 'V')
GROUP BY s.name
HAVING COUNT(o.object_id) > 0
ORDER BY TableCount DESC, ViewCount DESC, s.name;
"@
    },
    @{
        Name = "03_tables_and_views.csv"
        Title = "فهرست کلیه جداول و ویوها در شِماهای هدف"
        Sql = @"
SELECT 
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type_desc AS ObjectType,
    o.create_date AS CreateDate,
    o.modify_date AS ModifyDate,
    CAST(ep.value AS NVARCHAR(255)) AS ObjectDescription
FROM sys.objects o
INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
LEFT JOIN sys.extended_properties ep ON ep.major_id = o.object_id AND ep.minor_id = 0 AND ep.name = 'MS_Description'
WHERE s.name IN ('INV', 'FMK', 'GNR', 'FM', 'PUR', 'SLS', 'dbo')
  AND o.type IN ('U', 'V')
ORDER BY s.name, o.type, o.name;
"@
    },
    @{
        Name = "04_columns_detail.csv"
        Title = "جزئیات ستون‌های جداول و ویوها در شِماهای انبار و مالی"
        Sql = @"
SELECT 
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type_desc AS ObjectType,
    c.column_id AS ColumnID,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length AS MaxLength,
    c.precision AS Precision,
    c.scale AS Scale,
    c.is_nullable AS IsNullable,
    c.is_identity AS IsIdentity,
    CAST(ep.value AS NVARCHAR(255)) AS ColumnDescription
FROM sys.columns c
INNER JOIN sys.objects o ON c.object_id = o.object_id
INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
LEFT JOIN sys.extended_properties ep ON ep.major_id = o.object_id AND ep.minor_id = c.column_id AND ep.name = 'MS_Description'
WHERE s.name IN ('INV', 'FMK', 'GNR', 'FM')
  AND o.type IN ('U', 'V')
ORDER BY s.name, o.name, c.column_id;
"@
    },
    @{
        Name = "05_key_status_columns.csv"
        Title = "ستون‌های وضعیت، ابطال، سال مالی و موجودی"
        Sql = @"
SELECT 
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type_desc AS ObjectType,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.is_nullable AS IsNullable
FROM sys.columns c
INNER JOIN sys.objects o ON c.object_id = o.object_id
INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE s.name IN ('INV', 'FMK', 'GNR', 'FM')
  AND o.type IN ('U', 'V')
  AND (
      c.name LIKE '%Cancel%' OR
      c.name LIKE '%Status%' OR
      c.name LIKE '%State%' OR
      c.name LIKE '%Post%' OR
      c.name LIKE '%Approve%' OR
      c.name LIKE '%Voucher%' OR
      c.name LIKE '%Fiscal%' OR
      c.name LIKE '%Year%' OR
      c.name LIKE '%Date%' OR
      c.name LIKE '%Stock%' OR
      c.name LIKE '%Balance%' OR
      c.name LIKE '%Quantity%' OR
      c.name LIKE '%OnHand%' OR
      c.name LIKE '%Reserv%' OR
      c.name LIKE '%Unit%' OR
      c.name LIKE '%Item%'
  )
ORDER BY s.name, o.name, c.name;
"@
    },
    @{
        Name = "06_foreign_keys.csv"
        Title = "کلیدهای خارجی و روابط بین جداول"
        Sql = @"
SELECT 
    fk.name AS ForeignKeyName,
    OBJECT_SCHEMA_NAME(fk.parent_object_id) AS ParentSchema,
    OBJECT_NAME(fk.parent_object_id) AS ParentTable,
    COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ParentColumn,
    OBJECT_SCHEMA_NAME(fk.referenced_object_id) AS ReferencedSchema,
    OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
    COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ReferencedColumn
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
WHERE OBJECT_SCHEMA_NAME(fk.parent_object_id) IN ('INV', 'FMK', 'GNR', 'FM')
ORDER BY ParentSchema, ParentTable, fkc.constraint_column_id;
"@
    }
)

Write-Host "`nدر حال برقراری اتصال به سرور سپیدار..." -ForegroundColor Yellow
$conn = New-Object Microsoft.Data.SqlClient.SqlConnection($connBuilder.ConnectionString)

try {
    $conn.Open()
    Write-Host "  [OK] اتصال با موفقیت برقرار شد." -ForegroundColor Green

    # Set LOCK_TIMEOUT and READ COMMITTED
    $initCmd = $conn.CreateCommand()
    $initCmd.CommandTimeout = 30
    $initCmd.CommandText = "SET LOCK_TIMEOUT 3000; SET TRANSACTION ISOLATION LEVEL READ COMMITTED;"
    $initCmd.ExecuteNonQuery() | Out-Null

    $summaryList = @()

    foreach ($q in $queries) {
        Write-Host "`nدر حال اجرای استعلام: $($q.Title)..." -ForegroundColor Yellow
        $cmd = $conn.CreateCommand()
        $cmd.CommandTimeout = 30
        $cmd.CommandText = $q.Sql

        $adapter = New-Object Microsoft.Data.SqlClient.SqlDataAdapter($cmd)
        $table = New-Object System.Data.DataTable
        [void]$adapter.Fill($table)

        $targetCsv = Join-Path $OutputDir $q.Name
        $table | Export-Csv -Path $targetCsv -NoTypeInformation -Encoding UTF8

        Write-Host "  [OK] تعداد سطرهای خروجی: $($table.Rows.Count) -> $targetCsv" -ForegroundColor Green
        $summaryList += [PSCustomObject]@{
            FileName = $q.Name
            Title = $q.Title
            RowCount = $table.Rows.Count
        }
    }

    # Generate Manifest / Summary Report
    $summaryReport = @"
# خلاصه نتایج کشف متادیتای سپیدار (فاز ۱) — شناسه: $runId

* **زمان اجرا:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')
* **سرور مبدأ:** $Server
* **پایگاه‌داده مبدأ:** $Database
* **ایزولاسیون:** READ COMMITTED
* **سقف انقضا:** CommandTimeout = 30s | LOCK_TIMEOUT = 3000ms

### فایل‌های CSV تولیدشده:
| نام فایل | شرح محتوا | تعداد ردیف‌ها |
| :--- | :--- | :---: |
"@

    foreach ($s in $summaryList) {
        $summaryReport += "`n| `$($s.FileName)` | $($s.Title) | **$($s.RowCount)** |"
    }

    $summaryReport += @"

> [!NOTE]
> **ملاحظه حقوق دسترسی به متادیتا:** داده‌های فوق بر مبنای حقوق حساب اتصال‌یافته استخراج شده‌اند. دیده نشدن یک شیء لزوماً به معنای عدم وجود فیزیکی آن در سرور نیست.
"@

    $summaryReportPath = Join-Path $OutputDir "metadata_export_summary.md"
    Set-Content -Path $summaryReportPath -Value $summaryReport -Encoding UTF8

    Write-Host "`n==========================================================" -ForegroundColor Green
    Write-Host " فرآیند استخراج متادیتا به CSV با موفقیت کامل شد!" -ForegroundColor Green
    Write-Host " مسیر پوشه خروجی: $OutputDir" -ForegroundColor Green
    Write-Host " خلاصه گزارش: $summaryReportPath" -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
}
catch {
    Write-Host "`n[ERROR] خطا در برقراری اتصال یا اجرای کوئری: $($_.Exception.Message)" -ForegroundColor Red
    $errReport = @"
# گزارش خطای اتصال / استعلام متادیتای سپیدار

* **سرور:** $Server
* **دیتابیس:** $Database
* **زمان وقوع:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')
* **متن خطا (بدون اطلاعات محرمانه):**
  $($_.Exception.Message)

* **وضعیت ایمنی:** هیچ تغییری در پایگاه داده یا مجوزهای سرور ایجاد نشد.
"@
    $errPath = Join-Path $OutputDir "connection_error_report.md"
    Set-Content -Path $errPath -Value $errReport -Encoding UTF8
    Write-Host "  گزارش خطا در $errPath ذخیره شد." -ForegroundColor Yellow
    exit 1
}
finally {
    if ($conn -and $conn.State -eq 'Open') {
        $conn.Close()
    }
}
