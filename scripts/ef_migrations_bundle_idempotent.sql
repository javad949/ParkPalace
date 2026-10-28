IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017183302_InitialCrmIdentityAndAudit'
)
BEGIN
    CREATE TABLE [AuditEvents] (
        [Id] uniqueidentifier NOT NULL,
        [TimestampUtc] datetime2 NOT NULL,
        [ActorId] uniqueidentifier NULL,
        [ActorUsername] nvarchar(100) NOT NULL,
        [Action] nvarchar(100) NOT NULL,
        [EntityType] nvarchar(100) NOT NULL,
        [EntityId] nvarchar(100) NOT NULL,
        [DetailsJson] nvarchar(max) NULL,
        [IpAddress] nvarchar(50) NULL,
        [Succeeded] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_AuditEvents] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017183302_InitialCrmIdentityAndAudit'
)
BEGIN
    CREATE TABLE [IntegrationSyncRuns] (
        [Id] uniqueidentifier NOT NULL,
        [SourceSystem] nvarchar(50) NOT NULL,
        [TargetEntity] nvarchar(50) NOT NULL,
        [StartedAtUtc] datetime2 NOT NULL,
        [CompletedAtUtc] datetime2 NULL,
        [Status] nvarchar(30) NOT NULL,
        [RecordsIngested] int NOT NULL,
        [ErrorMessage] nvarchar(max) NULL,
        [WatermarkToken] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_IntegrationSyncRuns] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017183302_InitialCrmIdentityAndAudit'
)
BEGIN
    CREATE TABLE [Permissions] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(100) NOT NULL,
        [Description] nvarchar(255) NOT NULL,
        [Category] nvarchar(50) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017183302_InitialCrmIdentityAndAudit'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        [Description] nvarchar(255) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017183302_InitialCrmIdentityAndAudit'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [AdObjectSid] nvarchar(128) NOT NULL,
        [Username] nvarchar(100) NOT NULL,
        [DisplayName] nvarchar(150) NOT NULL,
        [Email] nvarchar(150) NULL,
        [IsEnabled] bit NOT NULL,
        [LastLoginAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017183302_InitialCrmIdentityAndAudit'
)
BEGIN
    CREATE TABLE [RolePermissions] (
        [RoleId] uniqueidentifier NOT NULL,
        [PermissionId] uniqueidentifier NOT NULL,
        [GrantedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId]),
        CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017183302_InitialCrmIdentityAndAudit'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [AssignedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017183302_InitialCrmIdentityAndAudit'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_ActorUsername] ON [AuditEvents] ([ActorUsername]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017183302_InitialCrmIdentityAndAudit'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_TimestampUtc] ON [AuditEvents] ([TimestampUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017183302_InitialCrmIdentityAndAudit'
)
BEGIN
    CREATE INDEX [IX_IntegrationSyncRuns_StartedAtUtc] ON [IntegrationSyncRuns] ([StartedAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017183302_InitialCrmIdentityAndAudit'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Permissions_Code] ON [Permissions] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017183302_InitialCrmIdentityAndAudit'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_PermissionId] ON [RolePermissions] ([PermissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017183302_InitialCrmIdentityAndAudit'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017183302_InitialCrmIdentityAndAudit'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017183302_InitialCrmIdentityAndAudit'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_AdObjectSid] ON [Users] ([AdObjectSid]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017183302_InitialCrmIdentityAndAudit'
)
BEGIN
    CREATE INDEX [IX_Users_Username] ON [Users] ([Username]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017183302_InitialCrmIdentityAndAudit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261017183302_InitialCrmIdentityAndAudit', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] uniqueidentifier NOT NULL,
        [ParentId] uniqueidentifier NULL,
        [Name] nvarchar(200) NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Categories_Categories_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE TABLE [Parties] (
        [Id] uniqueidentifier NOT NULL,
        [PartyType] int NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [FirstName] nvarchar(100) NULL,
        [LastName] nvarchar(100) NULL,
        [ShopName] nvarchar(200) NULL,
        [NationalId] nvarchar(20) NULL,
        [LegalName] nvarchar(200) NULL,
        [NationalIdentifier] nvarchar(30) NULL,
        [RegistrationNumber] nvarchar(50) NULL,
        [EconomicCode] nvarchar(30) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Parties] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE TABLE [UnitsOfMeasure] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Precision] int NOT NULL,
        [Dimension] nvarchar(50) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_UnitsOfMeasure] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE TABLE [PartyAddresses] (
        [Id] uniqueidentifier NOT NULL,
        [PartyId] uniqueidentifier NOT NULL,
        [AddressType] nvarchar(50) NOT NULL,
        [Province] nvarchar(100) NOT NULL,
        [City] nvarchar(100) NOT NULL,
        [AddressText] nvarchar(500) NOT NULL,
        [PostalCode] nvarchar(20) NULL,
        [IsPrimary] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PartyAddresses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PartyAddresses_Parties_PartyId] FOREIGN KEY ([PartyId]) REFERENCES [Parties] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE TABLE [PartyContacts] (
        [Id] uniqueidentifier NOT NULL,
        [PartyId] uniqueidentifier NOT NULL,
        [PersonName] nvarchar(150) NOT NULL,
        [Position] nvarchar(100) NULL,
        [IsPrimary] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PartyContacts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PartyContacts_Parties_PartyId] FOREIGN KEY ([PartyId]) REFERENCES [Parties] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE TABLE [PartyDocuments] (
        [Id] uniqueidentifier NOT NULL,
        [PartyId] uniqueidentifier NOT NULL,
        [DocumentType] nvarchar(100) NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [FileExtension] nvarchar(20) NOT NULL,
        [FileSizeBytes] bigint NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [StorageReference] nvarchar(500) NOT NULL,
        [UploadedByUserId] uniqueidentifier NULL,
        [UploadedAtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PartyDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PartyDocuments_Parties_PartyId] FOREIGN KEY ([PartyId]) REFERENCES [Parties] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE TABLE [Materials] (
        [Id] uniqueidentifier NOT NULL,
        [InternalCode] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [BaseUnitId] uniqueidentifier NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Specifications] nvarchar(4000) NULL,
        [Brand] nvarchar(100) NULL,
        [MinStock] decimal(18,4) NULL,
        [SafetyStock] decimal(18,4) NULL,
        [LeadTimeDays] int NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Materials] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Materials_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Materials_UnitsOfMeasure_BaseUnitId] FOREIGN KEY ([BaseUnitId]) REFERENCES [UnitsOfMeasure] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE TABLE [PartyPhones] (
        [Id] uniqueidentifier NOT NULL,
        [PartyId] uniqueidentifier NOT NULL,
        [ContactId] uniqueidentifier NULL,
        [PhoneType] nvarchar(50) NOT NULL,
        [OriginalValue] nvarchar(50) NOT NULL,
        [NormalizedValue] nvarchar(50) NOT NULL,
        [IsPrimary] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PartyPhones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PartyPhones_Parties_PartyId] FOREIGN KEY ([PartyId]) REFERENCES [Parties] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PartyPhones_PartyContacts_ContactId] FOREIGN KEY ([ContactId]) REFERENCES [PartyContacts] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE TABLE [ExternalItemMappings] (
        [Id] uniqueidentifier NOT NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [SourceSystem] nvarchar(50) NOT NULL,
        [SourceCompany] nvarchar(100) NOT NULL,
        [ExternalItemId] nvarchar(100) NOT NULL,
        [ExternalItemCode] nvarchar(100) NOT NULL,
        [LastSeenUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ExternalItemMappings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExternalItemMappings_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE TABLE [MaterialUnitConversions] (
        [Id] uniqueidentifier NOT NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [FromUnitId] uniqueidentifier NOT NULL,
        [ToUnitId] uniqueidentifier NOT NULL,
        [Factor] decimal(18,6) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MaterialUnitConversions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MaterialUnitConversions_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MaterialUnitConversions_UnitsOfMeasure_FromUnitId] FOREIGN KEY ([FromUnitId]) REFERENCES [UnitsOfMeasure] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MaterialUnitConversions_UnitsOfMeasure_ToUnitId] FOREIGN KEY ([ToUnitId]) REFERENCES [UnitsOfMeasure] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE TABLE [SupplierMaterials] (
        [Id] uniqueidentifier NOT NULL,
        [PartyId] uniqueidentifier NOT NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [IsPreferred] bit NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [DefaultLeadTimeDays] int NULL,
        [MinimumOrderQty] decimal(18,4) NULL,
        [PurchaseUnitId] uniqueidentifier NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SupplierMaterials] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupplierMaterials_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupplierMaterials_Parties_PartyId] FOREIGN KEY ([PartyId]) REFERENCES [Parties] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SupplierMaterials_UnitsOfMeasure_PurchaseUnitId] FOREIGN KEY ([PurchaseUnitId]) REFERENCES [UnitsOfMeasure] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE TABLE [SupplierPriceHistories] (
        [Id] uniqueidentifier NOT NULL,
        [SupplierMaterialId] uniqueidentifier NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [Currency] nvarchar(20) NOT NULL,
        [UnitId] uniqueidentifier NOT NULL,
        [EffectiveFrom] datetime2 NOT NULL,
        [EffectiveTo] datetime2 NULL,
        [RecordedAt] datetime2 NOT NULL,
        [Source] nvarchar(200) NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SupplierPriceHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupplierPriceHistories_SupplierMaterials_SupplierMaterialId] FOREIGN KEY ([SupplierMaterialId]) REFERENCES [SupplierMaterials] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SupplierPriceHistories_UnitsOfMeasure_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [UnitsOfMeasure] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_Code] ON [Categories] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_Categories_ParentId] ON [Categories] ([ParentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_ExternalItemMappings_MaterialId] ON [ExternalItemMappings] ([MaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ExternalItemMappings_SourceSystem_SourceCompany_ExternalItemId] ON [ExternalItemMappings] ([SourceSystem], [SourceCompany], [ExternalItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_Materials_BaseUnitId] ON [Materials] ([BaseUnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_Materials_CategoryId] ON [Materials] ([CategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Materials_InternalCode] ON [Materials] ([InternalCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_MaterialUnitConversions_FromUnitId] ON [MaterialUnitConversions] ([FromUnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MaterialUnitConversions_MaterialId_FromUnitId_ToUnitId] ON [MaterialUnitConversions] ([MaterialId], [FromUnitId], [ToUnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_MaterialUnitConversions_ToUnitId] ON [MaterialUnitConversions] ([ToUnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Parties_NationalId] ON [Parties] ([NationalId]) WHERE [NationalId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Parties_NationalIdentifier] ON [Parties] ([NationalIdentifier]) WHERE [NationalIdentifier] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_PartyAddresses_PartyId] ON [PartyAddresses] ([PartyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_PartyContacts_PartyId] ON [PartyContacts] ([PartyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_PartyDocuments_PartyId] ON [PartyDocuments] ([PartyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_PartyPhones_ContactId] ON [PartyPhones] ([ContactId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_PartyPhones_NormalizedValue] ON [PartyPhones] ([NormalizedValue]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_PartyPhones_PartyId] ON [PartyPhones] ([PartyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_SupplierMaterials_MaterialId] ON [SupplierMaterials] ([MaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SupplierMaterials_PartyId_MaterialId] ON [SupplierMaterials] ([PartyId], [MaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_SupplierMaterials_PurchaseUnitId] ON [SupplierMaterials] ([PurchaseUnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_SupplierPriceHistories_SupplierMaterialId_EffectiveFrom] ON [SupplierPriceHistories] ([SupplierMaterialId], [EffectiveFrom]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_SupplierPriceHistories_UnitId] ON [SupplierPriceHistories] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UnitsOfMeasure_Code] ON [UnitsOfMeasure] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017190643_Phase3_CatalogAndSuppliers'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261017190643_Phase3_CatalogAndSuppliers', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE TABLE [InventoryItemMappings] (
        [Id] uniqueidentifier NOT NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [SepidarItemId] nvarchar(50) NOT NULL,
        [SepidarItemCode] nvarchar(50) NOT NULL,
        [SepidarItemTitle] nvarchar(250) NOT NULL,
        [SepidarBaseUnit] nvarchar(50) NULL,
        [IsActive] bit NOT NULL,
        [LastSyncUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_InventoryItemMappings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventoryItemMappings_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE TABLE [InventorySyncJobs] (
        [Id] uniqueidentifier NOT NULL,
        [JobType] int NOT NULL,
        [Status] int NOT NULL,
        [StartedAtUtc] datetime2 NOT NULL,
        [CompletedAtUtc] datetime2 NULL,
        [ItemsProcessed] int NOT NULL,
        [MovementsProcessed] int NOT NULL,
        [SnapshotsUpdated] int NOT NULL,
        [ErrorMessage] nvarchar(4000) NULL,
        [CorrelationId] nvarchar(50) NOT NULL,
        [TriggeredBy] nvarchar(100) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_InventorySyncJobs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE TABLE [MaterialConsumptionSummaries] (
        [Id] uniqueidentifier NOT NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [PeriodDays] int NOT NULL,
        [TotalConsumption] decimal(18,4) NOT NULL,
        [AverageDailyConsumption] decimal(18,4) NOT NULL,
        [ActiveDaysCount] int NOT NULL,
        [ZeroDaysCount] int NOT NULL,
        [CalculatedAtUtc] datetime2 NOT NULL,
        [FromDateUtc] datetime2 NOT NULL,
        [ToDateUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MaterialConsumptionSummaries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MaterialConsumptionSummaries_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE TABLE [ReplenishmentPolicies] (
        [Id] uniqueidentifier NOT NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [ReviewPeriodDays] int NOT NULL,
        [LeadTimeDays] int NOT NULL,
        [SafetyStock] decimal(18,4) NOT NULL,
        [MinOrderQuantity] decimal(18,4) NOT NULL,
        [PackageMultiple] decimal(18,4) NULL,
        [PreferredSupplierId] uniqueidentifier NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ReplenishmentPolicies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReplenishmentPolicies_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReplenishmentPolicies_Parties_PreferredSupplierId] FOREIGN KEY ([PreferredSupplierId]) REFERENCES [Parties] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE TABLE [Warehouses] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [SepidarWarehouseRef] nvarchar(50) NULL,
        [IsActive] bit NOT NULL,
        [LastSyncUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Warehouses] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE TABLE [InventorySyncErrors] (
        [Id] uniqueidentifier NOT NULL,
        [JobId] uniqueidentifier NOT NULL,
        [ItemKey] nvarchar(50) NULL,
        [VoucherNumber] nvarchar(50) NULL,
        [ErrorCode] nvarchar(50) NOT NULL,
        [ErrorMessage] nvarchar(2000) NOT NULL,
        [RawPayload] nvarchar(4000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_InventorySyncErrors] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventorySyncErrors_InventorySyncJobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [InventorySyncJobs] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE TABLE [InventoryMovements] (
        [Id] uniqueidentifier NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [VoucherNumber] nvarchar(50) NOT NULL,
        [MovementType] int NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [UnitId] uniqueidentifier NOT NULL,
        [TransactionDateUtc] datetime2 NOT NULL,
        [PostingStatus] int NOT NULL,
        [IsCancelled] bit NOT NULL,
        [ReferenceDocument] nvarchar(150) NULL,
        [SourceMovementKey] nvarchar(150) NULL,
        [ImportedAtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_InventoryMovements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventoryMovements_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryMovements_UnitsOfMeasure_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [UnitsOfMeasure] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryMovements_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE TABLE [InventorySnapshots] (
        [Id] uniqueidentifier NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [OnHandQuantity] decimal(18,4) NOT NULL,
        [ReservableQuantity] decimal(18,4) NOT NULL,
        [IncomingQuantity] decimal(18,4) NOT NULL,
        [ReservedQuantity] decimal(18,4) NOT NULL,
        [UnitId] uniqueidentifier NOT NULL,
        [SnapshotTimeUtc] datetime2 NOT NULL,
        [IsStale] bit NOT NULL,
        [SourceSnapshotKey] nvarchar(150) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_InventorySnapshots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventorySnapshots_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventorySnapshots_UnitsOfMeasure_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [UnitsOfMeasure] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventorySnapshots_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE TABLE [InventorySyncCheckpoints] (
        [Id] uniqueidentifier NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [LastVoucherId] nvarchar(50) NOT NULL,
        [LastVoucherDateUtc] datetime2 NULL,
        [LastSyncTimeUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_InventorySyncCheckpoints] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventorySyncCheckpoints_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE TABLE [PurchaseRecommendations] (
        [Id] uniqueidentifier NOT NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [CurrentOnHand] decimal(18,4) NOT NULL,
        [ReservableOnHand] decimal(18,4) NOT NULL,
        [IncomingOrders] decimal(18,4) NOT NULL,
        [ReservedQuantity] decimal(18,4) NOT NULL,
        [AverageDailyConsumption] decimal(18,4) NOT NULL,
        [LeadTimeDays] int NOT NULL,
        [ReviewPeriodDays] int NOT NULL,
        [SafetyStock] decimal(18,4) NOT NULL,
        [TargetStock] decimal(18,4) NOT NULL,
        [InventoryPosition] decimal(18,4) NOT NULL,
        [RawSuggestedQuantity] decimal(18,4) NOT NULL,
        [RoundedSuggestedQuantity] decimal(18,4) NOT NULL,
        [UnitId] uniqueidentifier NOT NULL,
        [PreferredSupplierId] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [RecommendationExplanation] nvarchar(2000) NULL,
        [DataWarning] nvarchar(1000) NULL,
        [GeneratedAtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PurchaseRecommendations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseRecommendations_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseRecommendations_Parties_PreferredSupplierId] FOREIGN KEY ([PreferredSupplierId]) REFERENCES [Parties] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseRecommendations_UnitsOfMeasure_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [UnitsOfMeasure] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseRecommendations_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE INDEX [IX_InventoryItemMappings_MaterialId] ON [InventoryItemMappings] ([MaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InventoryItemMappings_SepidarItemId] ON [InventoryItemMappings] ([SepidarItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE INDEX [IX_InventoryMovements_MaterialId_TransactionDateUtc] ON [InventoryMovements] ([MaterialId], [TransactionDateUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_InventoryMovements_SourceMovementKey] ON [InventoryMovements] ([SourceMovementKey]) WHERE [SourceMovementKey] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE INDEX [IX_InventoryMovements_UnitId] ON [InventoryMovements] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE INDEX [IX_InventoryMovements_WarehouseId_MovementType_PostingStatus] ON [InventoryMovements] ([WarehouseId], [MovementType], [PostingStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE INDEX [IX_InventorySnapshots_MaterialId] ON [InventorySnapshots] ([MaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE INDEX [IX_InventorySnapshots_UnitId] ON [InventorySnapshots] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InventorySnapshots_WarehouseId_MaterialId] ON [InventorySnapshots] ([WarehouseId], [MaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InventorySyncCheckpoints_WarehouseId] ON [InventorySyncCheckpoints] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE INDEX [IX_InventorySyncErrors_JobId] ON [InventorySyncErrors] ([JobId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE INDEX [IX_InventorySyncJobs_CorrelationId] ON [InventorySyncJobs] ([CorrelationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MaterialConsumptionSummaries_MaterialId_PeriodDays] ON [MaterialConsumptionSummaries] ([MaterialId], [PeriodDays]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE INDEX [IX_PurchaseRecommendations_MaterialId_Status] ON [PurchaseRecommendations] ([MaterialId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE INDEX [IX_PurchaseRecommendations_PreferredSupplierId] ON [PurchaseRecommendations] ([PreferredSupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE INDEX [IX_PurchaseRecommendations_UnitId] ON [PurchaseRecommendations] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE INDEX [IX_PurchaseRecommendations_WarehouseId] ON [PurchaseRecommendations] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReplenishmentPolicies_MaterialId] ON [ReplenishmentPolicies] ([MaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE INDEX [IX_ReplenishmentPolicies_PreferredSupplierId] ON [ReplenishmentPolicies] ([PreferredSupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Warehouses_Code] ON [Warehouses] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017200046_Phase4_InventoryAndWarehouse'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261017200046_Phase4_InventoryAndWarehouse', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    IF SCHEMA_ID(N'crm') IS NULL EXEC(N'CREATE SCHEMA [crm];');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE TABLE [crm].[PurchaseOrders] (
        [Id] uniqueidentifier NOT NULL,
        [OrderNumber] nvarchar(50) NOT NULL,
        [SupplierId] uniqueidentifier NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [Currency] nvarchar(10) NOT NULL,
        [ExchangeRate] decimal(18,4) NOT NULL,
        [SubTotal] decimal(18,2) NOT NULL,
        [TaxTotal] decimal(18,2) NOT NULL,
        [DiscountTotal] decimal(18,2) NOT NULL,
        [GrandTotal] decimal(18,2) NOT NULL,
        [IssuedByUserId] uniqueidentifier NULL,
        [IssuedByUsername] nvarchar(100) NULL,
        [IssuedAtUtc] datetime2 NULL,
        [ExpectedDeliveryDateUtc] datetime2 NULL,
        [PaymentTerms] nvarchar(200) NULL,
        [DeliveryTerms] nvarchar(200) NULL,
        [Notes] nvarchar(1000) NULL,
        [ConcurrencyToken] nvarchar(100) NOT NULL,
        [CancelledAtUtc] datetime2 NULL,
        [CancelledBy] nvarchar(100) NULL,
        [CancellationReason] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_PurchaseOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseOrders_Parties_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Parties] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrders_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE TABLE [crm].[PurchaseRequests] (
        [Id] uniqueidentifier NOT NULL,
        [RequestNumber] nvarchar(50) NOT NULL,
        [RequesterName] nvarchar(100) NOT NULL,
        [Department] nvarchar(100) NULL,
        [NeededByDateUtc] datetime2 NULL,
        [Status] int NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_PurchaseRequests] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE TABLE [crm].[RequestForQuotations] (
        [Id] uniqueidentifier NOT NULL,
        [RfqNumber] nvarchar(50) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [DeadlineUtc] datetime2 NULL,
        [Status] int NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_RequestForQuotations] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE TABLE [crm].[GoodsReceipts] (
        [Id] uniqueidentifier NOT NULL,
        [ReceiptNumber] nvarchar(50) NOT NULL,
        [PurchaseOrderId] uniqueidentifier NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [ReceivedByUserId] uniqueidentifier NULL,
        [ReceivedByUsername] nvarchar(100) NOT NULL,
        [ReceiptDateUtc] datetime2 NOT NULL,
        [CarrierName] nvarchar(150) NULL,
        [DeliveryNoteNumber] nvarchar(100) NULL,
        [VehiclePlateNumber] nvarchar(50) NULL,
        [Status] int NOT NULL,
        [SepidarMovementRef] nvarchar(100) NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_GoodsReceipts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GoodsReceipts_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [crm].[PurchaseOrders] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GoodsReceipts_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE TABLE [crm].[PurchaseOrderAudits] (
        [Id] uniqueidentifier NOT NULL,
        [PurchaseOrderId] uniqueidentifier NOT NULL,
        [Action] nvarchar(100) NOT NULL,
        [PerformedBy] nvarchar(100) NOT NULL,
        [TimestampUtc] datetime2 NOT NULL,
        [DetailsJson] nvarchar(4000) NULL,
        CONSTRAINT [PK_PurchaseOrderAudits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseOrderAudits_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [crm].[PurchaseOrders] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE TABLE [crm].[PurchaseOrderLines] (
        [Id] uniqueidentifier NOT NULL,
        [PurchaseOrderId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [OrderedQuantity] decimal(18,4) NOT NULL,
        [UnitId] uniqueidentifier NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [TaxRate] decimal(18,4) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        [ReceivedQuantity] decimal(18,4) NOT NULL,
        [InvoicedQuantity] decimal(18,4) NOT NULL,
        [MaterialCodeSnapshot] nvarchar(50) NULL,
        [MaterialNameSnapshot] nvarchar(200) NULL,
        [UnitCodeSnapshot] nvarchar(20) NULL,
        [UnitNameSnapshot] nvarchar(50) NULL,
        [SupplierNameSnapshot] nvarchar(200) NULL,
        [Notes] nvarchar(500) NULL,
        CONSTRAINT [PK_PurchaseOrderLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseOrderLines_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrderLines_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [crm].[PurchaseOrders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PurchaseOrderLines_UnitsOfMeasure_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [UnitsOfMeasure] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE TABLE [crm].[SupplierInvoices] (
        [Id] uniqueidentifier NOT NULL,
        [InvoiceNumber] nvarchar(100) NOT NULL,
        [SupplierId] uniqueidentifier NOT NULL,
        [PurchaseOrderId] uniqueidentifier NULL,
        [InvoiceDateUtc] datetime2 NOT NULL,
        [DueDateUtc] datetime2 NULL,
        [ReceivedDateUtc] datetime2 NOT NULL,
        [Currency] nvarchar(10) NOT NULL,
        [ExchangeRate] decimal(18,4) NOT NULL,
        [SubTotal] decimal(18,2) NOT NULL,
        [TaxTotal] decimal(18,2) NOT NULL,
        [DiscountTotal] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [RegisteredInSepidar] bit NOT NULL,
        [SepidarInvoiceRef] nvarchar(100) NULL,
        [SepidarVoucherNumber] nvarchar(100) NULL,
        [AttachmentFileName] nvarchar(255) NULL,
        [AttachmentStoragePath] nvarchar(500) NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_SupplierInvoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupplierInvoices_Parties_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Parties] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupplierInvoices_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [crm].[PurchaseOrders] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE TABLE [crm].[PurchaseRequestLines] (
        [Id] uniqueidentifier NOT NULL,
        [PurchaseRequestId] uniqueidentifier NOT NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [UnitId] uniqueidentifier NOT NULL,
        [EstimatedUnitPrice] decimal(18,2) NULL,
        [Notes] nvarchar(500) NULL,
        CONSTRAINT [PK_PurchaseRequestLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseRequestLines_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseRequestLines_PurchaseRequests_PurchaseRequestId] FOREIGN KEY ([PurchaseRequestId]) REFERENCES [crm].[PurchaseRequests] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PurchaseRequestLines_UnitsOfMeasure_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [UnitsOfMeasure] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE TABLE [crm].[RfqSuppliers] (
        [Id] uniqueidentifier NOT NULL,
        [RequestForQuotationId] uniqueidentifier NOT NULL,
        [SupplierId] uniqueidentifier NOT NULL,
        [InvitedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RfqSuppliers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RfqSuppliers_Parties_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Parties] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RfqSuppliers_RequestForQuotations_RequestForQuotationId] FOREIGN KEY ([RequestForQuotationId]) REFERENCES [crm].[RequestForQuotations] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE TABLE [crm].[SupplierQuotations] (
        [Id] uniqueidentifier NOT NULL,
        [QuotationNumber] nvarchar(50) NOT NULL,
        [RequestForQuotationId] uniqueidentifier NULL,
        [SupplierId] uniqueidentifier NOT NULL,
        [QuotationDateUtc] datetime2 NOT NULL,
        [ValidUntilUtc] datetime2 NULL,
        [Currency] nvarchar(10) NOT NULL,
        [ExchangeRate] decimal(18,4) NOT NULL,
        [PaymentTerms] nvarchar(200) NULL,
        [LeadTimeDays] int NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_SupplierQuotations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupplierQuotations_Parties_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Parties] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupplierQuotations_RequestForQuotations_RequestForQuotationId] FOREIGN KEY ([RequestForQuotationId]) REFERENCES [crm].[RequestForQuotations] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE TABLE [crm].[GoodsReceiptLines] (
        [Id] uniqueidentifier NOT NULL,
        [GoodsReceiptId] uniqueidentifier NOT NULL,
        [PurchaseOrderLineId] uniqueidentifier NOT NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [DeliveredQuantity] decimal(18,4) NOT NULL,
        [AcceptedQuantity] decimal(18,4) NOT NULL,
        [RejectedQuantity] decimal(18,4) NOT NULL,
        [UnitId] uniqueidentifier NOT NULL,
        [QcStatus] int NOT NULL,
        [RejectionReason] nvarchar(300) NULL,
        [BatchNumber] nvarchar(100) NULL,
        [ExpiryDateUtc] datetime2 NULL,
        [StorageLocation] nvarchar(100) NULL,
        [Notes] nvarchar(500) NULL,
        CONSTRAINT [PK_GoodsReceiptLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GoodsReceiptLines_GoodsReceipts_GoodsReceiptId] FOREIGN KEY ([GoodsReceiptId]) REFERENCES [crm].[GoodsReceipts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_GoodsReceiptLines_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GoodsReceiptLines_PurchaseOrderLines_PurchaseOrderLineId] FOREIGN KEY ([PurchaseOrderLineId]) REFERENCES [crm].[PurchaseOrderLines] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GoodsReceiptLines_UnitsOfMeasure_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [UnitsOfMeasure] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE TABLE [crm].[SepidarFinancialReconciliations] (
        [Id] uniqueidentifier NOT NULL,
        [SupplierInvoiceId] uniqueidentifier NOT NULL,
        [SepidarInvoiceId] nvarchar(100) NOT NULL,
        [SepidarVoucherNumber] nvarchar(100) NOT NULL,
        [SepidarVoucherDateUtc] datetime2 NOT NULL,
        [SepidarAmount] decimal(18,2) NOT NULL,
        [CandidateConfidenceScore] decimal(5,4) NOT NULL,
        [LinkStatus] int NOT NULL,
        [LinkedBy] nvarchar(100) NULL,
        [LinkedAtUtc] datetime2 NULL,
        [AuditNotes] nvarchar(1000) NULL,
        CONSTRAINT [PK_SepidarFinancialReconciliations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SepidarFinancialReconciliations_SupplierInvoices_SupplierInvoiceId] FOREIGN KEY ([SupplierInvoiceId]) REFERENCES [crm].[SupplierInvoices] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE TABLE [crm].[ThreeWayMatchResults] (
        [Id] uniqueidentifier NOT NULL,
        [PurchaseOrderId] uniqueidentifier NOT NULL,
        [GoodsReceiptId] uniqueidentifier NULL,
        [SupplierInvoiceId] uniqueidentifier NOT NULL,
        [EvaluatedAtUtc] datetime2 NOT NULL,
        [EvaluatedBy] nvarchar(100) NOT NULL,
        [MatchStatus] int NOT NULL,
        [TotalVarianceAmount] decimal(18,2) NOT NULL,
        [TolerancePercentageApplied] decimal(18,4) NOT NULL,
        [IsResolved] bit NOT NULL,
        [ResolutionAction] nvarchar(100) NULL,
        [ResolvedBy] nvarchar(100) NULL,
        [ResolvedAtUtc] datetime2 NULL,
        [ResolutionNotes] nvarchar(1000) NULL,
        CONSTRAINT [PK_ThreeWayMatchResults] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ThreeWayMatchResults_GoodsReceipts_GoodsReceiptId] FOREIGN KEY ([GoodsReceiptId]) REFERENCES [crm].[GoodsReceipts] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_ThreeWayMatchResults_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [crm].[PurchaseOrders] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ThreeWayMatchResults_SupplierInvoices_SupplierInvoiceId] FOREIGN KEY ([SupplierInvoiceId]) REFERENCES [crm].[SupplierInvoices] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE TABLE [crm].[SupplierQuotationLines] (
        [Id] uniqueidentifier NOT NULL,
        [SupplierQuotationId] uniqueidentifier NOT NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [OfferedQuantity] decimal(18,4) NOT NULL,
        [UnitId] uniqueidentifier NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [TaxRate] decimal(18,4) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        [LeadTimeDays] int NULL,
        [Notes] nvarchar(500) NULL,
        CONSTRAINT [PK_SupplierQuotationLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupplierQuotationLines_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupplierQuotationLines_SupplierQuotations_SupplierQuotationId] FOREIGN KEY ([SupplierQuotationId]) REFERENCES [crm].[SupplierQuotations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SupplierQuotationLines_UnitsOfMeasure_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [UnitsOfMeasure] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE TABLE [crm].[SupplierInvoiceLines] (
        [Id] uniqueidentifier NOT NULL,
        [SupplierInvoiceId] uniqueidentifier NOT NULL,
        [PurchaseOrderLineId] uniqueidentifier NULL,
        [GoodsReceiptLineId] uniqueidentifier NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [InvoicedQuantity] decimal(18,4) NOT NULL,
        [UnitId] uniqueidentifier NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        [Description] nvarchar(300) NULL,
        CONSTRAINT [PK_SupplierInvoiceLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupplierInvoiceLines_GoodsReceiptLines_GoodsReceiptLineId] FOREIGN KEY ([GoodsReceiptLineId]) REFERENCES [crm].[GoodsReceiptLines] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupplierInvoiceLines_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupplierInvoiceLines_PurchaseOrderLines_PurchaseOrderLineId] FOREIGN KEY ([PurchaseOrderLineId]) REFERENCES [crm].[PurchaseOrderLines] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupplierInvoiceLines_SupplierInvoices_SupplierInvoiceId] FOREIGN KEY ([SupplierInvoiceId]) REFERENCES [crm].[SupplierInvoices] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SupplierInvoiceLines_UnitsOfMeasure_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [UnitsOfMeasure] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE TABLE [crm].[ThreeWayMatchLineVariances] (
        [Id] uniqueidentifier NOT NULL,
        [ThreeWayMatchResultId] uniqueidentifier NOT NULL,
        [PurchaseOrderLineId] uniqueidentifier NULL,
        [GoodsReceiptLineId] uniqueidentifier NULL,
        [SupplierInvoiceLineId] uniqueidentifier NULL,
        [MaterialId] uniqueidentifier NOT NULL,
        [PoQuantity] decimal(18,4) NOT NULL,
        [ReceivedQuantity] decimal(18,4) NOT NULL,
        [InvoicedQuantity] decimal(18,4) NOT NULL,
        [QuantityVariance] decimal(18,4) NOT NULL,
        [PoUnitPrice] decimal(18,2) NOT NULL,
        [InvoicedUnitPrice] decimal(18,2) NOT NULL,
        [PriceVariance] decimal(18,2) NOT NULL,
        [PriceVariancePercent] decimal(18,4) NOT NULL,
        [PoLineTotal] decimal(18,2) NOT NULL,
        [InvoicedLineTotal] decimal(18,2) NOT NULL,
        [TotalVariance] decimal(18,2) NOT NULL,
        [MismatchType] nvarchar(50) NOT NULL,
        [Notes] nvarchar(500) NULL,
        CONSTRAINT [PK_ThreeWayMatchLineVariances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ThreeWayMatchLineVariances_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ThreeWayMatchLineVariances_ThreeWayMatchResults_ThreeWayMatchResultId] FOREIGN KEY ([ThreeWayMatchResultId]) REFERENCES [crm].[ThreeWayMatchResults] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_GoodsReceiptLines_GoodsReceiptId] ON [crm].[GoodsReceiptLines] ([GoodsReceiptId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_GoodsReceiptLines_MaterialId] ON [crm].[GoodsReceiptLines] ([MaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_GoodsReceiptLines_PurchaseOrderLineId] ON [crm].[GoodsReceiptLines] ([PurchaseOrderLineId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_GoodsReceiptLines_UnitId] ON [crm].[GoodsReceiptLines] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_GoodsReceipts_PurchaseOrderId] ON [crm].[GoodsReceipts] ([PurchaseOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GoodsReceipts_ReceiptNumber] ON [crm].[GoodsReceipts] ([ReceiptNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_GoodsReceipts_WarehouseId] ON [crm].[GoodsReceipts] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderAudits_PurchaseOrderId] ON [crm].[PurchaseOrderAudits] ([PurchaseOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderLines_MaterialId] ON [crm].[PurchaseOrderLines] ([MaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderLines_PurchaseOrderId] ON [crm].[PurchaseOrderLines] ([PurchaseOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderLines_UnitId] ON [crm].[PurchaseOrderLines] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PurchaseOrders_OrderNumber] ON [crm].[PurchaseOrders] ([OrderNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_SupplierId] ON [crm].[PurchaseOrders] ([SupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_WarehouseId] ON [crm].[PurchaseOrders] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_PurchaseRequestLines_MaterialId] ON [crm].[PurchaseRequestLines] ([MaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_PurchaseRequestLines_PurchaseRequestId] ON [crm].[PurchaseRequestLines] ([PurchaseRequestId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_PurchaseRequestLines_UnitId] ON [crm].[PurchaseRequestLines] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PurchaseRequests_RequestNumber] ON [crm].[PurchaseRequests] ([RequestNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RequestForQuotations_RfqNumber] ON [crm].[RequestForQuotations] ([RfqNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_RfqSuppliers_RequestForQuotationId] ON [crm].[RfqSuppliers] ([RequestForQuotationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_RfqSuppliers_SupplierId] ON [crm].[RfqSuppliers] ([SupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_SepidarFinancialReconciliations_SupplierInvoiceId] ON [crm].[SepidarFinancialReconciliations] ([SupplierInvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_SupplierInvoiceLines_GoodsReceiptLineId] ON [crm].[SupplierInvoiceLines] ([GoodsReceiptLineId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_SupplierInvoiceLines_MaterialId] ON [crm].[SupplierInvoiceLines] ([MaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_SupplierInvoiceLines_PurchaseOrderLineId] ON [crm].[SupplierInvoiceLines] ([PurchaseOrderLineId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_SupplierInvoiceLines_SupplierInvoiceId] ON [crm].[SupplierInvoiceLines] ([SupplierInvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_SupplierInvoiceLines_UnitId] ON [crm].[SupplierInvoiceLines] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_SupplierInvoices_PurchaseOrderId] ON [crm].[SupplierInvoices] ([PurchaseOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SupplierInvoices_SupplierId_InvoiceNumber] ON [crm].[SupplierInvoices] ([SupplierId], [InvoiceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_SupplierQuotationLines_MaterialId] ON [crm].[SupplierQuotationLines] ([MaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_SupplierQuotationLines_SupplierQuotationId] ON [crm].[SupplierQuotationLines] ([SupplierQuotationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_SupplierQuotationLines_UnitId] ON [crm].[SupplierQuotationLines] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_SupplierQuotations_RequestForQuotationId] ON [crm].[SupplierQuotations] ([RequestForQuotationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_SupplierQuotations_SupplierId] ON [crm].[SupplierQuotations] ([SupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_ThreeWayMatchLineVariances_MaterialId] ON [crm].[ThreeWayMatchLineVariances] ([MaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_ThreeWayMatchLineVariances_ThreeWayMatchResultId] ON [crm].[ThreeWayMatchLineVariances] ([ThreeWayMatchResultId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_ThreeWayMatchResults_GoodsReceiptId] ON [crm].[ThreeWayMatchResults] ([GoodsReceiptId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_ThreeWayMatchResults_PurchaseOrderId] ON [crm].[ThreeWayMatchResults] ([PurchaseOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    CREATE INDEX [IX_ThreeWayMatchResults_SupplierInvoiceId] ON [crm].[ThreeWayMatchResults] ([SupplierInvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017204424_Phase5_ProcurementAndThreeWayMatching'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261017204424_Phase5_ProcurementAndThreeWayMatching', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE TABLE [crm].[CallRecords] (
        [Id] uniqueidentifier NOT NULL,
        [UniqueId] nvarchar(100) NOT NULL,
        [LinkedId] nvarchar(100) NOT NULL,
        [Direction] int NOT NULL,
        [Status] int NOT NULL,
        [CallerNumber] nvarchar(50) NOT NULL,
        [NormalizedCallerNumber] nvarchar(50) NOT NULL,
        [CallerName] nvarchar(200) NULL,
        [DestinationNumber] nvarchar(50) NOT NULL,
        [Extension] nvarchar(50) NULL,
        [Channel] nvarchar(200) NULL,
        [DestinationChannel] nvarchar(200) NULL,
        [StartTimeUtc] datetime2 NOT NULL,
        [AnswerTimeUtc] datetime2 NULL,
        [EndTimeUtc] datetime2 NULL,
        [DurationSeconds] int NULL,
        [BillableSeconds] int NULL,
        [PartyId] uniqueidentifier NULL,
        [ContactId] uniqueidentifier NULL,
        [IsUnknownCaller] bit NOT NULL,
        [MatchConfidence] nvarchar(20) NOT NULL,
        [RecordingFileName] nvarchar(250) NULL,
        [RecordingFilePath] nvarchar(500) NULL,
        [RecordingDurationSeconds] int NULL,
        [RecordingFileSize] bigint NULL,
        [HasRecording] bit NOT NULL,
        [CdrId] nvarchar(100) NULL,
        [ReconciliationStatus] int NOT NULL,
        [DisconnectionCause] nvarchar(100) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_CallRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CallRecords_Parties_PartyId] FOREIGN KEY ([PartyId]) REFERENCES [Parties] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CallRecords_PartyContacts_ContactId] FOREIGN KEY ([ContactId]) REFERENCES [PartyContacts] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE TABLE [crm].[UserExtensions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Username] nvarchar(100) NOT NULL,
        [Extension] nvarchar(50) NOT NULL,
        [DeviceType] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL,
        [Description] nvarchar(250) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_UserExtensions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserExtensions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE TABLE [crm].[CallAudits] (
        [Id] uniqueidentifier NOT NULL,
        [CallRecordId] uniqueidentifier NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [PerformedBy] nvarchar(100) NOT NULL,
        [TimestampUtc] datetime2 NOT NULL,
        [DetailsJson] nvarchar(4000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_CallAudits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CallAudits_CallRecords_CallRecordId] FOREIGN KEY ([CallRecordId]) REFERENCES [crm].[CallRecords] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE TABLE [crm].[CallNotes] (
        [Id] uniqueidentifier NOT NULL,
        [CallRecordId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Username] nvarchar(100) NOT NULL,
        [NoteText] nvarchar(4000) NOT NULL,
        [Outcome] nvarchar(50) NOT NULL,
        [FollowUpRequired] bit NOT NULL,
        [FollowUpDueDateUtc] datetime2 NULL,
        [FollowUpAssignedTo] nvarchar(100) NULL,
        [FollowUpStatus] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_CallNotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CallNotes_CallRecords_CallRecordId] FOREIGN KEY ([CallRecordId]) REFERENCES [crm].[CallRecords] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallAudits_CallRecordId] ON [crm].[CallAudits] ([CallRecordId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallAudits_TimestampUtc] ON [crm].[CallAudits] ([TimestampUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallNotes_CallRecordId] ON [crm].[CallNotes] ([CallRecordId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallNotes_FollowUpStatus] ON [crm].[CallNotes] ([FollowUpStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallRecords_ContactId] ON [crm].[CallRecords] ([ContactId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallRecords_Extension] ON [crm].[CallRecords] ([Extension]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallRecords_LinkedId] ON [crm].[CallRecords] ([LinkedId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallRecords_NormalizedCallerNumber] ON [crm].[CallRecords] ([NormalizedCallerNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallRecords_PartyId] ON [crm].[CallRecords] ([PartyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallRecords_StartTimeUtc] ON [crm].[CallRecords] ([StartTimeUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_CallRecords_Status] ON [crm].[CallRecords] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CallRecords_UniqueId] ON [crm].[CallRecords] ([UniqueId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserExtensions_Extension] ON [crm].[UserExtensions] ([Extension]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    CREATE INDEX [IX_UserExtensions_UserId] ON [crm].[UserExtensions] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018042000_Phase6_TelephonyAndCallManagement'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261018042000_Phase6_TelephonyAndCallManagement', N'8.0.10');
END;
GO

COMMIT;
GO

