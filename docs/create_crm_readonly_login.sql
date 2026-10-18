-- ======================================================================================
-- ParkPalace Procurement CRM - Dedicated Read-Only SQL Login for Sepidar
-- Target Server: SEPIDAR\SEPIDAR (Port 62753)
-- Purpose: Fulfill AC-01 (Strictly enforce SELECT-only, no INSERT/UPDATE/DELETE/EXECUTE)
-- Run as: System Administrator ('sa') in SQL Server Management Studio (SSMS)
-- ======================================================================================

USE [master];
GO

-- 1. Create Login if it does not exist
IF NOT EXISTS (SELECT name FROM sys.server_principals WHERE name = 'crm_reader')
BEGIN
    -- Please replace 'StrongReadonlyPassword1405!' with a secure password of your choice
    CREATE LOGIN [crm_reader] WITH PASSWORD = 'StrongReadonlyPassword1405!', 
        CHECK_EXPIRATION = OFF, 
        CHECK_POLICY = ON;
    PRINT 'Login [crm_reader] created successfully.';
END
ELSE
BEGIN
    PRINT 'Login [crm_reader] already exists.';
END
GO

-- 2. Configure Read-Only Access in Target Sepidar Database (Sepidar01)
USE [Sepidar01];
GO

IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = 'crm_reader')
BEGIN
    CREATE USER [crm_reader] FOR LOGIN [crm_reader];
    PRINT 'User [crm_reader] created in Sepidar01.';
END
GO

-- Grant db_datareader (SELECT on all tables and views)
ALTER ROLE [db_datareader] ADD MEMBER [crm_reader];

-- Explicitly DENY all mutating actions (Defense-in-depth)
DENY INSERT TO [crm_reader];
DENY UPDATE TO [crm_reader];
DENY DELETE TO [crm_reader];
DENY EXECUTE TO [crm_reader];
DENY ALTER TO [crm_reader];
PRINT 'Granted db_datareader and explicitly DENIED INSERT, UPDATE, DELETE, EXECUTE to [crm_reader] in Sepidar01.';
GO

-- 3. Configure in Sepidar02 as well (if relevant company data is in Sepidar02)
USE [Sepidar02];
GO

IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = 'crm_reader')
BEGIN
    CREATE USER [crm_reader] FOR LOGIN [crm_reader];
    PRINT 'User [crm_reader] created in Sepidar02.';
END
GO

ALTER ROLE [db_datareader] ADD MEMBER [crm_reader];
DENY INSERT TO [crm_reader];
DENY UPDATE TO [crm_reader];
DENY DELETE TO [crm_reader];
DENY EXECUTE TO [crm_reader];
DENY ALTER TO [crm_reader];
PRINT 'Configured [crm_reader] in Sepidar02.';
GO

-- 4. Verify Effective Permissions
USE [Sepidar01];
GO
SELECT 
    pr.name AS PrincipalName,
    pe.permission_name AS Permission,
    pe.state_desc AS StateDesc,
    OBJECT_NAME(pe.major_id) AS ObjectName
FROM sys.database_permissions pe
INNER JOIN sys.database_principals pr ON pe.grantee_principal_id = pr.principal_id
WHERE pr.name = 'crm_reader';
GO
