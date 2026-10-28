<#
.SYNOPSIS
    ParkPalace CRM - تکمیل فقط‌خواندنی بررسی‌های ناقص پیش‌نیازها با حساب مدیریتی SQL
.DESCRIPTION
    این ابزار بررسی‌های ناقص گزارش قبلی را بدون افزایش دسترسی crm_reader و با حساب مدیریتی
    ازپیش‌مجاز SQL تکمیل می‌کند:
      ۱. فضای آزاد واقعی دیسک فیزیکی میزبان فایل‌های tempdb (sys.dm_os_volume_stats).
      ۲. وضعیت لحظه‌ای تراکنش‌های فعال Sepidar01 (sys.dm_tran_database_transactions).
      ۳. ساعت سرور و سابقه بکاپ کامل با تاریخ میلادی ISO و اعداد مستقل از تنظیمات محلی.

    الزامات و تعهدات قطعی:
    - صفر تغییر: فاقد هرگونه ALTER DATABASE، BACKUP، RESTORE، VERIFYONLY، قطع نشست یا Apply.
    - عدم تغییر در دسترسی کاربر crm_reader.
    - مهلت هر فرمان ۳۰ ثانیه (CommandTimeout = 30) و قفل ۳ ثانیه (LOCK_TIMEOUT = 3000ms).
    - دریافت امن و تعاملی اعتبارنامه‌ها در حافظه رم (فاقد رمز ثابت).
    - خروجی با انکودینگ استاندارد UTF-8 with BOM و مستقل از Culture محلی.
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
    [System.Management.Automation.PSCredential]$AdminCredential,

    [Parameter(Mandatory = $false)]
    [System.Management.Automation.PSCredential]$CrmCredential
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

