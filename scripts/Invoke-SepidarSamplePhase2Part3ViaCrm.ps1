<#
.SYNOPSIS
    ParkPalace CRM - Sepidar Sample Phase 2 (Part 3 - Supplementary) via Secure CRM Jumpbox
.DESCRIPTION
    Supplementary candidate runner script for:
      1. INV.ItemStockSummary (KG items only: UnitRef=1, max 20 items, FY=10, Stock=1)
      2. INV.InventoryReceipt (Sample document numbers for each Type/IsReturn combination)
      3. INV.InventoryDelivery (Sample document numbers for each Type/IsReturn combination)

    SECURITY & INTEGRITY GUARANTEES:
    - Pure read-only sampling; zero DDL, zero DML, zero mutations.
    - Zero descriptions, zero financial amounts/prices, zero personal data.
    - CommandTimeout = 30s per query; SET LOCK_TIMEOUT 3000 ms; READ COMMITTED.
    - SqlDataReader.IsDBNull(ordinal) extraction logic preserving True, False, 0, and NULL.
    - UTF-8 with BOM encoding for all exported CSVs.
    - In-memory credential prompt via Get-Credential (zero hardcoded passwords).
    - Strict verification of target database (Sepidar01) before execution.
    - Zero overwrite of past run folders.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SepidarServer = "192.168.1.242,62753",

    [Parameter(Mandatory = $false)]
    [string]$TargetDatabase = "Sepidar01",

    [Parameter(Mandatory = $false)]
    [int]$FiscalYearId = 10,

    [Parameter(Mandatory = $false)]
    [int]$StockId = 1,

    [Parameter(Mandatory = $false)]
    [string]$OutputDir,

    [Parameter(Mandatory = $false)]
    [System.Management.Automation.PSCredential]$SepidarCredential
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

