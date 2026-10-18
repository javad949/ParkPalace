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