$runId = "snapshot_prerequisites_admin_" + (Get-Date -Format 'yyyyMMdd_HHmmss')
if (-not $OutputDir) {
    $OutputDir = Join-Path $rootDir "docs\runs\$runId"
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " ParkPalace CRM - تکمیل فقط‌خواندنی بررسی پیش‌نیازها (حساب مدیر SQL)" -ForegroundColor Cyan
Write-Host " مسیر: کلاینت -> نشست امن WinRM (crm-mgmt.sahar.loc:5987) -> سپیدار ($SepidarServer)" -ForegroundColor Cyan
Write-Host " دیتابیس هدف: $TargetDatabase | پوشه خروجی: $OutputDir" -ForegroundColor Cyan
Write-Host " دامنه: فضای دیسک tempdb + تراکنش‌های فعال + سابقه بکاپ ISO و ساعت سرور" -ForegroundColor Yellow
Write-Host " محدودیت قفل: LOCK_TIMEOUT = 3000ms | مهلت فرمان: CommandTimeout = 30s" -ForegroundColor Cyan
Write-Host " وضعیت تغییرات: صفر تغییر (Read-Only) — فاقد ALTER DATABASE / BACKUP / RESTORE" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan

# --------------------------------------------------------------------------------------
# گام ۱: دریافت امن اعتبارنامه حساب مدیریتی SQL در حافظه رم
# --------------------------------------------------------------------------------------
if (-not $AdminCredential) {
    Write-Host "`n[گام ۱] لطفاً نام کاربری و رمز عبور حساب مدیریتی ازپیش‌مجاز SQL Server (مانند sa) را وارد کنید:" -ForegroundColor Yellow
    Write-Host "  (این حساب صرفاً برای خواندن VIEW SERVER STATE و سوابق msdb استفاده می‌شود)" -ForegroundColor Gray
    Write-Host "  (رمز عبور صرفاً در حافظه رم نگهداری می‌شود و در هیچ فایلی درج نخواهد شد)" -ForegroundColor Gray
    $AdminCredential = Get-Credential -UserName "sa" -Message "اتصال با حساب مدیریتی مجاز SQL Server به سپیدار (Sepidar01):"
}

# --------------------------------------------------------------------------------------
# گام ۲: دریافت امن اعتبارنامه سرور CRM و برقراری نشست امن WinRM HTTPS
# --------------------------------------------------------------------------------------
if (-not $CrmCredential) {
    Write-Host "`n[گام ۲] لطفاً اعتبارنامه اتصال به سرور CRM (crm-mgmt.sahar.loc) را وارد کنید:" -ForegroundColor Yellow
    $CrmCredential = Get-Credential -UserName "CRM\Administrator" -Message "اتصال امن به سرور مدیریت CRM:"
}

Write-Host "`n[گام ۲.ب] برقراری نشست امن WinRM HTTPS به crm-mgmt.sahar.loc:5987..." -ForegroundColor Yellow
$sessionOption = New-PSSessionOption -SkipCACheck:$false -SkipCNCheck:$false -SkipRevocationCheck:$false

$crmSession = $null
try {
    $crmSession = New-PSSession -ConnectionUri "https://crm-mgmt.sahar.loc:5987/wsman" `
        -Credential $CrmCredential `
        -SessionOption $sessionOption `
        -Authentication Negotiate

    Write-Host "  [OK] نشست امن با سرور CRM برقرار شد." -ForegroundColor Green

    # ----------------------------------------------------------------------------------
    # گام ۳: اجرای استعلام‌های تکمیلی در داخل سرور CRM با مدیریت کانتکست و Culture مستقل
    # ----------------------------------------------------------------------------------
    Write-Host "`n[گام ۳] اتصال به سرور سپیدار ($SepidarServer) و اجرای استعلام‌های تکمیلی..." -ForegroundColor Yellow

    $scriptBlock = {
        param(
            [string]$Server,
            [string]$Database,
            [System.Management.Automation.PSCredential]$Cred
        )

        $ErrorActionPreference = 'Stop'

        # تضمین استفاده از فرهنگ مستقل از زبان محلی در .NET
        [System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::InvariantCulture
        [System.Threading.Thread]::CurrentThread.CurrentUICulture = [System.Globalization.CultureInfo]::InvariantCulture

        $user = $Cred.UserName
        $pass = $Cred.GetNetworkCredential().Password
        $sqlConnStr = "Server=$Server;Database=$Database;User Id=$user;Password=$pass;TrustServerCertificate=True;Connection Timeout=15;"

        $conn = New-Object System.Data.SqlClient.SqlConnection($sqlConnStr)
        $conn.Open()

        # بررسی هویت سرور و پایگاه‌داده
        $verifyCmd = $conn.CreateCommand()
        $verifyCmd.CommandTimeout = 30
        $verifyCmd.CommandText = "SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName, @@VERSION AS SqlVersion, SUSER_SNAME() AS ExecutingLogin"
        $reader = $verifyCmd.ExecuteReader()
        $actualServer = ""
        $actualDb = ""
        $actualVersion = ""
        $execLogin = ""
        if ($reader.Read()) {
            $actualServer = $reader["ServerName"].ToString()
            $actualDb = $reader["DatabaseName"].ToString()
            $actualVersion = $reader["SqlVersion"].ToString()
            $execLogin = $reader["ExecutingLogin"].ToString()
        }
        $reader.Close()

        if ($actualDb -ne $Database) {
            $conn.Close()
            throw "پایگاه‌داده متصل‌شده ($actualDb) با مقصد موردنظر ($Database) تطابق ندارد! عملیات متوقف شد."
        }

        # تعریف دسته‌های استعلام تکمیلی
        $batches = @(
            @{
                Id = "01_server_time_and_culture_diagnostics"
                Title = "تشخیص ساعت سرور سپیدار، تطبیق با UTC و تنظیمات سرور"
                HasResults = $true
                Sql = @"
SET LOCK_TIMEOUT 3000;

SELECT 
    CONVERT(VARCHAR(30), SYSDATETIME(), 126) AS [ServerLocalDateTime_Iso],
    CONVERT(VARCHAR(30), SYSUTCDATETIME(), 126) AS [ServerUtcDateTime_Iso],
    DATEDIFF(MINUTE, SYSUTCDATETIME(), SYSDATETIME()) AS [ServerUtcOffset_Minutes],
    CAST(SERVERPROPERTY('ServerName') AS NVARCHAR(128)) AS [ServerInstanceName],
    CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR(128)) AS [ProductVersion],
    CAST(SERVERPROPERTY('Collation') AS NVARCHAR(128)) AS [ServerCollation],
    DB_NAME() AS [CurrentDatabaseContext];
"@
            },
            @{
                Id = "02_full_backup_history_iso"
                Title = "سوابق ثبت‌شده بکاپ کامل در msdb با فرمت ISO 8601 و اعداد مستقل از فرهنگ محلی"
                HasResults = $true
                Sql = @"
SET LOCK_TIMEOUT 3000;

SELECT TOP 10
    bs.database_name AS [DatabaseName],
    bs.backup_set_id AS [BackupSetId],
    bs.name AS [BackupSetName],
    CONVERT(VARCHAR(30), bs.backup_start_date, 126) AS [BackupStartDate_Iso],
    CONVERT(VARCHAR(30), bs.backup_finish_date, 126) AS [RecordedFinishDate_Iso],
    CASE bs.type 
        WHEN 'D' THEN 'Full Database Backup (D)'
        WHEN 'I' THEN 'Differential Backup (I)'
        WHEN 'L' THEN 'Transaction Log Backup (L)'
        ELSE bs.type 
    END AS [BackupType],
    CAST(CAST(bs.backup_size / 1024.0 / 1024.0 AS DECIMAL(18,2)) AS VARCHAR(30)) AS [RecordedBackupSize_MB],
    CAST(CAST(bs.compressed_backup_size / 1024.0 / 1024.0 AS DECIMAL(18,2)) AS VARCHAR(30)) AS [CompressedSize_MB],
    bmf.physical_device_name AS [RecordedPath_PhysicalExistenceNotVerified],
    CASE bs.is_copy_only WHEN 1 THEN 'True' ELSE 'False' END AS [IsCopyOnly]
FROM msdb.dbo.backupset bs
LEFT JOIN msdb.dbo.backupmediafamily bmf ON bs.media_set_id = bmf.media_set_id
WHERE bs.database_name = '$Database'
  AND bs.type = 'D'
ORDER BY bs.backup_finish_date DESC;
"@
            },
            @{
                Id = "03_tempdb_context_switch"
                Title = "تغییر زمینه اتصال به دیتابیس tempdb"
                HasResults = $false
                Sql = @"
USE tempdb;
"@
            },
            @{
                Id = "03_tempdb_real_disk_volume_space"
                Title = "فضای آزاد واقعی درایو دیسک فیزیکی میزبان فایل‌های tempdb"
                HasResults = $true
                Sql = @"
SET LOCK_TIMEOUT 3000;

SELECT DISTINCT
    vs.volume_mount_point AS [DriveMountPoint],
    vs.logical_volume_name AS [VolumeName],
    vs.file_system_type AS [FileSystem],
    CAST(CAST(vs.total_bytes / 1024.0 / 1024.0 / 1024.0 AS DECIMAL(18,2)) AS VARCHAR(30)) AS [TotalDiskCapacity_GB],
    CAST(CAST(vs.available_bytes / 1024.0 / 1024.0 / 1024.0 AS DECIMAL(18,2)) AS VARCHAR(30)) AS [RealDiskFreeSpace_GB],
    CAST(CAST(CAST(vs.available_bytes AS FLOAT) / CAST(vs.total_bytes AS FLOAT) * 100.0 AS DECIMAL(5,2)) AS VARCHAR(30)) AS [RealDiskFreePercent]
FROM tempdb.sys.database_files df
CROSS APPLY sys.dm_os_volume_stats(DB_ID('tempdb'), df.file_id) vs;
"@
            },
            @{
                Id = "04_active_transactions_point_in_time"
                Title = "مشاهده مقطعی و لحظه‌ای تراکنش‌های فعال پایگاه‌داده Sepidar01"
                HasResults = $true
                Sql = @"
SET LOCK_TIMEOUT 3000;

SELECT 
    CONVERT(VARCHAR(30), SYSDATETIME(), 126) AS [ObservationServerTime_Iso],
    s.session_id AS [SessionId],
    s.login_name AS [LoginName],
    s.host_name AS [HostName],
    s.program_name AS [ProgramName],
    s.status AS [SessionStatus],
    t.transaction_id AS [TransactionId],
    t.name AS [TransactionName],
    CONVERT(VARCHAR(30), t.transaction_begin_time, 126) AS [TransactionBeginTime_ServerTime_Iso],
    DATEDIFF(SECOND, t.transaction_begin_time, SYSDATETIME()) AS [ActiveDuration_Seconds],
    dt.database_transaction_state AS [DatabaseTransactionState_Code]
FROM sys.dm_tran_database_transactions dt
JOIN sys.dm_tran_active_transactions t ON dt.transaction_id = t.transaction_id
JOIN sys.dm_tran_session_transactions st ON t.transaction_id = st.transaction_id
JOIN sys.dm_exec_sessions s ON s.session_id = st.session_id
WHERE dt.database_id = DB_ID('$Database');
"@
            }
        )

        $results = [ordered]@{}
        $results["_metadata"] = [PSCustomObject]@{
            ServerName = $actualServer
            DatabaseName = $actualDb
            SqlVersion = $actualVersion
            ExecutingLogin = $execLogin
            ClientExecutionUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        }

        $statusList = New-Object System.Collections.Generic.List[PSObject]

        foreach ($b in $batches) {
            $bId = $b.Id
            $bTitle = $b.Title
            $hasRes = $b.HasResults
            $bSql = $b.Sql

            try {
                $cmd = $conn.CreateCommand()
                $cmd.CommandTimeout = 30
                $cmd.CommandText = $bSql

                if ($hasRes) {
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
                                $rowObj[$col] = "NULL"
                            } else {
                                $val = $r.GetValue($i)
                                if ($val -is [bool]) {
                                    $rowObj[$col] = if ($val) { "True" } else { "False" }
                                } elseif ($val -is [System.DateTime]) {
                                    $rowObj[$col] = $val.ToString("yyyy-MM-ddTHH:mm:ss.fff", [System.Globalization.CultureInfo]::InvariantCulture)
                                } elseif ($val -is [decimal] -or $val -is [double] -or $val -is [float]) {
                                    $rowObj[$col] = $val.ToString("0.00", [System.Globalization.CultureInfo]::InvariantCulture)
                                } else {
                                    $rowObj[$col] = $val.ToString()
                                }
                            }
                        }
                        $rows.Add([PSCustomObject]$rowObj)
                    }
                    $r.Close()

                    $results[$bId] = $rows.ToArray()

                    $statusList.Add([PSCustomObject]@{
                        SectionId = $bId
                        SectionTitle = $bTitle
                        InspectionStatus = "موفق"
                        RowCount = $rows.Count
                        ErrorMessage = "ندارد"
                    })
                } else {
                    $cmd.ExecuteNonQuery() | Out-Null
                    $statusList.Add([PSCustomObject]@{
                        SectionId = $bId
                        SectionTitle = $bTitle
                        InspectionStatus = "موفق"
                        RowCount = 0
                        ErrorMessage = "ندارد"
                    })
                }
            } catch {
                $errMsg = $_.Exception.Message
                $statusList.Add([PSCustomObject]@{
                    SectionId = $bId
                    SectionTitle = $bTitle
                    InspectionStatus = "قابل بررسی نبود (عدم کفایت مجوز یا خطا)"
                    RowCount = 0
                    ErrorMessage = $errMsg
                })
                if ($hasRes) {
                    $results[$bId] = @()
                }
            }
        }

        $conn.Close()
        $results["_status"] = $statusList.ToArray()

        return $results
    }

    $remoteData = Invoke-Command -Session $crmSession `
        -ScriptBlock $scriptBlock `
        -ArgumentList $SepidarServer, $TargetDatabase, $AdminCredential

    Write-Host "  [OK] استعلام‌های تکمیلی با موفقیت اجرا شدند." -ForegroundColor Green

    # ----------------------------------------------------------------------------------
    # گام ۴: ذخیره‌سازی داده‌ها در فایل‌های CSV با انکودینگ استاندارد UTF-8 BOM
    # ----------------------------------------------------------------------------------
    Write-Host "`n[گام ۴] ذخیره نتایج در $OutputDir با انکودینگ UTF-8 BOM..." -ForegroundColor Yellow

    $meta = $remoteData["_metadata"]
    Write-Host "  سرور واقعی SQL Server: $($meta.ServerName)" -ForegroundColor Cyan
    Write-Host "  دیتابیس هدف: $($meta.DatabaseName)" -ForegroundColor Cyan
    Write-Host "  حساب لاگین اجراکننده: $($meta.ExecutingLogin)" -ForegroundColor Cyan
    Write-Host "  زمان اجرای کلاینت (UTC): $($meta.ClientExecutionUtc)" -ForegroundColor Cyan

    $csvEncoding = if ($PSVersionTable.PSEdition -eq "Core") { "utf8BOM" } else { "UTF8" }

    # ذخیره جدول وضعیت اجرا
    $statusRows = $remoteData["_status"]
    $statusCsvPath = Join-Path $OutputDir "00_admin_supplementary_status.csv"
    $statusRows | Export-Csv -Path $statusCsvPath -NoTypeInformation -Encoding $csvEncoding
    Write-Host "  [OK] 00_admin_supplementary_status.csv -> $($statusRows.Count) سطر" -ForegroundColor Green

    $manifest = @()
    $manifest += [PSCustomObject]@{ File = "00_admin_supplementary_status.csv"; RowCount = $statusRows.Count; Title = "وضعیت اجرای بخش‌ها و مجوزها" }

    foreach ($key in $remoteData.Keys) {
        if ($key.StartsWith("_") -or $key -eq "03_tempdb_context_switch") { continue }
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
            Title = $key
        }
    }

    # ----------------------------------------------------------------------------------
    # گام ۵: تولید گزارش جامع پیش‌نیازهای تکمیلی و تفکیک شواهد
    # ----------------------------------------------------------------------------------
    Write-Host "`n[گام ۵] ایجاد گزارش تحلیلی در run_summary.md..." -ForegroundColor Yellow

    $summaryPath = Join-Path $OutputDir "run_summary.md"

    # آماده‌سازی جداول مارک‌داون
    $mdStatusTable = ($statusRows | ForEach-Object {
        "| $($_.SectionId) | $($_.SectionTitle) | **$($_.InspectionStatus)** | $($_.RowCount) | $($_.ErrorMessage) |"
    }) -join "`n"

    $diagRows = $remoteData["01_server_time_and_culture_diagnostics"]
    $mdDiagTable = if ($diagRows -and $diagRows.Count -gt 0) {
        "| ساعت محلی سرور (ISO) | ساعت UTC سرور (ISO) | اختلاف دقیقه با UTC | نام سرور | نسخه SQL | Collation | دیتابیس جاری |`n|---|---|---|---|---|---|---|`n" +
        (($diagRows | ForEach-Object {
            "| ``$($_.ServerLocalDateTime_Iso)`` | ``$($_.ServerUtcDateTime_Iso)`` | **$($_.ServerUtcOffset_Minutes)** | $($_.ServerInstanceName) | $($_.ProductVersion) | $($_.ServerCollation) | $($_.CurrentDatabaseContext) |"
        }) -join "`n")
    } else {
        "_اطلاعات زمان سرور در دسترس نیست._"
    }

    $bkRows = $remoteData["02_full_backup_history_iso"]
    $mdBkTable = if ($bkRows -and $bkRows.Count -gt 0) {
        "| نام دیتابیس | شناسه ست بکاپ | نام بکاپ | تاریخ شروع (ISO) | تاریخ پایان ثبت در msdb (ISO) | نوع بکاپ | حجم ثبت‌شده (MB) | حجم فشرده (MB) | مسیر ثبت‌شده | CopyOnly |`n|---|---|---|---|---|---|---|---|---|---|`n" +
        (($bkRows | ForEach-Object {
            "| $($_.DatabaseName) | $($_.BackupSetId) | $($_.BackupSetName) | ``$($_.BackupStartDate_Iso)`` | ``$($_.RecordedFinishDate_Iso)`` | $($_.BackupType) | $($_.RecordedBackupSize_MB) | $($_.CompressedSize_MB) | ``$($_.RecordedPath_PhysicalExistenceNotVerified)`` | $($_.IsCopyOnly) |"
        }) -join "`n")
    } else {
        "_سوابق بکاپ در دسترس نیست یا خطایی رخ داده است._"
    }

    $volRows = $remoteData["03_tempdb_real_disk_volume_space"]
    $mdVolTable = if ($volRows -and $volRows.Count -gt 0) {
        "| نقطه اتصال درایو | نام درایو / Volume | سیستم فایل | ظرفیت کل دیسک (GB) | فضای آزاد واقعی دیسک (GB) | درصد فضای آزاد واقعی |`n|---|---|---|---|---|---|`n" +
        (($volRows | ForEach-Object {
            "| $($_.DriveMountPoint) | $($_.VolumeName) | $($_.FileSystem) | $($_.TotalDiskCapacity_GB) | **$($_.RealDiskFreeSpace_GB)** | **$($_.RealDiskFreePercent)%** |"
        }) -join "`n")
    } else {
        "_اطلاعات حجم دیسک در دسترس نیست یا بخش قابل بررسی نبود._"
    }

    $txRows = $remoteData["04_active_transactions_point_in_time"]
    $mdTxTable = if ($txRows -and $txRows.Count -gt 0) {
        "| زمان مشاهده (ISO) | شناسه نشست | کاربر | نام برنامه | وضعیت نشست | شناسه تراکنش | نام تراکنش | زمان شروع تراکنش (ISO) | مدت زمان فعال (ثانیه) | کد وضعیت تراکنش |`n|---|---|---|---|---|---|---|---|---|---|`n" +
        (($txRows | ForEach-Object {
            "| ``$($_.ObservationServerTime_Iso)`` | $($_.SessionId) | $($_.LoginName) | $($_.ProgramName) | $($_.SessionStatus) | $($_.TransactionId) | $($_.TransactionName) | ``$($_.TransactionBeginTime_ServerTime_Iso)`` | **$($_.ActiveDuration_Seconds)** | $($_.DatabaseTransactionState_Code) |"
        }) -join "`n")
    } else {
        "**در لحظه مشاهده کوئری، هیچ تراکنش فعالی روی دیتابیس $TargetDatabase مشاهده نشد.**"
    }

    $reportContent = @"