$runId = "sepidar_sample_phase2_part3_" + (Get-Date -Format 'yyyyMMdd_HHmmss')
if (-not $OutputDir) {
    $OutputDir = Join-Path $rootDir "docs\runs\$runId"
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " ParkPalace CRM - نمونه‌برداری تکمیلی فاز ۲ (بخش سوم)" -ForegroundColor Cyan
Write-Host " مسیر: کلاینت توسعه -> نشست امن CRM (192.168.1.248) -> سپیدار ($SepidarServer)" -ForegroundColor Cyan
Write-Host " دیتابیس هدف: $TargetDatabase | سال مالی صریح: $FiscalYearId | انبار صریح: $StockId" -ForegroundColor Cyan
Write-Host " پوشه خروجی: $OutputDir" -ForegroundColor Cyan
Write-Host " محدودیت قفل: LOCK_TIMEOUT = 3000ms | مهلت فرمان: CommandTimeout = 30s" -ForegroundColor Cyan
Write-Host " دامنه: موجودی اقلام KG + شماره اسناد نمونه برای تطبیق با رابط سپیدار" -ForegroundColor Green
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
    # گام ۳: اعتبارسنجی مقصد و اجرای کوئری‌های تکمیلی از داخل سرور CRM
    # ----------------------------------------------------------------------------------
    Write-Host "`n[گام ۳] اتصال از سرور CRM به سپیدار ($SepidarServer) و اجرای کوئری‌های تکمیلی..." -ForegroundColor Yellow

    $scriptBlock = {
        param(
            [string]$Server,
            [string]$Database,
            [int]$FYId,
            [int]$WHId,
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

        # تعریف کوئری‌های تکمیلی
        $queries = @(
            @{
                Id = "01_kg_stock_calculation_sample"
                Title = "نمونه‌برداری محاسباتی موجودی اقلام کیلوگرمی (UnitRef=1، حداکثر ۲۰ قلم دارای گردش)"
                Sql = @"
SELECT TOP 20
    ItemRef,
    StockRef,
    UnitRef,
    InputQuantity,
    OutputQuantity,
    Quantity AS StoredQuantity,
    (InputQuantity - OutputQuantity) AS NetDifference,
    SaleQuantity,
    SaleWithReserveQuantity,
    FeedFromClosingOperation
FROM INV.ItemStockSummary
WHERE FiscalYearRef = $FYId
  AND StockRef = $WHId
  AND UnitRef = 1
  AND (InputQuantity > 0 OR OutputQuantity > 0)
ORDER BY ItemRef;
"@
            },
            @{
                Id = "02_receipt_document_number_samples"
                Title = "شماره سند نمونه برای هر ترکیب Type/IsReturn در رسیدهای انبار جهت تطبیق با رابط سپیدار"
                Sql = @"
WITH RankedReceipts AS (
    SELECT 
        InventoryReceiptID,
        Number AS DocumentNumber,
        Date AS DocumentDate,
        Type AS ReceiptType,
        IsReturn,
        AccountingVoucherRef,
        ROW_NUMBER() OVER(PARTITION BY Type, IsReturn ORDER BY InventoryReceiptID DESC) AS RowNum
    FROM INV.InventoryReceipt
    WHERE FiscalYearRef = $FYId
      AND StockRef = $WHId
)
SELECT 
    InventoryReceiptID,
    DocumentNumber,
    DocumentDate,
    ReceiptType,
    IsReturn,
    AccountingVoucherRef
FROM RankedReceipts
WHERE RowNum <= 3
ORDER BY ReceiptType, IsReturn, RowNum;
"@
            },
            @{
                Id = "03_delivery_document_number_samples"
                Title = "شماره سند نمونه برای هر ترکیب Type/IsReturn در حواله‌های انبار جهت تطبیق با رابط سپیدار"
                Sql = @"
WITH RankedDeliveries AS (
    SELECT 
        InventoryDeliveryID,
        Number AS DocumentNumber,
        Date AS DocumentDate,
        Type AS DeliveryType,
        IsReturn,
        AccountingVoucherRef,
        ROW_NUMBER() OVER(PARTITION BY Type, IsReturn ORDER BY InventoryDeliveryID DESC) AS RowNum
    FROM INV.InventoryDelivery
    WHERE FiscalYearRef = $FYId
      AND StockRef = $WHId
)
SELECT 
    InventoryDeliveryID,
    DocumentNumber,
    DocumentDate,
    DeliveryType,
    IsReturn,
    AccountingVoucherRef
FROM RankedDeliveries
WHERE RowNum <= 3
ORDER BY DeliveryType, IsReturn, RowNum;
"@
            }
        )

        $results = [ordered]@{}
        $results["_metadata"] = [PSCustomObject]@{
            ServerName = $actualServer
            DatabaseName = $actualDb
            FiscalYearId = $FYId
            StockId = $WHId
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
                for ($i = 0; $i -lt $r.FieldCount; $i++) {
                    $col = $colNames[$i]
                    if ($r.IsDBNull($i)) {
                        $rowObj[$col] = ""
                    } else {
                        $val = $r.GetValue($i)
                        if ($val -is [bool]) {
                            $rowObj[$col] = if ($val) { "True" } else { "False" }
                        } else {
                            $rowObj[$col] = $val.ToString()
                        }
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
        -ArgumentList $SepidarServer, $TargetDatabase, $FiscalYearId, $StockId, $SepidarCredential, $connStr

    Write-Host "  [OK] استعلام‌های تکمیلی با موفقیت در سرور CRM اجرا شدند." -ForegroundColor Green

    # ----------------------------------------------------------------------------------
    # گام ۴: تبدیل نتایج به فایل‌های CSV مجزا با انکودینگ UTF-8 with BOM
    # ----------------------------------------------------------------------------------
    Write-Host "`n[گام ۴] ذخیره‌سازی فایل‌های CSV در $OutputDir با انکودینگ UTF-8 BOM..." -ForegroundColor Yellow

    $meta = $remoteData["_metadata"]
    Write-Host "  سرور واقعی سپیدار: $($meta.ServerName)" -ForegroundColor Cyan
    Write-Host "  پایگاه‌داده واقعی: $($meta.DatabaseName)" -ForegroundColor Cyan
    Write-Host "  سال مالی صریح: $($meta.FiscalYearId) | انبار صریح: $($meta.StockId)" -ForegroundColor Cyan
    Write-Host "  زمان اجرا (UTC): $($meta.ExecutionTimeUtc)" -ForegroundColor Cyan

    $csvEncoding = if ($PSVersionTable.PSEdition -eq "Core") { "utf8BOM" } else { "UTF8" }
    $manifest = @()

    foreach ($key in $remoteData.Keys) {
        if ($key.StartsWith("_")) { continue }
        $rows = $remoteData[$key]
        $csvFileName = "$key.csv"
        $csvPath = Join-Path $OutputDir $csvFileName
        
        if ($rows -and $rows.Count -gt 0) {
            $rows | Export-Csv -Path $csvPath -NoTypeInformation -Encoding $csvEncoding
        } else {
            Set-Content -Path $csvPath -Value "" -Encoding $csvEncoding
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
# خلاصه نتایج نمونه‌برداری تکمیلی فاز ۲ (بخش سوم) — شناسه: $runId

* **زمان اجرا (UTC):** $($meta.ExecutionTimeUtc)
* **مسیر ارتباطی:** کلاینت توسعه -> نشست امن WinRM HTTPS -> سرور CRM (192.168.1.248) -> SQL Server سپیدار ($SepidarServer)
* **سرور واقعی SQL Server:** $($meta.ServerName)
* **پایگاه‌داده هدف:** $($meta.DatabaseName)
* **پارامترهای صریح:** سال مالی = $($meta.FiscalYearId) | انبار = $($meta.StockId)
* **تنظیمات قفل و مهلت:** CommandTimeout = 30 ثانیه | LOCK_TIMEOUT = 3000 میلی‌ثانیه
* **ایزولاسیون تراکنش:** READ COMMITTED
* **فرمت خروجی:** CSV با انکودینگ UTF-8 with BOM و تفکیک قطعی مقادیر بولی، عددی و NULL

## فایل‌های CSV استخراج‌شده:
$manifestLines

## توضیحات امنیتی:
* صفر فیلد مبلغ، قیمت یا شرح استخراج گردید.
* داده‌ها صرفاً شامل شناسه‌ها، شماره سند و کدهای نوع سند هستند.
* هیچ داده عملیاتی تغییر نیافت و هیچ پیش‌نویس آزمایشی ایجاد نشد.
"@

    [System.IO.File]::WriteAllText($summaryPath, $reportContent, [System.Text.Encoding]::UTF8)
    Write-Host "`n  [OK] گزارش خلاصه در $summaryPath ذخیره شد." -ForegroundColor Green

    # ایجاد فایل پرچم اتمام
    $doneFlagPath = Join-Path $OutputDir "SAMPLE_PHASE2_PART3_COMPLETED.txt"
    [System.IO.File]::WriteAllText($doneFlagPath, "SUCCESS`n$runId`n$($meta.ExecutionTimeUtc)", [System.Text.Encoding]::UTF8)

    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host " استخراج بسته تکمیلی با موفقیت پایان یافت." -ForegroundColor Green
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
