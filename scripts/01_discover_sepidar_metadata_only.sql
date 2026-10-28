-- ======================================================================================
-- ParkPalace CRM - Sepidar Discovery: Phase 1 (Metadata & Catalog Only)
-- File: scripts/01_discover_sepidar_metadata_only.sql
-- Role: Strictly Read-Only Catalog Queries (Metadata Only)
-- Target: Sepidar Database (MSSQL)
-- 
-- GUARANTEES & CONSTRAINTS:
-- 1. ZERO SELECT queries against operational/transactional data tables.
-- 2. ZERO assumptions about the existence of FMK.FiscalYear or specific document columns.
-- 3. Inspects metadata for BOTH Tables ('U') and Views ('V').
-- 4. READ COMMITTED isolation for reliable, non-dirty catalog reading.
-- 5. TIMEOUT SPECIFICATION:
--    - SET LOCK_TIMEOUT 3000: Limits lock wait time to 3 seconds (fail-fast on resource contention).
--    - NOTE: LOCK_TIMEOUT only limits lock acquisition wait time; for total command execution duration,
--      the executing client (SSMS, sqlcmd, PowerShell, ADO.NET) MUST specify an explicit CommandTimeout.
-- 6. STRICTLY SAFE FOR DBA REVIEW — DO NOT EXECUTE WITHOUT PRIOR DBA AUTHORIZATION.
-- ======================================================================================

SET NOCOUNT ON;
-- Limit lock acquisition wait time (fail-fast on locked objects): 3000 ms
SET LOCK_TIMEOUT 3000;
-- Standard consistent catalog reads (no dirty uncommitted reads on system metadata)
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

-- --------------------------------------------------------------------------------------
-- ۱. بررسی پیکربندی پایگاه‌داده و وضعیت ایزولاسیون Snapshot
-- --------------------------------------------------------------------------------------
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

-- --------------------------------------------------------------------------------------
-- ۲. فهرست کلیه شِماها و تعداد اشیاء (جدول و ویو) در هر شِما
-- --------------------------------------------------------------------------------------
SELECT 
    s.name AS SchemaName,
    COUNT(CASE WHEN o.type = 'U' THEN 1 END) AS TableCount,
    COUNT(CASE WHEN o.type = 'V' THEN 1 END) AS ViewCount
FROM sys.schemas s
LEFT JOIN sys.objects o ON s.schema_id = o.schema_id AND o.type IN ('U', 'V')
GROUP BY s.name
HAVING COUNT(o.object_id) > 0
ORDER BY TableCount DESC, ViewCount DESC, s.name;

-- --------------------------------------------------------------------------------------
-- ۳. کشف تمامی جداول ('U') و ویوها ('V') در شِماهای حوزه انبار، خرید، مالی و عمومی
-- هیچ فرضی در مورد نام جداول وجود ندارد؛ کلیه اشیاء شِماهای هدف کشف می‌شوند.
-- --------------------------------------------------------------------------------------
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

-- --------------------------------------------------------------------------------------
-- ۴. استخراج ساختار ستون‌های کلیه جداول و ویوها در شِماهای انبار و مالی
-- ستون‌های جداول ('U') و ویوها ('V') با دقت استخراج می‌شوند بدون خواندن هیچ داده‌ای.
-- --------------------------------------------------------------------------------------
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

-- --------------------------------------------------------------------------------------
-- ۵. جستجوی ستون‌های کلیدی مرتبط با وضعیت اسناد، ابطال، سال مالی و موجودی در کلیه جداول و ویوها
-- این کوئری نام‌های واقعی ستون‌های وضعیت (Status/State/Cancel/Post/Approve/Date/Year) را مشخص می‌کند.
-- --------------------------------------------------------------------------------------
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

-- --------------------------------------------------------------------------------------
-- ۶. کشف کلیدهای اصلی (PK) و روابط کلید خارجی (FK) جداول انبار و مالی
-- --------------------------------------------------------------------------------------
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