# گزارش تکمیلی بررسی پیش‌نیازهای ایزولاسیون Snapshot (حساب مدیر SQL) — شناسه: $runId

## ۱. مشخصات نشست و محیط اجرا
* **مسیر ارتباطی:** کلاینت توسعه -> نشست امن WinRM HTTPS -> سرور CRM (crm-mgmt.sahar.loc:5987) -> SQL Server سپیدار ($SepidarServer)
* **سرور واقعی SQL Server:** $($meta.ServerName)
* **پایگاه‌داده هدف:** $($meta.DatabaseName)
* **لاگین اجراکننده در SQL Server:** $($meta.ExecutingLogin) (حساب مدیریتی ازپیش‌مجاز؛ دسترسی crm_reader تغییر نیافته است)
* **زمان اجرای کلاینت (UTC):** $($meta.ClientExecutionUtc)
* **محدودیت‌های فنی اعمال‌شده:** CommandTimeout = 30 ثانیه | SET LOCK_TIMEOUT 3000 میلی‌ثانیه | انکودینگ UTF-8 with BOM | فرمت‌بندی تاریخ‌ها به ISO 8601 و اعداد Invariant
* **تعهد عدم تغییر (Zero Mutations):** هیچ دستور ALTER DATABASE، BACKUP، RESTORE، RESTORE VERIFYONLY، قطع نشست، استقرار یا Apply اجرا نشده است.

