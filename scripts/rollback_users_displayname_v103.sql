-- ==============================================================================
-- Script: rollback_users_displayname_v103.sql
-- Description: Rollback script for Users.DisplayName from snapshot backup table.
-- ==============================================================================

USE [ParkPalaceCRM];
GO

SET NOCOUNT ON;

BEGIN TRANSACTION;

IF OBJECT_ID('dbo.Users_Backup_PreDisplayNameFix', 'U') IS NOT NULL
BEGIN
    UPDATE u
    SET u.DisplayName = b.DisplayName
    FROM dbo.Users u
    INNER JOIN dbo.Users_Backup_PreDisplayNameFix b ON u.Id = b.Id;

    PRINT N'[ROLLBACK] Restored DisplayName from dbo.Users_Backup_PreDisplayNameFix';
    
    SELECT Id, Username, DisplayName 
    FROM dbo.Users
    ORDER BY Username;
    
    COMMIT TRANSACTION;
    PRINT N'[SUCCESS] Rollback completed successfully.';
END
ELSE
BEGIN
    ROLLBACK TRANSACTION;
    RAISERROR(N'[ERROR] Backup snapshot table dbo.Users_Backup_PreDisplayNameFix does not exist!', 16, 1);
END
GO
