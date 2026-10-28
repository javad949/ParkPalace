<#
.SYNOPSIS
    ParkPalace CRM - بررسی فقط‌خواندنی پیش‌نیازهای ایزولاسیون Snapshot در سپیدار از مسیر امن CRM
.DESCRIPTION
    این اسکریپت با رعایت کلیه الزامات امنیتی، پیش‌نیازهای فنی ایزولاسیون Snapshot را
    از طریق نشست امن WinRM HTTPS سرور CRM روی دیتابیس Sepidar01 و tempdb بررسی می‌کند.

    الزامات و تعهدات قطعی:
    ۱. CommandTimeout = 30 ثانیه برای هر دستور و SET LOCK_TIMEOUT 3000 میلی‌ثانیه برای هر بخش.
    ۲. مدیریت جداکننده‌های GO در یک کانکشن یکتا و حفظ زمینه (Context) دیتابیس tempdb.
    ۳. عدم برچسب‌گذاری UTC برای transaction_begin_time و محاسبه مدت تراکنش بر مبنای ساعت سرور (SYSDATETIME).
    ۴. تفکیک فایل‌های داده (ROWS) و لاگ (LOG) در tempdb و عدم تبدیل مقادیر NULL به صفر.
    ۵. در صورت کمبود هر مجوز، ثبت «قابل بررسی نبود» بدون هیچ‌گونه افزایش یا تغییر دسترسی.
    ۶. سابقه بکاپ در msdb بدون ادعای سلامت یا قابلیت بازیابی (تفکیک VERIFYONLY از بازیابی آزمایشی).
    ۷. صفر دستور ALTER DATABASE، BACKUP، RESTORE، VERIFYONLY، قطع نشست یا Apply.
    ۸. دریافت امن و تعاملی اعتبارنامه در حافظه رم بدون درج رمز در فایل‌ها.
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
    [System.Management.Automation.PSCredential]$SepidarCredential,

    [Parameter(Mandatory = $false)]
    [System.Management.Automation.PSCredential]$CrmCredential
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

