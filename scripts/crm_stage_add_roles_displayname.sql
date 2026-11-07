-- Staging-only additive patch: Roles.DisplayName required by dashboard-exec-v1 EF model.
-- Does NOT target Production. Idempotent.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IS NULL
    THROW 52110, N'Run inside the staging database.', 1;

IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
    THROW 52110, N'dbo.Roles missing.', 1;

IF COL_LENGTH(N'dbo.Roles', N'DisplayName') IS NULL
BEGIN
    ALTER TABLE dbo.Roles ADD DisplayName nvarchar(120) NOT NULL
        CONSTRAINT DF_Roles_DisplayName DEFAULT (N'');
END
GO

UPDATE dbo.Roles
SET DisplayName = CASE
    WHEN Description IS NOT NULL AND LTRIM(RTRIM(Description)) <> N'' THEN LEFT(Description, 120)
    ELSE Name
END
WHERE DisplayName IS NULL OR DisplayName = N'';
GO

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = N'Roles'
ORDER BY ORDINAL_POSITION;
GO