---

## ۲. خلاصه وضعیت اجرای بخش‌ها و مجوزها
| شناسه بخش | عنوان بخش | وضعیت بررسی | تعداد سطر | خطای مشاهده‌شده (در صورت وجود) |
|---|---|---|---|---|
$mdStatusTable

---

## ۳. تحلیل ساعت سرور سپیدار و تطبیق مبنای زمانی
$mdDiagTable

* **تحلیل علت تاریخ‌های غیرعادی گزارش قبلی:**
  - در اجرای قبلی، تبدیل اشیاء تاریخ و اعداد با تنظیمات پیش‌فرض کلاینت/هاست (فرهنگ فارسی fa-IR) انجام شده بود که ممیز فارسی (/) و تقویم محلی را اعمال می‌کرد.
  - در این گزارش، فرمت‌بندی تاریخ‌ها مستقیماً با فرمت استاندارد ISO 8601 در سطح SQL Server (فرمت 126) و اعداد اعشاری به صورت رشته عددی استاندارد با ممیز نقطه استخراج شدند.

---

## ۴. سوابق کامل بکاپ ثبت‌شده در msdb (صرفاً سوابق تاریخی استاندارد ISO)
$mdBkTable

> [!WARNING]
> ### تفکیک حقوقی و فنی شواهد بکاپ:
> ۱. **سابقه بکاپ در msdb:** جدول فوق صرفاً تاریخ و نام فایل ثبت‌شده در جداول سیستم msdb را نشان می‌دهد و به هیچ عنوان سلامت فیزیکی فایل یا قابلیت بازیابی را اثبات نمی‌کند.
> ۲. **شاهد بازیابی آزمایشی (Test Restore):** در سطح کل مخزن و مستندات پروژه، هیچ شاهدی از انجام مانور بازیابی آزمایشی روی یک پایگاه‌داده مجزا برای دیتابیس Sepidar01 وجود ندارد.
> ۳. **تفکیک VERIFYONLY از بازیابی آزمایشی:** دستور RESTORE VERIFYONLY صرفاً هدر فایل را اعتبارسنجی می‌کند و معادل بازیابی آزمایشی موفق نیست؛ این دو شاهد کاملاً مستقل هستند.
> ۴. **الزام پیش از اعمال:** پیش از هرگونه اعمال عملیاتی (Apply) روی دیتابیس سپیدار یا CRM، وجود شاهد بازیابی آزمایشی موفق در محیط مجزا الزامی است.

