BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN
    ALTER TABLE [MaterialUnitConversions] ADD [IsConfirmed] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN
    ALTER TABLE [MaterialUnitConversions] ADD [Notes] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN
    ALTER TABLE [Materials] ADD [CategorizationStatus] nvarchar(50) NOT NULL DEFAULT N'NeedsReview';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN
    ALTER TABLE [Materials] ADD [ConsumptionUnitId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN
    ALTER TABLE [Materials] ADD [IsCustomizedByUser] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN
    ALTER TABLE [Materials] ADD [PackageSize] decimal(18,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN
    ALTER TABLE [Materials] ADD [PackageSizeUnitId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN
    ALTER TABLE [Materials] ADD [Packaging] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN
    ALTER TABLE [Materials] ADD [ProductFamily] nvarchar(150) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN
    ALTER TABLE [Materials] ADD [PurchaseUnitId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN
    ALTER TABLE [Materials] ADD [StandardizationNotes] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN
    CREATE INDEX [IX_Materials_ConsumptionUnitId] ON [Materials] ([ConsumptionUnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN
    CREATE INDEX [IX_Materials_PackageSizeUnitId] ON [Materials] ([PackageSizeUnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN
    CREATE INDEX [IX_Materials_PurchaseUnitId] ON [Materials] ([PurchaseUnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN
    ALTER TABLE [Materials] ADD CONSTRAINT [FK_Materials_UnitsOfMeasure_ConsumptionUnitId] FOREIGN KEY ([ConsumptionUnitId]) REFERENCES [UnitsOfMeasure] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN
    ALTER TABLE [Materials] ADD CONSTRAINT [FK_Materials_UnitsOfMeasure_PackageSizeUnitId] FOREIGN KEY ([PackageSizeUnitId]) REFERENCES [UnitsOfMeasure] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN
    ALTER TABLE [Materials] ADD CONSTRAINT [FK_Materials_UnitsOfMeasure_PurchaseUnitId] FOREIGN KEY ([PurchaseUnitId]) REFERENCES [UnitsOfMeasure] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN

                    UPDATE [MaterialUnitConversions]
                    SET [IsConfirmed] = 0
                    WHERE [Notes] IS NULL OR ([Notes] NOT LIKE N'%تأیید%' AND [Notes] NOT LIKE N'%شاهد%' AND [Notes] NOT LIKE N'%verified%');
                
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261024143247_Phase7_CatalogStandardizationAndDecoupledUnits', N'8.0.10');
END;
GO

COMMIT;
GO

