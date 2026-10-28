-- =========================================================================================
-- نام اسکریپت: 04_inspect_snapshot_prerequisites_readonly.sql
-- هدف: استعلام فقط‌خواندنی پیش‌نیازهای ایزولاسیون Snapshot، سوابق بکاپ، وضعیت دیسک و tempdb
-- پایگاه‌داده هدف: Sepidar01 و tempdb روی سرور SEPIDAR\SEPIDAR
--
-- قوانین و تعهدات اجرایی:
-- ۱. این اسکریپت کاملاً فقط‌خواندنی (Read-Only) است؛ هیچ دستور DDL، DML یا ALTER DATABASE ندارد.
-- ۲. هیچ دستور BACKUP، RESTORE یا RESTORE VERIFYONLY در این اسکریپت وجود ندارد.
-- ۳. خروجی بخش بکاپ صرفاً «سابقه ثبت‌شده در msdb» است و سلامت یا قابل‌بازیابی بودن را اثبات نمی‌کند.
-- ۴. مشاهده تراکنش‌ها کاملاً مقطعی و لحظه‌ای است و نبود تراکنش در ثانیه‌های بعد را تضمین نمی‌کند.
-- ۵. مجوزهای لازم برای هر بخش صریحاً مشخص شده است؛ در صورت نبود مجوز، سطح دسترسی نباید افزایش یابد.
-- =========================================================================================

--------------------------------------------------------------------------------------------
-- بخش اول: استعلام وضعیت فعلی تنظیمات ایزولاسیون در پایگاه‌داده Sepidar01
-- مجوز لازم: VIEW ANY DATABASE یا دسترسی عمومی به sys.databases
--------------------------------------------------------------------------------------------
SET LOCK_TIMEOUT 3000;

SELECT 
    d.name AS [DatabaseName],
    d.snapshot_isolation_state AS [SnapshotIsolationState_Code],
    d.snapshot_isolation_state_desc AS [SnapshotIsolationState_Desc],
    d.is_read_committed_snapshot_on AS [IsRcsiOn],
    d.user_access_desc AS [UserAccessMode],
    d.state_desc AS [DatabaseOnlineState]
FROM sys.databases d
WHERE d.name = 'Sepidar01';
GO

--------------------------------------------------------------------------------------------
-- بخش دوم: استعلام «سابقه بکاپ کامل» ثبت‌شده در msdb (Backup History Only)
-- مجوز لازم: عضویت در نقش db_datareader در msdb یا دسترسی SELECT به جداول backupset و backupmediafamily
-- هشدار حقوقی و فنی:
-- - این خروجی صرفاً «سابقه ثبت‌شده بکاپ در دیتابیس msdb» است؛
-- - این گزارش به هیچ عنوان به معنای «بکاپ سالم» یا «بکاپ قابل‌بازیابی» نیست.
-- - احراز بکاپ معتبر نیازمند ۳ شاهد مستقل است:
--     ۱) سابقه ثبت در msdb (این کوئری)
--     ۲) تأیید وجود فیزیکی فایل روی دیسک / مسیر ذخیره‌سازی
--     ۳) شاهد بازیابی آزمایشی موفق روی سرور تست (توجه: RESTORE VERIFYONLY معادل بازیابی آزمایشی نیست)
-- - برای دیتابیس CRM نیز پیش از هرگونه عملیات اعمال (Apply)، شاهد بکاپ قابل‌بازیابی الزامی است.
--------------------------------------------------------------------------------------------
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
WHERE bs.database_name = 'Sepidar01'
  AND bs.type = 'D' -- صرفاً سوابق بکاپ‌های کامل
ORDER BY bs.backup_finish_date DESC;
GO

--------------------------------------------------------------------------------------------
-- بخش سوم: استعلام فضای داخل فایل‌های tempdb و فضای واقعی درایو دیسک
-- مجوز لازم بخش ۳.الف: CONNECT به tempdb و VIEW DATABASE STATE
-- مجوز لازم بخش ۳.ب: VIEW SERVER STATE جهت فراخوانی sys.dm_os_volume_stats
-- قانون: در صورت نبود هریک از مجوزها، سطح دسترسی کاربر نباید افزایش یابد و پیام خطای مجوز ثبت گردد.
--------------------------------------------------------------------------------------------

-- ۳.الف. فضای مصرف‌شده و آزاد داخل فایل‌های دیتابیس tempdb (تفکیک فایل‌های داده ROWS از لاگ LOG)
-- اجرا الزاماً در زمینه (Context) دیتابیس tempdb انجام می‌شود تا FILEPROPERTY مقدار صحیح محاسبه کند.
-- مقادیر NULL برای فضای مصرفی (به‌ویژه در فایل‌های لاگ) حفظ شده و صفر تلقی نمی‌شوند.
USE tempdb;
GO

SET LOCK_TIMEOUT 3000;

SELECT 
    df.file_id AS [FileId],
    df.name AS [LogicalName],
    df.type_desc AS [FileType], -- ROWS (داده) یا LOG (لاگ)
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
GO

-- ۳.ب. فضای آزاد واقعی درایو دیسک فیزیکی میزبان فایل‌های tempdb (جدا از فضای داخل فایل)
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
GO

--------------------------------------------------------------------------------------------
-- بخش چهارم: مشاهده مقطعی و لحظه‌ای تراکنش‌های باز روی دیتابیس Sepidar01
-- مجوز لازم: VIEW SERVER STATE
-- هشدار فنی:
-- این استعلام صرفاً یک «مشاهده مقطعی و لحظه‌ای در لحظه اجرای کوئری» است.
-- نبود تراکنش در زمان مشاهده، به هیچ وجه تضمین‌کننده عدم ثبت تراکنش در ثانیه‌های بعد یا آمادگی قطعی نیست.
-- زمان شروع تراکنش بدون اثبات UTC بودن، UTC نامیده نشده و مدت زمان با ساعت سرور (SYSDATETIME) محاسبه می‌شود.
--------------------------------------------------------------------------------------------
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
WHERE dt.database_id = DB_ID('Sepidar01');
GO
