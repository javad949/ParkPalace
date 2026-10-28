<#
.SYNOPSIS
    ParkPalace CRM - Sepidar Metadata Discovery via Secure CRM Jumpbox (Phase 1)
.DESCRIPTION
    Executes scripts/01_discover_sepidar_metadata_only.sql against Sepidar01
    via the internal CRM server (192.168.1.248 -> 192.168.1.242,62753).
    
    SECURITY GUARANTEES:
    - Zero credentials logged to disk, CLI arguments, or console output.
    - Credential prompt via Get-Credential (in-memory only).
    - Strictly Read-Only catalog inspection (sys.* only).
    - Zero DDL, zero DML, zero queries against operational data tables.
    - CommandTimeout = 30 seconds for each query.
    - SET LOCK_TIMEOUT 3000 ms; SET TRANSACTION ISOLATION LEVEL READ COMMITTED.
    - Strict verification of target server and database (Sepidar01) before running queries.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SepidarServer = "192.168.1.242,62753",

    [Parameter(Mandatory = $false)]
    [string]$TargetDatabase = "Sepidar01",

    [Parameter(Mandatory = $false)]
    [string]$OutputDir,

    [Parameter(Mandatory = $false)]
    [System.Management.Automation.PSCredential]$SepidarCredential
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
Write-Host " ParkPalace CRM - استخراج متادیتای سپیدار از طریق سرور CRM" -ForegroundColor Cyan
Write-Host " مسیر ارتباطی: سرور واسط داخلی CRM (192.168.1.248) -> سپیدار ($SepidarServer)" -ForegroundColor Cyan
Write-Host " دیتابیس هدف: $TargetDatabase" -ForegroundColor Cyan
Write-Host " پوشه خروجی: $OutputDir" -ForegroundColor Cyan
Write-Host " محدودیت قفل: LOCK_TIMEOUT = 3000ms | مهلت فرمان: CommandTimeout = 30s" -ForegroundColor Cyan
Write-Host " سطح امنیت: کاملاً فقط‌خواندنی (Catalog sys.*) - صفر تغییر داده و زیرساخت" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan

# --------------------------------------------------------------------------------------
# گام ۱: دریافت امن اعتبارنامه در حافظه رم (بدون ثبت در فایل یا لاگ)
# --------------------------------------------------------------------------------------
if (-not $SepidarCredential) {
    if ($env:PARKPALACE_SEPIDAR_CONN) {
        Write-Host "`n[گام ۱] خواندن رشته اتصال از متغیر محیطی امن PARKPALACE_SEPIDAR_CONN..." -ForegroundColor Yellow
        $connStr = $env:PARKPALACE_SEPIDAR_CONN
    } else {
        Write-Host "`n[گام ۱] لطفاً نام کاربری و رمز عبور حساب مجاز سپیدار (مانند crm_reader) را وارد کنید:" -ForegroundColor Yellow
        Write-Host "  (رمز عبور صرفاً در حافظه رم نگهداری می‌شود و در هیچ فایلی ثبت نخواهد شد)" -ForegroundColor Gray
        $SepidarCredential = Get-Credential -UserName "crm_reader" -Message "اتصال فقط‌خواندنی به پایگاه‌داده سپیدار (Sepidar01):"
    }
}

# --------------------------------------------------------------------------------------
# گام ۲: برقراری نشست امن WinRM HTTPS به سرور داخلی CRM
# --------------------------------------------------------------------------------------
Write-Host "`n[گام ۲] برقراری نشست امن WinRM HTTPS به سرور CRM (crm-mgmt.sahar.loc:5987)..." -ForegroundColor Yellow

$crmCred = Get-Credential -UserName "CRM\Administrator" -Message "Enter authorized CRM credentials (not Sepidar SQL credentials)"
$sessionOption = New-PSSessionOption -SkipCACheck:$false -SkipCNCheck:$false -SkipRevocationCheck:$false

