-- ==============================================================================
-- Script: repair_users_displayname_v103.sql
-- Description: Targeted Unicode repair for Persian DisplayName in dbo.Users table.
-- Safety:
--   1. Creates automatic snapshot table dbo.Users_Backup_PreDisplayNameFix
--   2. Explicit individual WHERE Username = ... updates (NO mass/wildcard replaces)
--   3. Does NOT modify any User IDs, UserRoles, Role assignments or Permissions
--   4. Fully transactional with verification
-- ==============================================================================

USE [ParkPalaceCRM];
GO

SET NOCOUNT ON;

BEGIN TRANSACTION;

-- 1. Create backup snapshot if not already exists
IF OBJECT_ID('dbo.Users_Backup_PreDisplayNameFix', 'U') IS NULL
BEGIN
    SELECT * INTO dbo.Users_Backup_PreDisplayNameFix FROM dbo.Users;
    PRINT N'[BACKUP] Created snapshot table dbo.Users_Backup_PreDisplayNameFix';
END
ELSE
BEGIN
    PRINT N'[BACKUP] Snapshot table dbo.Users_Backup_PreDisplayNameFix already exists';
END

-- 2. Targeted update of DisplayName using verified N'' Unicode literals
UPDATE dbo.Users 
SET DisplayName = N'مدیر سرور CRM (Admin)'
WHERE Username = 'CRM\Administrator';

UPDATE dbo.Users 
SET DisplayName = N'مدیر ارشد سیستم (Admin)'
WHERE Username = 'SAHAR\Administrator';

UPDATE dbo.Users 
SET DisplayName = N'کارشناس تدارکات'
WHERE Username = 'SAHAR\procurement';

UPDATE dbo.Users 
SET DisplayName = N'سرپرست انبار'
WHERE Username = 'SAHAR\warehouse';

UPDATE dbo.Users 
SET DisplayName = N'کارشناس حسابداری'
WHERE Username = 'SAHAR\finance';

UPDATE dbo.Users 
SET DisplayName = N'مدیر ناظر بازرگانی'
WHERE Username = 'SAHAR\manager';

-- 3. Verification query
PRINT N'[VERIFICATION] Result of updated DisplayNames:';
SELECT Id, Username, DisplayName, IsEnabled 
FROM dbo.Users
ORDER BY Username;

COMMIT TRANSACTION;
PRINT N'[SUCCESS] Users.DisplayName repair completed successfully.';
GO
