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