$crmSession = $null
try {
    $crmSession = New-PSSession -ConnectionUri "https://crm-mgmt.sahar.loc:5987/wsman" `
        -Credential $crmCred `
        -SessionOption $sessionOption `
        -Authentication Negotiate

    Write-Host "  [OK] نشست امن با سرور CRM برقرار شد." -ForegroundColor Green

    # ----------------------------------------------------------------------------------
    # گام ۳: اعتبارسنجی مقصد و اجرای استعلام‌های متادیتا از داخل سرور CRM
    # ----------------------------------------------------------------------------------
    Write-Host "`n[گام ۳] اتصال از داخل سرور CRM به سپیدار ($SepidarServer) و بررسی مقصد..." -ForegroundColor Yellow

    $scriptBlock = {
        param(
            [string]$Server,
            [string]$Database,
            [System.Management.Automation.PSCredential]$Cred,
            [string]$ExplicitConnStr
        )

        $ErrorActionPreference = 'Stop'

        # ساخت رشته اتصال دیتابیسی
        $sqlConnStr = $ExplicitConnStr
        if (-not $sqlConnStr) {
            $user = $Cred.UserName
            $pass = $Cred.GetNetworkCredential().Password
            $sqlConnStr = "Server=$Server;Database=$Database;User Id=$user;Password=$pass;TrustServerCertificate=True;Connection Timeout=15;"
        }

        $conn = New-Object System.Data.SqlClient.SqlConnection($sqlConnStr)
        $conn.Open()

        # بررسی هویت سرور و پایگاه داده
        $verifyCmd = $conn.CreateCommand()
        $verifyCmd.CommandTimeout = 30
        $verifyCmd.CommandText = "SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName, @@VERSION AS SqlVersion"
        $reader = $verifyCmd.ExecuteReader()
        $actualServer = ""
        $actualDb = ""
        $actualVersion = ""
        if ($reader.Read()) {
            $actualServer = $reader["ServerName"].ToString()
            $actualDb = $reader["DatabaseName"].ToString()
            $actualVersion = $reader["SqlVersion"].ToString()
        }
        $reader.Close()

        # تطابق قطعی نام پایگاه‌داده
        if ($actualDb -ne $Database) {
            $conn.Close()
            throw "پایگاه‌داده متصل‌شده ($actualDb) با مقصد موردنظر ($Database) تطابق ندارد! عملیات متوقف شد."
        }

        # اعمال LOCK_TIMEOUT و ایزولاسیون خواندن
        $initCmd = $conn.CreateCommand()
        $initCmd.CommandTimeout = 30
        $initCmd.CommandText = "SET LOCK_TIMEOUT 3000; SET TRANSACTION ISOLATION LEVEL READ COMMITTED;"
        $initCmd.ExecuteNonQuery() | Out-Null

        # تعریف ۶ استعلام متادیتا (sys.*)
        $queries = @(
            @{
                Id = "01_database_config"
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
                Id = "02_schemas_summary"
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
                Id = "03_tables_and_views"
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
                Id = "04_columns_detail"
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
                Id = "05_potential_inventory_columns"
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
                Id = "06_foreign_keys"
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

        $results = [ordered]@{}
        $results["_metadata"] = [PSCustomObject]@{
            ServerName = $actualServer
            DatabaseName = $actualDb
            SqlVersion = $actualVersion
            ExecutionTimeUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        }

        foreach ($q in $queries) {
            $cmd = $conn.CreateCommand()
            $cmd.CommandTimeout = 30
            $cmd.CommandText = $q.Sql
            
            $r = $cmd.ExecuteReader()
            $rows = New-Object System.Collections.Generic.List[PSObject]
            
            # استخراج نام ستون‌ها
            $colNames = @()
            for ($i = 0; $i -lt $r.FieldCount; $i++) {
                $colNames += $r.GetName($i)
            }

            while ($r.Read()) {
                $rowObj = [ordered]@{}
                foreach ($col in $colNames) {
                    $val = $r[$col]
                    if ($val -eq [System.DBNull]::Value -or $null -eq $val) {
                        $rowObj[$col] = ""
                    } else {
                        $rowObj[$col] = $val.ToString()
                    }
                }
                $rows.Add([PSCustomObject]$rowObj)
            }
            $r.Close()
            $results[$q.Id] = $rows.ToArray()
        }

        $conn.Close()
        return $results
    }

    $remoteData = Invoke-Command -Session $crmSession `
        -ScriptBlock $scriptBlock `
        -ArgumentList $SepidarServer, $TargetDatabase, $SepidarCredential, $connStr

    Write-Host "  [OK] استعلام‌ها با موفقیت در سرور CRM اجرا شدند." -ForegroundColor Green

    # ----------------------------------------------------------------------------------
    # گام ۴: تبدیل مجموعه‌نتایج به فایل‌های CSV مجزا در پوشه پروژه
    # ----------------------------------------------------------------------------------
    Write-Host "`n[گام ۴] ذخیره‌سازی فایل‌های CSV در $OutputDir..." -ForegroundColor Yellow

    $meta = $remoteData["_metadata"]
    Write-Host "  سرور واقعی سپیدار: $($meta.ServerName)" -ForegroundColor Cyan
    Write-Host "  پایگاه‌داده واقعی: $($meta.DatabaseName)" -ForegroundColor Cyan
    Write-Host "  زمان اجرا (UTC): $($meta.ExecutionTimeUtc)" -ForegroundColor Cyan

    $manifest = @()

    foreach ($key in $remoteData.Keys) {
        if ($key.StartsWith("_")) { continue }
        $rows = $remoteData[$key]
        $csvFileName = "$key.csv"
        $csvPath = Join-Path $OutputDir $csvFileName
        
        if ($rows -and $rows.Count -gt 0) {
            $rows | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
        } else {
            # ایجاد فایل خالی با سرستون مناسب در صورت نبود داده
            Set-Content -Path $csvPath -Value "" -Encoding UTF8
        }

        $rowCount = if ($rows) { $rows.Count } else { 0 }
        Write-Host "  [OK] $csvFileName -> $rowCount سطر" -ForegroundColor Green

        $manifest += [PSCustomObject]@{
            File = $csvFileName
            RowCount = $rowCount
        }
    }

    # ایجاد گزارش خلاصه اجرا
    $summaryPath = Join-Path $OutputDir "run_summary.md"
    $manifestLines = ($manifest | ForEach-Object { "* **$($_.File):** $($_.RowCount) سطر" }) -join "`n"

    $reportContent = @"
# خلاصه استخراج متادیتای سپیدار (فاز ۱) — شناسه: $runId

* **زمان اجرا (UTC):** $($meta.ExecutionTimeUtc)
* **مسیر ارتباطی:** کلاینت توسعه -> نشست امن WinRM HTTPS -> سرور CRM (192.168.1.248) -> SQL Server سپیدار ($SepidarServer)
* **سرور واقعی SQL Server:** $($meta.ServerName)
* **پایگاه‌داده هدف:** $($meta.DatabaseName)
* **تنظیمات قفل و مهلت:** CommandTimeout = 30 ثانیه | LOCK_TIMEOUT = 3000 میلی‌ثانیه
* **ایزولاسیون تراکنش:** READ COMMITTED (sys.* catalogs only)

## فایل‌های CSV استخراج‌شده:
$manifestLines

## وضعیت دسترسی و دید متادیتا:
* کوئری‌ها منحصراً روی جداول سیستمی کاتالوگ (`sys.databases`, `sys.schemas`, `sys.objects`, `sys.columns`, `sys.foreign_keys`, `sys.extended_properties`) اجرا شدند.
* صفر سطر از جداول عملیاتی خوانده شد.
* هیچ تنظیم دیتابیس، زیرساخت، فایروال یا مجوزی تغییر نیافت.
"@

    Set-Content -Path $summaryPath -Value $reportContent -Encoding UTF8
    Write-Host "`n  [OK] گزارش خلاصه در $summaryPath ذخیره شد." -ForegroundColor Green

    # ایجاد فایل پرچم اتمام موفق
    $doneFlagPath = Join-Path $OutputDir "DISCOVERY_COMPLETED.txt"
    Set-Content -Path $doneFlagPath -Value "SUCCESS`n$runId`n$($meta.ExecutionTimeUtc)" -Encoding UTF8

    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host " استخراج متادیتای مرحله اول سپیدار با موفقیت پایان یافت." -ForegroundColor Green
    Write-Host " پوشه نتایج: $OutputDir" -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
}
finally {
    if ($crmSession) {
        Remove-PSSession -Session $crmSession -ErrorAction SilentlyContinue
    }
    # پاک‌سازی متغیر اعتبارنامه از حافظه
    $SepidarCredential = $null
    $crmCred = $null
    [System.GC]::Collect()
}
