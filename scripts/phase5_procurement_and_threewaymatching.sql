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

