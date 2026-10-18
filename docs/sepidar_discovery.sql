-- ======================================================================================
-- ParkPalace Procurement CRM - Phase 1: Safe Read-Only Sepidar Discovery Script
-- Target: Sepidar01 / Sepidar02 on SEPIDAR\SEPIDAR (Port 62753 / Instance MSSQL15.SEPIDAR)
-- NOTE: This script is STRICTLY READ-ONLY (SELECT only). It creates no objects and mutates no data.
-- ======================================================================================

SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

-- 1. Database & Version Verification
SELECT 
    DB_NAME() AS CurrentDatabase,
    @@SERVERNAME AS ServerName,
    @@VERSION AS SQLVersion,
    DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS Collation,
    DATABASEPROPERTYEX(DB_NAME(), 'Status') AS DBStatus;

-- 2. Schemas & Object Summary
SELECT 
    s.name AS SchemaName,
    COUNT(t.object_id) AS TableCount
FROM sys.schemas s
LEFT JOIN sys.tables t ON s.schema_id = t.schema_id
GROUP BY s.name
ORDER BY TableCount DESC, s.name;

-- 3. All Tables with Approximate Row Counts & Descriptions
SELECT 
    s.name AS SchemaName,
    t.name AS TableName,
    p.rows AS ApproximateRowCount,
    CAST(ep.value AS NVARCHAR(255)) AS TableDescription
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
LEFT JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1)
LEFT JOIN sys.extended_properties ep ON ep.major_id = t.object_id AND ep.minor_id = 0 AND ep.name = 'MS_Description'
ORDER BY s.name, t.name;

-- 4. Key Procurement / Inventory / Supplier Tables Detailed Columns
-- Focus on schemas: INV (Inventory), PUR (Purchasing), CNT (Contacts/Parties), GNR (General), FM (Finance)
SELECT 
    s.name AS SchemaName,
    t.name AS TableName,
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
INNER JOIN sys.tables t ON c.object_id = t.object_id
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
LEFT JOIN sys.extended_properties ep ON ep.major_id = t.object_id AND ep.minor_id = c.column_id AND ep.name = 'MS_Description'
WHERE s.name IN ('INV', 'PUR', 'CNT', 'GNR', 'FM', 'SLS', 'dbo')
ORDER BY s.name, t.name, c.column_id;

-- 5. Primary Keys & Foreign Keys (Relationship Discovery)
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
ORDER BY ParentSchema, ParentTable, fkc.constraint_column_id;