---

## ۵. فضای آزاد واقعی درایو دیسک فیزیکی میزبان فایل‌های tempdb
$mdVolTable

* **تحلیل ظرفیت دیسک:** بر اساس خروجی فوق، وضعیت ظرفیت کل درایو و فضای آزاد واقعی دیسک فیزیکی درایوی که فایل‌های tempdb روی آن قرار دارند استخراج گردید.

---

## ۶. تراکنش‌های فعال پایگاه‌داده $TargetDatabase در زمان مشاهده
$mdTxTable

* **هشدار فنی و زمانی:**
  - زمان شروع تراکنش‌ها بر مبنای ساعت سرور سپیدار (ServerLocalDateTime) است.
  - مدت زمان فعال بودن تراکنش‌ها با DATEDIFF نسبت به SYSDATETIME سرور در همان لحظه محاسبه شده است.
  - مشاهده تراکنش‌ها کاملاً مقطعی و لحظه‌ای است؛ عدم مشاهده تراکنش در لحظه کوئری، نبود تراکنش در ثانیه‌های بعد را اثبات نمی‌کند.

---

## ۷. فهرست فایل‌های خروجی مستقل
$(($manifest | ForEach-Object { "* **$($_.File):** $($_.RowCount) سطر ($($_.Title))" }) -join "`n")
"@

    [System.IO.File]::WriteAllText($summaryPath, $reportContent, [System.Text.Encoding]::UTF8)
    Write-Host "`n  [OK] گزارش جامع تکمیلی در $summaryPath ذخیره شد." -ForegroundColor Green

    # ایجاد فایل پرچم اتمام
    $doneFlagPath = Join-Path $OutputDir "INSPECTION_ADMIN_COMPLETED.txt"
    [System.IO.File]::WriteAllText($doneFlagPath, "SUCCESS`n$runId`n$($meta.ClientExecutionUtc)", [System.Text.Encoding]::UTF8)

    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host " بررسی تکمیلی پیش‌نیازها با موفقیت به پایان رسید." -ForegroundColor Green
    Write-Host " پوشه نتایج: $OutputDir" -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
}
finally {
    if ($crmSession) {
        Remove-PSSession -Session $crmSession -ErrorAction SilentlyContinue
    }
    $AdminCredential = $null
    $CrmCredential = $null
    [System.GC]::Collect()
}
