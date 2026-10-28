-- =========================================================================================
-- نام اسکریپت: 05_inspect_snapshot_prerequisites_admin_supplementary.sql
-- هدف: تکمیل فقط‌خواندنی بررسی‌های ناقص پیش‌نیازها با حساب مدیریتی SQL ازپیش‌مجاز
-- سرور هدف: SEPIDAR\SEPIDAR (192.168.1.242,62753) | دیتابیس: Sepidar01 و tempdb
--
-- تعهدات و محدودیت‌های قطعی:
-- ۱. فقط‌خواندنی (Read-Only)؛ فاقد هرگونه دستور DDL، DML یا ALTER DATABASE.
-- ۲. فاقد BACKUP، RESTORE، RESTORE VERIFYONLY، قطع نشست (Kill Session) یا Apply.
-- ۳. دسترسی crm_reader به هیچ وجه تغییر یا افزایش نیافته است.
-- ۴. استخراج تاریخ‌ها در قالب استاندارد ISO 8601 (فرمت 126) و اعداد مستقل از تنظیمات محلی.
-- ۵. اعمال LOCK_TIMEOUT = 3000 میلی‌ثانیه و CommandTimeout = 30 ثانیه.
-- =========================================================================================

--------------------------------------------------------------------------------------------
-- بخش اول: ساعت سرور و سابقه بکاپ کامل با تاریخ‌های میلادی ISO و اعداد مستقل از تنظیمات زبان
--------------------------------------------------------------------------------------------

-- ۱.الف: استعلام ساعت سرور سپیدار و تعیین مبنای زمانی نسبت به UTC
SET LOCK_TIMEOUT 3000;

SELECT 
    CONVERT(VARCHAR(30), SYSDATETIME(), 126) AS [ServerLocalDateTime_Iso],
    CONVERT(VARCHAR(30), SYSUTCDATETIME(), 126) AS [ServerUtcDateTime_Iso],
    DATEDIFF(MINUTE, SYSUTCDATETIME(), SYSDATETIME()) AS [ServerUtcOffset_Minutes],
    CAST(SERVERPROPERTY('ServerName') AS NVARCHAR(128)) AS [ServerInstanceName],
    CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR(128)) AS [ProductVersion],
    CAST(SERVERPROPERTY('Collation') AS NVARCHAR(128)) AS [ServerCollation],
    DB_NAME() AS [CurrentDatabaseContext];
GO

-- ۱.ب: سوابق ثبت‌شده بکاپ کامل در msdb با فرمت قطعی ISO 8601 و اعداد اعشاری با ممیز نقطه
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
WHERE bs.database_name = 'Sepidar01'
  AND bs.type = 'D'
ORDER BY bs.backup_finish_date DESC;
GO

--------------------------------------------------------------------------------------------
-- بخش دوم: فضای آزاد واقعی درایو دیسک فیزیکی میزبان فایل‌های tempdb (نیازمند VIEW SERVER STATE)
--------------------------------------------------------------------------------------------
USE tempdb;
GO

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
GO

--------------------------------------------------------------------------------------------
-- بخش سوم: مشاهده مقطعی و لحظه‌ای تراکنش‌های فعال Sepidar01 (نیازمند VIEW SERVER STATE)
--------------------------------------------------------------------------------------------
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
WHERE dt.database_id = DB_ID('Sepidar01');
GO
