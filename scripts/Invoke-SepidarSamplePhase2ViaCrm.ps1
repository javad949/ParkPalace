<#
.SYNOPSIS
    ParkPalace CRM - Sepidar Sample Phase 2 Queries 1-4 via Secure CRM Jumpbox
.DESCRIPTION
    Executes ONLY Queries 1 to 4 from scripts/02_sepidar_sample_queries_pending_verification.sql
    against Sepidar01 via the internal CRM server (192.168.1.248 -> 192.168.1.242,62753):
      1. FMK.FiscalYear (Fiscal Year list & time bounds)
      2. INV.Stock (Warehouses list & active status)
      3. ACC.Voucher Metadata (Catalog inspection of ACC.Voucher columns)
      4. INV.Unit (Units of measure list)

    SECURITY GUARANTEES:
    - Zero credentials logged to disk, CLI arguments, or output.
    - Credential prompt via Get-Credential (in-memory only).
    - Queries 5-8 are STRICTLY EXCLUDED.
    - Zero DDL, zero DML, zero changes to operational data or DB settings.
    - CommandTimeout = 30s per query; SET LOCK_TIMEOUT 3000 ms.
    - Strict verification of target database (Sepidar01) before execution.
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

$runId = "sepidar_sample_phase2_" + (Get-Date -Format 'yyyyMMdd_HHmmss')
if (-not $OutputDir) {
    $OutputDir = Join-Path $rootDir "docs\runs\$runId"
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " ParkPalace CRM - اجرای کوئری‌های ۱ تا ۴ نمونه‌برداری سپیدار" -ForegroundColor Cyan
Write-Host " مسیر: کلاینت توسعه -> نشست امن CRM (192.168.1.248) -> سپیدار ($SepidarServer)" -ForegroundColor Cyan
Write-Host " دیتابیس هدف: $TargetDatabase" -ForegroundColor Cyan
Write-Host " پوشه خروجی: $OutputDir" -ForegroundColor Cyan
Write-Host " محدودیت قفل: LOCK_TIMEOUT = 3000ms | مهلت فرمان: CommandTimeout = 30s" -ForegroundColor Cyan
Write-Host " محدوده مجاز: صرفاً کوئری‌های ۱ تا ۴ (دوره‌ها، انبارها، متادیتای سند حسابداری، واحدها)" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan

# --------------------------------------------------------------------------------------
# گام ۱: دریافت امن اعتبارنامه در حافظه رم
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
    # گام ۳: اعتبارسنجی مقصد و اجرای کوئری‌های ۱ تا ۴ از داخل سرور CRM
    # ----------------------------------------------------------------------------------
    Write-Host "`n[گام ۳] اتصال از سرور CRM به سپیدار ($SepidarServer) و اجرای کوئری‌های ۱ تا ۴..." -ForegroundColor Yellow

    $scriptBlock = {
        param(
            [string]$Server,
            [string]$Database,
            [System.Management.Automation.PSCredential]$Cred,
            [string]$ExplicitConnStr
        )

        $ErrorActionPreference = 'Stop'

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

        if ($actualDb -ne $Database) {
            $conn.Close()
            throw "پایگاه‌داده متصل‌شده ($actualDb) با مقصد موردنظر ($Database) تطابق ندارد! عملیات متوقف شد."
        }

        # اعمال LOCK_TIMEOUT و ایزولاسیون خواندن متعهد
        $initCmd = $conn.CreateCommand()
        $initCmd.CommandTimeout = 30
        $initCmd.CommandText = "SET LOCK_TIMEOUT 3000; SET TRANSACTION ISOLATION LEVEL READ COMMITTED;"
        $initCmd.ExecuteNonQuery() | Out-Null

        # ۴ کوئری مجاز
        $queries = @(
            @{
                Id = "01_fiscal_years"
                Title = "فهرست سال‌های مالی و مرزهای زمانی"
                Sql = @"
SELECT TOP 10
    FiscalYearId,
    Title,
    StartDate,
    EndDate,
    CAST(StartDate AS TIME) AS StartTimePart,
    CAST(EndDate AS TIME) AS EndTimePart,
    Status AS FiscalYearStatus
FROM FMK.FiscalYear
ORDER BY FiscalYearId DESC;
"@
            },
            @{
                Id = "02_stocks"
                Title = "فهرست انبارها و وضعیت فعال بودن"
                Sql = @"
SELECT 
    StockID,
    Code,
    Title,
    IsActive
FROM INV.Stock
ORDER BY StockID;
"@
            },
            @{
                Id = "03_acc_voucher_columns"
                Title = "متادیتای ستون‌های جدول سند حسابداری (sys.columns)"
                Sql = @"
SELECT 
    s.name AS SchemaName,
    o.name AS ObjectName,
    c.column_id AS ColumnID,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.is_nullable AS IsNullable
FROM sys.columns c
INNER JOIN sys.objects o ON c.object_id = o.object_id
INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE s.name = 'ACC'
  AND o.name = 'Voucher'
ORDER BY c.column_id;
"@
            },
            @{
                Id = "04_units"
                Title = "فهرست کامل واحدهای سنجش کالا"
                Sql = @"
SELECT 
    UnitID,
    Title,
    Title_En
FROM INV.Unit
ORDER BY UnitID;
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

    Write-Host "  [OK] کوئری‌های ۱ تا ۴ با موفقیت در سرور CRM اجرا شدند." -ForegroundColor Green

    # ----------------------------------------------------------------------------------
    # گام ۴: تبدیل نتایج به ۴ فایل CSV مجزا در پوشه پروژه
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
# خلاصه نتایج نمونه‌برداری فاز ۲ (کوئری‌های ۱ تا ۴) — شناسه: $runId

* **زمان اجرا (UTC):** $($meta.ExecutionTimeUtc)
* **مسیر ارتباطی:** کلاینت توسعه -> نشست امن WinRM HTTPS -> سرور CRM (192.168.1.248) -> SQL Server سپیدار ($SepidarServer)
* **سرور واقعی SQL Server:** $($meta.ServerName)
* **پایگاه‌داده هدف:** $($meta.DatabaseName)
* **تنظیمات قفل و مهلت:** CommandTimeout = 30 ثانیه | LOCK_TIMEOUT = 3000 میلی‌ثانیه
* **ایزولاسیون تراکنش:** READ COMMITTED

## فایل‌های CSV استخراج‌شده:
$manifestLines

## توضیحات امنیتی و دامنه اجرا:
* صرفاً کوئری‌های ۱ تا ۴ با مجوز صریح اجرا شدند.
* کوئری‌های ۵ تا ۸ اجرا نشدند.
* متادیتای ACC.Voucher صرفاً از کاتالوگ سیستمی sys.columns استخراج گردید (صفر سطر از رکوردهای مالی/حسابداری خوانده شد).
* هیچ تنظیم دیتابیس یا زیرساختی تغییر نیافت.
"@

    Set-Content -Path $summaryPath -Value $reportContent -Encoding UTF8
    Write-Host "`n  [OK] گزارش خلاصه در $summaryPath ذخیره شد." -ForegroundColor Green

    # ایجاد فایل پرچم اتمام
    $doneFlagPath = Join-Path $OutputDir "SAMPLE_PHASE2_COMPLETED.txt"
    Set-Content -Path $doneFlagPath -Value "SUCCESS`n$runId`n$($meta.ExecutionTimeUtc)" -Encoding UTF8

    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host " استخراج ۴ کوئری مجاز با موفقیت پایان یافت." -ForegroundColor Green
    Write-Host " پوشه نتایج: $OutputDir" -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
}
finally {
    if ($crmSession) {
        Remove-PSSession -Session $crmSession -ErrorAction SilentlyContinue
    }
    $SepidarCredential = $null
    $crmCred = $null
    [System.GC]::Collect()
}
