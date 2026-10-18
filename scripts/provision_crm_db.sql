-- ======================================================================================
-- ParkPalace Procurement CRM - Isolated CRM Database Provisioning Script
-- Target Server: Local SQL Server on PDC (192.168.1.250) or Development SQL Server
-- Target Database: ParkPalaceCRM_DB
-- NOTE: Strictly independent from Sepidar. Does NOT touch Sepidar databases.
-- ======================================================================================

USE [master];
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'ParkPalaceCRM_DB')
BEGIN
    CREATE DATABASE [ParkPalaceCRM_DB]
    COLLATE Persian_100_CI_AI;
    PRINT 'Database [ParkPalaceCRM_DB] created successfully.';
END
ELSE
BEGIN
    PRINT 'Database [ParkPalaceCRM_DB] already exists.';
END
GO

USE [ParkPalaceCRM_DB];
GO

-- 1. Table: Roles
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Roles')
BEGIN
    CREATE TABLE [dbo].[Roles] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [Name] NVARCHAR(50) NOT NULL,
        [Description] NVARCHAR(255) NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAtUtc] DATETIME2 NULL,
        [RowVersion] ROWVERSION NOT NULL
    );
    CREATE UNIQUE INDEX [IX_Roles_Name] ON [dbo].[Roles]([Name]);
    PRINT 'Table [Roles] created.';
END
GO

-- 2. Table: Permissions
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Permissions')
BEGIN
    CREATE TABLE [dbo].[Permissions] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [Code] NVARCHAR(100) NOT NULL,
        [Category] NVARCHAR(50) NULL,
        [Description] NVARCHAR(255) NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAtUtc] DATETIME2 NULL,
        [RowVersion] ROWVERSION NOT NULL
    );
    CREATE UNIQUE INDEX [IX_Permissions_Code] ON [dbo].[Permissions]([Code]);
    PRINT 'Table [Permissions] created.';
END
GO

-- 3. Table: Users
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Users')
BEGIN
    CREATE TABLE [dbo].[Users] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [AdObjectSid] NVARCHAR(128) NOT NULL,
        [Username] NVARCHAR(100) NOT NULL,
        [DisplayName] NVARCHAR(150) NOT NULL,
        [Email] NVARCHAR(150) NULL,
        [IsEnabled] BIT NOT NULL DEFAULT 1,
        [LastLoginAtUtc] DATETIME2 NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAtUtc] DATETIME2 NULL,
        [RowVersion] ROWVERSION NOT NULL
    );
    CREATE UNIQUE INDEX [IX_Users_AdObjectSid] ON [dbo].[Users]([AdObjectSid]);
    CREATE INDEX [IX_Users_Username] ON [dbo].[Users]([Username]);
    PRINT 'Table [Users] created.';
END
GO

-- 4. Table: UserRoles
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'UserRoles')
BEGIN
    CREATE TABLE [dbo].[UserRoles] (
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [RoleId] UNIQUEIDENTIFIER NOT NULL,
        [AssignedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_UserRoles] PRIMARY KEY CLUSTERED ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles]([Id]) ON DELETE CASCADE
    );
    PRINT 'Table [UserRoles] created.';
END
GO

-- 5. Table: RolePermissions
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'RolePermissions')
BEGIN
    CREATE TABLE [dbo].[RolePermissions] (
        [RoleId] UNIQUEIDENTIFIER NOT NULL,
        [PermissionId] UNIQUEIDENTIFIER NOT NULL,
        [GrantedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY CLUSTERED ([RoleId], [PermissionId]),
        CONSTRAINT [FK_RolePermissions_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RolePermissions_Permissions] FOREIGN KEY ([PermissionId]) REFERENCES [dbo].[Permissions]([Id]) ON DELETE CASCADE
    );
    PRINT 'Table [RolePermissions] created.';
END
GO

-- 6. Table: AuditEvents
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'AuditEvents')
BEGIN
    CREATE TABLE [dbo].[AuditEvents] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [TimestampUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [ActorId] UNIQUEIDENTIFIER NULL,
        [ActorUsername] NVARCHAR(100) NOT NULL,
        [Action] NVARCHAR(100) NOT NULL,
        [EntityType] NVARCHAR(100) NOT NULL,
        [EntityId] NVARCHAR(100) NULL,
        [DetailsJson] NVARCHAR(MAX) NULL,
        [IpAddress] NVARCHAR(50) NULL,
        [Succeeded] BIT NOT NULL DEFAULT 1,
        [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAtUtc] DATETIME2 NULL,
        [RowVersion] ROWVERSION NOT NULL
    );
    CREATE INDEX [IX_AuditEvents_TimestampUtc] ON [dbo].[AuditEvents]([TimestampUtc]);
    CREATE INDEX [IX_AuditEvents_ActorUsername] ON [dbo].[AuditEvents]([ActorUsername]);
    PRINT 'Table [AuditEvents] created.';
END
GO

-- 7. Table: IntegrationSyncRuns
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'IntegrationSyncRuns')
BEGIN
    CREATE TABLE [dbo].[IntegrationSyncRuns] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [SourceSystem] NVARCHAR(50) NOT NULL,
        [TargetEntity] NVARCHAR(50) NOT NULL,
        [StartedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [CompletedAtUtc] DATETIME2 NULL,
        [Status] NVARCHAR(30) NOT NULL DEFAULT 'Pending',
        [RecordsIngested] INT NOT NULL DEFAULT 0,
        [ErrorMessage] NVARCHAR(MAX) NULL,
        [WatermarkToken] NVARCHAR(255) NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAtUtc] DATETIME2 NULL,
        [RowVersion] ROWVERSION NOT NULL
    );
    CREATE INDEX [IX_IntegrationSyncRuns_StartedAtUtc] ON [dbo].[IntegrationSyncRuns]([StartedAtUtc]);
    PRINT 'Table [IntegrationSyncRuns] created.';
END
GO

PRINT '=== ParkPalaceCRM_DB Provisioning Script Finished Successfully ===';
GO
