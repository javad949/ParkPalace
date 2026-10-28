-- ==============================================================================
-- ParkPalace CRM - Targeted Rollback Script: Seed Tables Mojibake Fix
-- Target Database: ParkPalaceCRM
-- Rollback Source: dbo.*_Backup_PreMojibakeFix tables
-- ==============================================================================

USE [ParkPalaceCRM];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID('dbo.Categories_Backup_PreMojibakeFix', 'U') IS NOT NULL
BEGIN
    UPDATE c
    SET c.Name = b.Name, c.Description = b.Description
    FROM dbo.Categories c
    INNER JOIN dbo.Categories_Backup_PreMojibakeFix b ON c.Id = b.Id;
    PRINT 'Categories restored from backup.';
END

IF OBJECT_ID('dbo.UnitsOfMeasure_Backup_PreMojibakeFix', 'U') IS NOT NULL
BEGIN
    UPDATE u
    SET u.Name = b.Name
    FROM dbo.UnitsOfMeasure u
    INNER JOIN dbo.UnitsOfMeasure_Backup_PreMojibakeFix b ON u.Id = b.Id;
    PRINT 'UnitsOfMeasure restored from backup.';
END

IF OBJECT_ID('dbo.Warehouses_Backup_PreMojibakeFix', 'U') IS NOT NULL
BEGIN
    UPDATE w
    SET w.Name = b.Name, w.Description = b.Description
    FROM dbo.Warehouses w
    INNER JOIN dbo.Warehouses_Backup_PreMojibakeFix b ON w.Id = b.Id;
    PRINT 'Warehouses restored from backup.';
END

IF OBJECT_ID('dbo.Roles_Backup_PreMojibakeFix', 'U') IS NOT NULL
BEGIN
    UPDATE r
    SET r.Description = b.Description
    FROM dbo.Roles r
    INNER JOIN dbo.Roles_Backup_PreMojibakeFix b ON r.Id = b.Id;
    PRINT 'Roles restored from backup.';
END

IF OBJECT_ID('dbo.Permissions_Backup_PreMojibakeFix', 'U') IS NOT NULL
BEGIN
    UPDATE p
    SET p.Description = b.Description
    FROM dbo.Permissions p
    INNER JOIN dbo.Permissions_Backup_PreMojibakeFix b ON p.Id = b.Id;
    PRINT 'Permissions restored from backup.';
END

COMMIT TRANSACTION;
PRINT 'Rollback completed successfully.';
GO