$runId = "snapshot_prerequisites_" + (Get-Date -Format 'yyyyMMdd_HHmmss')
if (-not $OutputDir) {
    $OutputDir = Join-Path $rootDir "docs\runs\$runId"
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " ParkPalace CRM - بررسی فقط‌خواندنی پیش‌نیازهای Snapshot" -ForegroundColor Cyan
Write-Host " مسیر: کلاینت -> نشست امن WinRM CRM (crm-mgmt.sahar.loc:5987) -> سپیدار ($SepidarServer)" -ForegroundColor Cyan
Write-Host " دیتابیس هدف: $TargetDatabase | پوشه خروجی: $OutputDir" -ForegroundColor Cyan
Write-Host " محدودیت قفل: LOCK_TIMEOUT = 3000ms | مهلت فرمان: CommandTimeout = 30s" -ForegroundColor Cyan
Write-Host " وضعیت تغییرات: صفر تغییر (Read-Only) — فاقد ALTER DATABASE / BACKUP / RESTORE" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan

# --------------------------------------------------------------------------------------
# گام ۱: دریافت امن اعتبارنامه حساب مجاز سپیدار در حافظه رم
# --------------------------------------------------------------------------------------
if (-not $SepidarCredential) {
    if ($env:PARKPALACE_SEPIDAR_CONN) {
        Write-Host "`n[گام ۱] استفاده از رشته اتصال محیطی امن PARKPALACE_SEPIDAR_CONN..." -ForegroundColor Yellow
        $connStr = $env:PARKPALACE_SEPIDAR_CONN
    } else {
        Write-Host "`n[گام ۱] لطفاً نام کاربری و رمز عبور حساب مجاز سپیدار (مانند crm_reader) را وارد کنید:" -ForegroundColor Yellow
        Write-Host "  (رمز عبور صرفاً در حافظه رم نگهداری می‌شود و در هیچ فایلی درج نخواهد شد)" -ForegroundColor Gray
        $SepidarCredential = Get-Credential -UserName "crm_reader" -Message "اتصال فقط‌خواندنی به پایگاه‌داده سپیدار (Sepidar01):"
    }
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
    # گام ۳: اجرای اسکریپت فقط‌خواندنی در داخل سرور CRM با مدیریت کانکشن و پیش‌نیازها
    # ----------------------------------------------------------------------------------
    Write-Host "`n[گام ۳] اتصال از سرور CRM به سرور سپیدار ($SepidarServer) و اجرای استعلام‌های پیش‌نیاز..." -ForegroundColor Yellow

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

        # بررسی سرور متصل و دیتابیس هدف
        $verifyCmd = $conn.CreateCommand()
        $verifyCmd.CommandTimeout = 30
        $verifyCmd.CommandText = "SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName, @@VERSION AS SqlVersion, SYSDATETIME() AS ServerLocalTime"
        $reader = $verifyCmd.ExecuteReader()
        $actualServer = ""
        $actualDb = ""
        $actualVersion = ""
        $serverTime = ""
        if ($reader.Read()) {
            $actualServer = $reader["ServerName"].ToString()
            $actualDb = $reader["DatabaseName"].ToString()
            $actualVersion = $reader["SqlVersion"].ToString()
            $serverTime = $reader["ServerLocalTime"].ToString()
        }
        $reader.Close()

        if ($actualDb -ne $Database) {
            $conn.Close()
            throw "پایگاه‌داده متصل‌شده ($actualDb) با مقصد موردنظر ($Database) تطابق ندارد! عملیات متوقف شد."
        }

        # ساختار دسته‌ای هماهنگ با فایل scripts/04_inspect_snapshot_prerequisites_readonly.sql
        # با حفظ ارتباط یکتا، تفکیک بخش‌ها، اعمال LOCK_TIMEOUT و CommandTimeout
        $batches = @(
            @{
                Id = "01_snapshot_and_rcsi_settings"
                Title = "تنظیمات ایزولاسیون Snapshot و RCSI پایگاه‌داده Sepidar01"
                HasResults = $true
                Sql = @"
SET LOCK_TIMEOUT 3000;

SELECT 
    d.name AS [DatabaseName],
    d.snapshot_isolation_state AS [SnapshotIsolationState_Code],
    d.snapshot_isolation_state_desc AS [SnapshotIsolationState_Desc],
    d.is_read_committed_snapshot_on AS [IsRcsiOn],
    d.user_access_desc AS [UserAccessMode],
    d.state_desc AS [DatabaseOnlineState]
FROM sys.databases d
WHERE d.name = '$Database';
"@
            },
            @{
                Id = "02_full_backup_history_msdb"
                Title = "سابقه ثبت‌شده بکاپ کامل در msdb (صرفاً سوابق ثبت‌شده)"
                HasResults = $true
                Sql = @"
SET LOCK_TIMEOUT 3000;

SELECT TOP 5
    bs.database_name AS [DatabaseName],
    bs.backup_set_id AS [BackupSetId],
    bs.name AS [BackupSetName],
    bs.backup_start_date AS [BackupStartDate],
    bs.backup_finish_date AS [RecordedFinishDate_HistoryOnly],
    CASE bs.type 
        WHEN 'D' THEN 'Full Database Backup (D)'
        WHEN 'I' THEN 'Differential Backup (I)'
        WHEN 'L' THEN 'Transaction Log Backup (L)'
        ELSE bs.type 
    END AS [BackupType],
    CAST(bs.backup_size / 1024.0 / 1024.0 AS DECIMAL(18,2)) AS [RecordedBackupSize_MB],
    CAST(bs.compressed_backup_size / 1024.0 / 1024.0 AS DECIMAL(18,2)) AS [CompressedSize_MB],
    bmf.physical_device_name AS [RecordedPath_PhysicalExistenceNotVerified],
    bs.is_copy_only AS [IsCopyOnly]
FROM msdb.dbo.backupset bs
LEFT JOIN msdb.dbo.backupmediafamily bmf ON bs.media_set_id = bmf.media_set_id
WHERE bs.database_name = '$Database'
  AND bs.type = 'D'
ORDER BY bs.backup_finish_date DESC;
"@
            },
            @{
                Id = "03_tempdb_context_switch"
                Title = "انتقال زمینه کانکشن به دیتابیس tempdb"
                HasResults = $false
                Sql = @"
USE tempdb;
"@
            },
            @{
                Id = "03_tempdb_files_space"
                Title = "فضای داخل فایل‌های دیتابیس tempdb (تفکیک ROWS و LOG و حفظ مقادیر NULL)"
                HasResults = $true
                Sql = @"
SET LOCK_TIMEOUT 3000;

SELECT 
    df.file_id AS [FileId],
    df.name AS [LogicalName],
    df.type_desc AS [FileType],
    df.physical_name AS [PhysicalPath],
    CAST(df.size * 8.0 / 1024.0 AS DECIMAL(18,2)) AS [AllocatedFileSize_MB],
    CASE 
        WHEN FILEPROPERTY(df.name, 'SpaceUsed') IS NOT NULL 
        THEN CAST(FILEPROPERTY(df.name, 'SpaceUsed') * 8.0 / 1024.0 AS DECIMAL(18,2))
        ELSE NULL 
    END AS [SpaceUsedInsideFile_MB_OrNull],
    CASE 
        WHEN FILEPROPERTY(df.name, 'SpaceUsed') IS NOT NULL 
        THEN CAST((df.size - FILEPROPERTY(df.name, 'SpaceUsed')) * 8.0 / 1024.0 AS DECIMAL(18,2))
        ELSE NULL 
    END AS [FreeSpaceInsideFile_MB_OrNull],
    df.growth AS [GrowthConfig],
    df.is_percent_growth AS [IsPercentGrowth]
FROM tempdb.sys.database_files df
ORDER BY df.type_desc DESC, df.file_id ASC;
"@
            },
            @{
                Id = "04_tempdb_real_disk_volume_space"
                Title = "فضای آزاد واقعی درایو دیسک فیزیکی میزبان فایل‌های tempdb"
                HasResults = $true
                Sql = @"
SET LOCK_TIMEOUT 3000;

SELECT DISTINCT
    vs.volume_mount_point AS [DriveMountPoint],
    vs.logical_volume_name AS [VolumeName],
    vs.file_system_type AS [FileSystem],
    CAST(vs.total_bytes / 1024.0 / 1024.0 / 1024.0 AS DECIMAL(18,2)) AS [TotalDiskCapacity_GB],
    CAST(vs.available_bytes / 1024.0 / 1024.0 / 1024.0 AS DECIMAL(18,2)) AS [RealDiskFreeSpace_GB],
    CAST(CAST(vs.available_bytes AS FLOAT) / CAST(vs.total_bytes AS FLOAT) * 100.0 AS DECIMAL(5,2)) AS [RealDiskFreePercent]
FROM tempdb.sys.database_files df
CROSS APPLY sys.dm_os_volume_stats(DB_ID('tempdb'), df.file_id) vs;
"@
            },
            @{
                Id = "05_active_transactions_point_in_time"
                Title = "مشاهده مقطعی و لحظه‌ای تراکنش‌های فعال پایگاه‌داده Sepidar01"
                HasResults = $true
                Sql = @"
SET LOCK_TIMEOUT 3000;

SELECT 
    SYSDATETIME() AS [ObservationServerTime],
    s.session_id AS [SessionId],
    s.login_name AS [LoginName],
    s.host_name AS [HostName],
    s.program_name AS [ProgramName],
    s.status AS [SessionStatus],
    t.transaction_id AS [TransactionId],
    t.name AS [TransactionName],
    t.transaction_begin_time AS [TransactionBeginTime_ServerTime],
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
            ServerLocalTime = $serverTime
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
                                # الزام عدم تلقی NULL به عنوان صفر
                                $rowObj[$col] = "NULL"
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
                # قانون: در صورت کمبود مجوز یا خطا، ثبت «قابل بررسی نبود» بدون افزایش دسترسی
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
        -ArgumentList $SepidarServer, $TargetDatabase, $SepidarCredential, $connStr

    Write-Host "  [OK] استعلام‌ها با موفقیت در محیط سرور CRM اجرا شدند." -ForegroundColor Green

    # ----------------------------------------------------------------------------------
    # گام ۴: ذخیره‌سازی داده‌های استخراج‌شده در قالب فایل‌های CSV با انکودینگ UTF-8 BOM
    # ----------------------------------------------------------------------------------
    Write-Host "`n[گام ۴] ذخیره نتایج در $OutputDir با انکودینگ UTF-8 BOM..." -ForegroundColor Yellow

    $meta = $remoteData["_metadata"]
    Write-Host "  سرور واقعی SQL Server: $($meta.ServerName)" -ForegroundColor Cyan
    Write-Host "  دیتابیس هدف: $($meta.DatabaseName)" -ForegroundColor Cyan
    Write-Host "  زمان ساعت سرور سپیدار: $($meta.ServerLocalTime)" -ForegroundColor Cyan
    Write-Host "  زمان اجرای کلاینت (UTC): $($meta.ClientExecutionUtc)" -ForegroundColor Cyan

    $csvEncoding = if ($PSVersionTable.PSEdition -eq "Core") { "utf8BOM" } else { "UTF8" }

    # ذخیره جدول وضعیت اجرا
    $statusRows = $remoteData["_status"]
    $statusCsvPath = Join-Path $OutputDir "00_prerequisites_inspection_status.csv"
    $statusRows | Export-Csv -Path $statusCsvPath -NoTypeInformation -Encoding $csvEncoding
    Write-Host "  [OK] 00_prerequisites_inspection_status.csv -> $($statusRows.Count) سطر" -ForegroundColor Green

    $manifest = @()
    $manifest += [PSCustomObject]@{ File = "00_prerequisites_inspection_status.csv"; RowCount = $statusRows.Count; Title = "وضعیت اجرای بخش‌ها و مجوزها" }

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
    # گام ۵: تولید گزارش جامع پیش‌نیازهای Snapshot و تفکیک شواهد
    # ----------------------------------------------------------------------------------
    Write-Host "`n[گام ۵] ایجاد گزارش تحلیلی پیش‌نیازها در run_summary.md..." -ForegroundColor Yellow

    $summaryPath = Join-Path $OutputDir "run_summary.md"

    # آماده‌سازی جداول مارک‌داون برای هر بخش
    $mdStatusTable = ($statusRows | ForEach-Object {
        "| $($_.SectionId) | $($_.SectionTitle) | **$($_.InspectionStatus)** | $($_.RowCount) | $($_.ErrorMessage) |"
    }) -join "`n"

    $snapRows = $remoteData["01_snapshot_and_rcsi_settings"]
    $mdSnapTable = if ($snapRows -and $snapRows.Count -gt 0) {
        "| پایگاه‌داده | کد وضعیت Snapshot | شرح وضعیت Snapshot | وضعیت RCSI | حالت دسترسی کاربر | وضعیت دیتابیس |`n|---|---|---|---|---|---|`n" +
        (($snapRows | ForEach-Object {
            "| $($_.DatabaseName) | $($_.SnapshotIsolationState_Code) | $($_.SnapshotIsolationState_Desc) | **$($_.IsRcsiOn)** | $($_.UserAccessMode) | $($_.DatabaseOnlineState) |"
        }) -join "`n")
    } else {
        "_داده‌ای استخراج نشد یا بخش قابل بررسی نبود._"
    }

    $bkRows = $remoteData["02_full_backup_history_msdb"]
    $mdBkTable = if ($bkRows -and $bkRows.Count -gt 0) {
        "| نام دیتابیس | شناسه ست بکاپ | نام بکاپ | نوع بکاپ | تاریخ پایان در سابقه msdb | مسیر ثبت‌شده در سابقه | حجم ثبت‌شده (MB) | حجم فشرده (MB) | CopyOnly |`n|---|---|---|---|---|---|---|---|---|`n" +
        (($bkRows | ForEach-Object {
            "| $($_.DatabaseName) | $($_.BackupSetId) | $($_.BackupSetName) | $($_.BackupType) | $($_.RecordedFinishDate_HistoryOnly) | ``$($_.RecordedPath_PhysicalExistenceNotVerified)`` | $($_.RecordedBackupSize_MB) | $($_.CompressedSize_MB) | $($_.IsCopyOnly) |"
        }) -join "`n")
    } else {
        "_سابقه بکاپ کاملی یافت نشد یا بخش به علت کمبود مجوز قابل بررسی نبود._"
    }

    $tempRows = $remoteData["03_tempdb_files_space"]
    $mdTempTable = if ($tempRows -and $tempRows.Count -gt 0) {
        "| شناسه فایل | نام منطقی | نوع فایل (ROWS / LOG) | فضای تخصیص‌یافته (MB) | فضای مصرفی داخل فایل (MB) | فضای آزاد داخل فایل (MB) | پیکربندی رشد | درصد رشد؟ | مسیر فیزیکی |`n|---|---|---|---|---|---|---|---|---|`n" +
        (($tempRows | ForEach-Object {
            "| $($_.FileId) | $($_.LogicalName) | **$($_.FileType)** | $($_.AllocatedFileSize_MB) | $($_.SpaceUsedInsideFile_MB_OrNull) | $($_.FreeSpaceInsideFile_MB_OrNull) | $($_.GrowthConfig) | $($_.IsPercentGrowth) | ``$($_.PhysicalPath)`` |"
        }) -join "`n")
    } else {
        "_اطلاعات فایل‌های tempdb در دسترس نیست یا بخش قابل بررسی نبود._"
    }

    $volRows = $remoteData["04_tempdb_real_disk_volume_space"]
    $mdVolTable = if ($volRows -and $volRows.Count -gt 0) {
        "| نقطه اتصال درایو | نام درایو / Volume | سیستم فایل | ظرفیت کل دیسک (GB) | فضای آزاد واقعی دیسک (GB) | درصد فضای آزاد واقعی |`n|---|---|---|---|---|---|`n" +
        (($volRows | ForEach-Object {
            "| $($_.DriveMountPoint) | $($_.VolumeName) | $($_.FileSystem) | $($_.TotalDiskCapacity_GB) | **$($_.RealDiskFreeSpace_GB)** | **$($_.RealDiskFreePercent)%** |"
        }) -join "`n")
    } else {
        "_اطلاعات حجم دیسک در دسترس نیست یا بخش قابل بررسی نبود._"
    }

    $txRows = $remoteData["05_active_transactions_point_in_time"]
    $mdTxTable = if ($txRows -and $txRows.Count -gt 0) {
        "| شناسه نشست | کاربر | نام برنامه | وضعیت نشست | شناسه تراکنش | نام تراکنش | زمان شروع تراکنش (ساعت سرور) | مدت زمان فعال (ثانیه) | کد وضعیت تراکنش |`n|---|---|---|---|---|---|---|---|---|`n" +
        (($txRows | ForEach-Object {
            "| $($_.SessionId) | $($_.LoginName) | $($_.ProgramName) | $($_.SessionStatus) | $($_.TransactionId) | $($_.TransactionName) | $($_.TransactionBeginTime_ServerTime) | **$($_.ActiveDuration_Seconds)** | $($_.DatabaseTransactionState_Code) |"
        }) -join "`n")
    } else {
        "خروجی تراکنش خالی است؛ فقط در صورت موفق بودن بخش مربوطه در جدول وضعیت، به معنای نبود تراکنش مشاهده‌شده است. در صورت خطا، وضعیت نامشخص است."
    }

    $reportContent = @"
# گزارش بررسی فقط‌خواندنی پیش‌نیازهای ایزولاسیون Snapshot — شناسه: $runId

## ۱. مشخصات نشست و محیط اجرا
* **مسیر ارتباطی:** کلاینت توسعه -> نشست امن WinRM HTTPS -> سرور CRM (crm-mgmt.sahar.loc:5987) -> SQL Server سپیدار ($SepidarServer)
* **سرور واقعی SQL Server:** $($meta.ServerName)
* **نسخه SQL Server:** $($meta.SqlVersion)
* **پایگاه‌داده هدف:** $($meta.DatabaseName)
* **زمان سرور سپیدار در لحظه اتصال:** $($meta.ServerLocalTime)
* **زمان اجرای کلاینت (UTC):** $($meta.ClientExecutionUtc)
* **محدودیت‌های فنی اعمال‌شده:** CommandTimeout = 30 ثانیه | SET LOCK_TIMEOUT 3000 میلی‌ثانیه | انکودینگ UTF-8 with BOM
* **تعهد عدم تغییر (Zero Mutations):** هیچ دستور ALTER DATABASE، BACKUP، RESTORE، RESTORE VERIFYONLY، قطع نشست، استقرار یا Apply اجرا نشده است.

---

## ۲. خلاصه وضعیت اجرای بخش‌ها و مجوزها
| شناسه بخش | عنوان بخش | وضعیت بررسی | تعداد سطر | خطای مشاهده‌شده (در صورت وجود) |
|---|---|---|---|---|
$mdStatusTable

> **قانون امنیتی:** در صورت کمبود مجوز برای هریک از بخش‌ها، سطح دسترسی حساب کاربر تغییر نیافته و بخش موردنظر صرفاً به‌عنوان «قابل بررسی نبود» ثبت شده است.

---

## ۳. تنظیمات فعلی Snapshot و RCSI در پایگاه‌داده $TargetDatabase
$mdSnapTable

---

## ۴. سابقه بکاپ کامل ثبت‌شده در msdb (صرفاً سوابق تاریخی)
$mdBkTable

> [!WARNING]
> ### تفکیک حقوقی و فنی شواهد بکاپ:
> ۱. **سابقه بکاپ در msdb:** جدول فوق صرفاً تاریخ و نام فایل ثبت‌شده در جداول سیستم msdb را نشان می‌دهد و به هیچ عنوان سلامت فیزیکی فایل یا قابلیت بازیابی را اثبات نمی‌کند.
> ۲. **وجود فیزیکی فایل:** تا زمان بررسی مستقیم سیستم فایل یا محل ذخیره، وجود فیزیکی فایل اثبات‌نشده تلقی می‌شود.
> ۳. **تفکیک VERIFYONLY از بازیابی آزمایشی:** دستور RESTORE VERIFYONLY صرفاً ساختار هدر فایل را بررسی می‌کند و معادل «بازیابی آزمایشی موفق» (Test Restore) نیست.
> ۴. **الزام پیش از اعمال:** پیش از هرگونه اعمال عملیاتی (Apply) روی دیتابیس سپیدار یا CRM، وجود شاهد بازیابی آزمایشی موفق الزامی است.

---

## ۵. وضعیت فضای دیتابیس tempdb و درایو فیزیکی میزبان
### ۵.الف. فضای داخل فایل‌های tempdb (تفکیک فایل‌های داده و لاگ)
$mdTempTable

* **نکته فنی:** مقادیر NULL برای فضای مصرفی فایل‌های لاگ (LOG) به دلیل رفتار استاندارد FILEPROPERTY در SQL Server است و نباید صفر تلقی شود.

### ۵.ب. فضای آزاد واقعی درایو دیسک فیزیکی میزبان tempdb
$mdVolTable

---

## ۶. تراکنش‌های فعال پایگاه‌داده $TargetDatabase در زمان مشاهده
$mdTxTable

* **هشدار فنی و زمانی:**
  - زمان شروع تراکنش‌ها بر مبنای ساعت سرور سپیدار (ServerLocalTime) است و بدون اثبات UTC بودن، برچسب UTC نخورده است.
  - مدت زمان فعال بودن تراکنش‌ها با DATEDIFF نسبت به SYSDATETIME سرور در همان لحظه محاسبه شده است.
  - مشاهده تراکنش‌ها کاملاً مقطعی و لحظه‌ای است؛ عدم مشاهده تراکنش در لحظه کوئری، نبود تراکنش در ثانیه‌های بعد را اثبات نمی‌کند.

---

## ۷. فهرست فایل‌های خروجی مستقل
$(($manifest | ForEach-Object { "* **$($_.File):** $($_.RowCount) سطر ($($_.Title))" }) -join "`n")
"@

    [System.IO.File]::WriteAllText($summaryPath, $reportContent, [System.Text.Encoding]::UTF8)
    Write-Host "`n  [OK] گزارش جامع در $summaryPath ذخیره شد." -ForegroundColor Green

    # ایجاد فایل پرچم اتمام
    $doneFlagPath = Join-Path $OutputDir "INSPECTION_COMPLETED.txt"
    [System.IO.File]::WriteAllText($doneFlagPath, "SUCCESS`n$runId`n$($meta.ClientExecutionUtc)", [System.Text.Encoding]::UTF8)

    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host " بررسی فقط‌خواندنی پیش‌نیازهای Snapshot با موفقیت به پایان رسید." -ForegroundColor Green
    Write-Host " پوشه نتایج: $OutputDir" -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
}
finally {
    if ($crmSession) {
        Remove-PSSession -Session $crmSession -ErrorAction SilentlyContinue
    }
    $SepidarCredential = $null
    $CrmCredential = $null
    [System.GC]::Collect()
}
