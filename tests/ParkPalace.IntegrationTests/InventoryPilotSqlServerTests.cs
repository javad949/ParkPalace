using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Services;
using Xunit;

namespace ParkPalace.IntegrationTests;

public sealed class InventoryPilotSqlServerTests : IAsyncLifetime
{
    // Fixed loopback host and unique test-only DB; never loads appsettings or credentials.
    private readonly string databaseName = "PP_CodexPilot_" + Guid.NewGuid().ToString("N");
    private readonly Guid warehouse = Guid.NewGuid(), material = Guid.NewGuid(), unit = Guid.NewGuid();
    private readonly DateTime captured = DateTime.UtcNow;

    private string ConnectionString =>
        $"Server=localhost;Database={databaseName};Integrated Security=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=5";

    private CrmDbContext Context(bool failJournal = false)
    {
        var builder = new DbContextOptionsBuilder<CrmDbContext>().UseSqlServer(ConnectionString);
        if (failJournal) builder.AddInterceptors(new FailJournal());
        return new(builder.Options);
    }

    private PilotManifest Manifest => new("WH-MAIN", warehouse, "1", 10,
        captured.Date.AddDays(-2), captured.Date.AddDays(2), new[] { new PilotItem("1", material, unit, "KG") });

    private PilotSourceCapture Source => new(10, captured,
        new[] { new SepidarStockRecord("1", "1", 200, null, 999, 999, "KG", captured) },
        new[] { new SepidarMovementRecord("REC-10", "10", "1", "1", InventoryMovementType.Receipt,
            100, "KG", captured.AddHours(-1), 4, false, null) });

    public async Task InitializeAsync()
    {
        await using var db = Context();
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync($"ALTER DATABASE [{databaseName}] SET ALLOW_SNAPSHOT_ISOLATION ON");

        // Seed CRM tables
        var category = new Category { Code = "PILOT-TEST", Name = "Test" };
        db.Categories.Add(category);
        db.UnitsOfMeasure.Add(new() { Id = unit, Code = "KG", Name = "kg", Dimension = "Mass", IsActive = true });
        db.Warehouses.Add(new() { Id = warehouse, Code = "WH-MAIN", Name = "Local test", SepidarWarehouseRef = "1", IsActive = true });
        db.Materials.Add(new() { Id = material, InternalCode = "PILOT-TEST-1", Name = "Preserve my name",
            CategoryId = category.Id, BaseUnitId = unit, IsActive = true });
        db.InventoryItemMappings.Add(new() { MaterialId = material, SepidarItemId = "1",
            SepidarItemCode = "1", SepidarItemTitle = "Test", SepidarBaseUnit = "KG", IsActive = true });
        db.InventorySnapshots.Add(new() { MaterialId = material, WarehouseId = warehouse, UnitId = unit,
            OnHandQuantity = 100, ReservedQuantity = 30, IncomingQuantity = 50, ReservableQuantity = 70,
            SnapshotTimeUtc = captured.AddDays(-1), SourceSnapshotKey = "before" });
        await db.SaveChangesAsync();

        // Create Sepidar test schemas and tables in the same isolated DB
        await db.Database.ExecuteSqlRawAsync("IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'FMK') EXEC('CREATE SCHEMA FMK')");
        await db.Database.ExecuteSqlRawAsync("IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'INV') EXEC('CREATE SCHEMA INV')");

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE FMK.FiscalYear (
                FiscalYearId INT PRIMARY KEY,
                Title NVARCHAR(50) NOT NULL,
                StartDate DATETIME2 NOT NULL,
                EndDate DATETIME2 NOT NULL
            );
            CREATE TABLE INV.Stock (
                StockID INT PRIMARY KEY,
                Code NVARCHAR(50) NOT NULL,
                Title NVARCHAR(100) NOT NULL,
                IsActive BIT NOT NULL
            );
            CREATE TABLE INV.Item (
                ItemID INT PRIMARY KEY,
                Code NVARCHAR(50) NOT NULL,
                Title NVARCHAR(100) NOT NULL,
                UnitRef INT NULL,
                IsActive BIT NOT NULL
            );
            CREATE TABLE INV.Unit (
                UnitID INT PRIMARY KEY,
                Title NVARCHAR(100) NULL
            );
            CREATE TABLE INV.ItemStockSummary (
                ItemRef INT NOT NULL,
                StockRef INT NOT NULL,
                FiscalYearRef INT NOT NULL,
                UnitRef INT NULL,
                Quantity DECIMAL(18,4) NOT NULL
            );
            CREATE TABLE INV.InventoryReceipt (
                InventoryReceiptID INT PRIMARY KEY,
                StockRef INT NOT NULL,
                FiscalYearRef INT NOT NULL,
                Number NVARCHAR(50) NOT NULL,
                Date DATETIME2 NOT NULL,
                Type INT NOT NULL,
                IsReturn BIT NOT NULL,
                AccountingVoucherRef INT NULL,
                Description NVARCHAR(200) NULL
            );
            CREATE TABLE INV.InventoryReceiptItem (
                InventoryReceiptItemID INT PRIMARY KEY,
                InventoryReceiptRef INT NOT NULL,
                ItemRef INT NOT NULL,
                Quantity DECIMAL(18,4) NOT NULL
            );
            CREATE TABLE INV.InventoryDelivery (
                InventoryDeliveryID INT PRIMARY KEY,
                StockRef INT NOT NULL,
                FiscalYearRef INT NOT NULL,
                Number NVARCHAR(50) NOT NULL,
                Date DATETIME2 NOT NULL,
                Type INT NOT NULL,
                IsReturn BIT NOT NULL,
                AccountingVoucherRef INT NULL,
                Description NVARCHAR(200) NULL
            );
            CREATE TABLE INV.InventoryDeliveryItem (
                InventoryDeliveryItemID INT PRIMARY KEY,
                InventoryDeliveryRef INT NOT NULL,
                ItemRef INT NOT NULL,
                Quantity DECIMAL(18,4) NOT NULL
            );
        ");

        // Seed Sepidar test data (Fiscal year 1405 has FiscalYearId 15 - dynamically tested, not assumed 10!)
        var start = captured.Date.AddDays(-2);
        var end = captured.Date.AddDays(2);
        await db.Database.ExecuteSqlRawAsync($@"
            INSERT INTO FMK.FiscalYear (FiscalYearId, Title, StartDate, EndDate)
            VALUES (15, N'1405', '{start:yyyy-MM-dd HH:mm:ss}', '{end:yyyy-MM-dd HH:mm:ss}');

            INSERT INTO INV.Stock (StockID, Code, Title, IsActive)
            VALUES (1, N'WH-MAIN', N'انبار مرکزی مواد اولیه', 1);

            INSERT INTO INV.Unit (UnitID, Title) VALUES (1, N'کیلوگرم');

            INSERT INTO INV.Item (ItemID, Code, Title, UnitRef, IsActive)
            VALUES (1, N'PILOT-TEST-1', N'آرد تست', 1, 1);

            INSERT INTO INV.ItemStockSummary (ItemRef, StockRef, FiscalYearRef, UnitRef, Quantity)
            VALUES (1, 1, 15, 1, 250.0000);

            INSERT INTO INV.InventoryReceipt (InventoryReceiptID, StockRef, FiscalYearRef, Number, Date, Type, IsReturn, AccountingVoucherRef, Description)
            VALUES (10, 1, 15, N'REC-10', '{captured.AddHours(-1):yyyy-MM-dd HH:mm:ss}', 1, 0, 1001, N'خرید آزمایشی');

            INSERT INTO INV.InventoryReceiptItem (InventoryReceiptItemID, InventoryReceiptRef, ItemRef, Quantity)
            VALUES (10, 10, 1, 100.0000);
        ");
    }

    public async Task DisposeAsync()
    {
        await using var db = Context();
        // The name is generated locally, never supplied by configuration or the user.
        if (!databaseName.StartsWith("PP_CodexPilot_", StringComparison.Ordinal)) throw new Exception("Invalid cleanup scope");
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task ExecuteAndRollback_PreserveUserEdits_AndRejectDuplicateRun()
    {
        var executor = new InventoryPilotExecutor(() => Context());
        var plan = await executor.PreviewAsync(Manifest, Source);
        var run = Guid.NewGuid();
        var receipt = await executor.ExecuteAsync(Manifest, Source, plan.PlanHash, run, "local-test");
        Assert.Equal(1, receipt.Snapshots);
        Assert.Equal(1, receipt.Movements);
        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(Manifest, Source, plan.PlanHash, run, "local-test"));
        await using (var db = Context())
        {
            var snapshot = await db.InventorySnapshots.SingleAsync();
            Assert.Equal(200m, snapshot.OnHandQuantity);
            Assert.Equal(30m, snapshot.ReservedQuantity);
            Assert.Equal(50m, snapshot.IncomingQuantity);
            snapshot.ReservedQuantity = 75; // another SQL session edits after the pilot
            var item = await db.Materials.SingleAsync();
            item.Name = "User renamed after pilot";
            await db.SaveChangesAsync();
        }
        var rollback = await executor.RollbackAsync(run, "local-test");
        Assert.Equal(1, rollback.Preserved);
        Assert.Equal(1, rollback.Reverted);
        await using var verify = Context();
        Assert.Empty(await verify.InventoryMovements.ToListAsync());
        Assert.Equal(75m, (await verify.InventorySnapshots.SingleAsync()).ReservedQuantity);
        Assert.Equal("User renamed after pilot", (await verify.Materials.SingleAsync()).Name);
        Assert.Empty(await verify.InventorySyncCheckpoints.ToListAsync());
        Assert.Empty(await verify.InventorySyncJobs.ToListAsync());
    }

    [Fact]
    public async Task RollbackWithoutLaterEdits_RestoresSnapshotAndDeletesOnlyOwnMovement()
    {
        var executor = new InventoryPilotExecutor(() => Context());
        var plan = await executor.PreviewAsync(Manifest, Source);
        var run = Guid.NewGuid();
        await executor.ExecuteAsync(Manifest, Source, plan.PlanHash, run, "local-test");
        var result = await executor.RollbackAsync(run, "local-test");
        Assert.Equal(2, result.Reverted);
        Assert.Equal(0, result.Preserved);
        await using var verify = Context();
        var snapshot = await verify.InventorySnapshots.SingleAsync();
        Assert.Equal(100m, snapshot.OnHandQuantity);
        Assert.Equal("before", snapshot.SourceSnapshotKey);
        Assert.Empty(await verify.InventoryMovements.ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.RollbackAsync(run, "local-test"));
    }

    [Fact]
    public async Task ChangedBaselineSincePreview_RejectsWithoutWriting()
    {
        var executor = new InventoryPilotExecutor(() => Context());
        var plan = await executor.PreviewAsync(Manifest, Source);
        await using (var other = Context())
        {
            (await other.InventorySnapshots.SingleAsync()).IncomingQuantity = 80;
            await other.SaveChangesAsync();
        }
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(Manifest, Source, plan.PlanHash, Guid.NewGuid(), "local-test"));
        Assert.Equal("PILOT_PREVIEW_CHANGED", error.Message);
        await using var verify = Context();
        Assert.Equal(100m, (await verify.InventorySnapshots.SingleAsync()).OnHandQuantity);
        Assert.Empty(await verify.InventoryMovements.ToListAsync());
        Assert.Empty(await verify.AuditEvents.ToListAsync());
    }

    [Fact]
    public async Task FailureAfterInventorySave_RollsBackInventoryAndJournalTogether()
    {
        var executor = new InventoryPilotExecutor(() => Context(true));
        var plan = await executor.PreviewAsync(Manifest, Source);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(Manifest, Source, plan.PlanHash, Guid.NewGuid(), "local-test"));
        await using var verify = Context();
        var snapshot = await verify.InventorySnapshots.SingleAsync();
        Assert.Equal(100m, snapshot.OnHandQuantity);
        Assert.Equal("before", snapshot.SourceSnapshotKey);
        Assert.Empty(await verify.InventoryMovements.ToListAsync());
        Assert.Empty(await verify.AuditEvents.ToListAsync());
    }

    [Fact]
    public async Task TrulyConcurrentSessions_OneWins_SecondRejectsOrAborts_LeavingZeroCorruption()
    {
        var executor1 = new InventoryPilotExecutor(() => Context());
        var executor2 = new InventoryPilotExecutor(() => Context());
        var plan = await executor1.PreviewAsync(Manifest, Source);

        var run1 = Guid.NewGuid();
        var run2 = Guid.NewGuid();

        // Launch two simultaneous execution attempts
        var task1 = Task.Run(() => executor1.ExecuteAsync(Manifest, Source, plan.PlanHash, run1, "concurrent-1"));
        var task2 = Task.Run(() => executor2.ExecuteAsync(Manifest, Source, plan.PlanHash, run2, "concurrent-2"));

        var results = await Task.WhenAll(
            task1.ContinueWith(t => (Success: t.IsCompletedSuccessfully, Error: t.Exception?.InnerException?.Message)),
            task2.ContinueWith(t => (Success: t.IsCompletedSuccessfully, Error: t.Exception?.InnerException?.Message)));

        // Exactly one should succeed, or if deadlock resolution aborted both, neither corrupts.
        int successes = results.Count(r => r.Success);
        Assert.True(successes <= 1, "At most one concurrent transaction may succeed under Serializable isolation");

        // Verify database state has zero corruptions or partial journals
        await using var verify = Context();
        var auditRuns = await verify.AuditEvents.Where(a => a.EntityType == "InventoryPilotRun").ToListAsync();
        Assert.Equal(successes, auditRuns.Count);

        var movements = await verify.InventoryMovements.ToListAsync();
        Assert.Equal(successes, movements.Count);

        var snapshot = await verify.InventorySnapshots.SingleAsync();
        if (successes == 1)
        {
            Assert.Equal(200m, snapshot.OnHandQuantity);
        }
        else
        {
            Assert.Equal(100m, snapshot.OnHandQuantity);
        }
    }

    [Fact]
    public async Task CancellationDuringExecution_AbortsTransaction_LeavesZeroChangesOrJournal()
    {
        var executor = new InventoryPilotExecutor(() => Context());
        var plan = await executor.PreviewAsync(Manifest, Source);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled token

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(Manifest, Source, plan.PlanHash, Guid.NewGuid(), "cancelled-actor", cts.Token));

        await using var verify = Context();
        var snapshot = await verify.InventorySnapshots.SingleAsync();
        Assert.Equal(100m, snapshot.OnHandQuantity);
        Assert.Empty(await verify.InventoryMovements.ToListAsync());
        Assert.Empty(await verify.AuditEvents.ToListAsync());
    }

    [Fact]
    public async Task CancellationDuringRollback_AbortsTransaction_LeavesDataConsistentForRetry()
    {
        var executor = new InventoryPilotExecutor(() => Context());
        var plan = await executor.PreviewAsync(Manifest, Source);
        var run = Guid.NewGuid();
        await executor.ExecuteAsync(Manifest, Source, plan.PlanHash, run, "local-test");

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            executor.RollbackAsync(run, "local-test", cts.Token));

        // State remains intact after aborted rollback
        await using (var verify = Context())
        {
            var snapshot = await verify.InventorySnapshots.SingleAsync();
            Assert.Equal(200m, snapshot.OnHandQuantity);
            Assert.Single(await verify.InventoryMovements.ToListAsync());
            // No rollback audit event recorded
            Assert.DoesNotContain(await verify.AuditEvents.ToListAsync(), a => a.Action == "InventoryPilot_RolledBack");
        }

        // Retry rollback cleanly without cancellation
        var retryReceipt = await executor.RollbackAsync(run, "local-test");
        Assert.Equal(2, retryReceipt.Reverted);

        await using (var verify = Context())
        {
            var snapshot = await verify.InventorySnapshots.SingleAsync();
            Assert.Equal(100m, snapshot.OnHandQuantity);
            Assert.Empty(await verify.InventoryMovements.ToListAsync());
            Assert.Contains(await verify.AuditEvents.ToListAsync(), a => a.Action == "InventoryPilot_RolledBack");
        }
    }

    [Fact]
    public async Task SepidarPilotReader_OnSqlServer_ValidatesFiscalYearAndConsistentCapture()
    {
        var reader = new SepidarPilotReader(ConnectionString);

        // 1. Success case: Title 1405 exists, resolves to ID 15 (not 10!), warehouse 1 exists
        var result = await reader.CaptureAsync("1", "1405");
        Assert.True(result.IsConsistent, result.FailureReason);
        Assert.Null(result.FailureReason);
        Assert.NotNull(result.FiscalYear);
        Assert.Equal(15, result.FiscalYear.FiscalYearId); // Dynamically verified from DB!
        Assert.Equal("1405", result.FiscalYear.Title);
        Assert.Equal(1, result.StockCount);
        Assert.Equal(1, result.MovementCount);

        var stock = Assert.Single(result.Capture!.Stocks);
        Assert.Equal("1", stock.ItemRef);
        Assert.Equal(250m, stock.OnHand);
        Assert.Equal("KG", stock.UnitCode);

        var mov = Assert.Single(result.Capture.Movements);
        Assert.Equal("REC-10", mov.VoucherRef);
        Assert.Equal(100m, mov.Quantity);

        // 2. Failure case: Unknown fiscal year
        var unknownResult = await reader.CaptureAsync("1", "1406");
        Assert.False(unknownResult.IsConsistent);
        Assert.Contains("PILOT_FISCAL_YEAR_NOT_FOUND", unknownResult.FailureReason);

        // 3. Failure case: Inactive warehouse
        await using (var db = Context())
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE INV.Stock SET IsActive = 0 WHERE StockID = 1");
        }
        var inactiveWhResult = await reader.CaptureAsync("1", "1405");
        Assert.False(inactiveWhResult.IsConsistent);
        Assert.Contains("PILOT_SOURCE_WAREHOUSE_INVALID", inactiveWhResult.FailureReason);
    }

    [Fact]
    public async Task InventoryPilotPreviewTool_OnSqlServer_RunsPreviewWithoutWriting()
    {
        // Re-enable warehouse 1 if modified
        await using (var db = Context())
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE INV.Stock SET IsActive = 1 WHERE StockID = 1");
        }

        var reader = new SepidarPilotReader(ConnectionString);
        await using var crmDb = Context();

        var testDir = Path.Combine(Path.GetTempPath(), "PP_Test_" + Guid.NewGuid().ToString("N"));
        var options = new PilotPreviewOptions(
            WarehouseCode: "WH-MAIN",
            SourceWarehouseId: "1",
            FiscalYearTitle: "1405",
            OutputJsonPath: Path.Combine(testDir, "test_output.json"),
            OutputReportPath: Path.Combine(testDir, "test_report.md"));

        var result = await InventoryPilotPreviewTool.GeneratePreviewAsync(crmDb, reader, options);

        // Live reader (SepidarPilotReader) must ALWAYS remain IsExecutable = false!
        Assert.False(result.IsExecutable);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("PILOT_SEMANTIC_CONTRACT_UNVERIFIED", result.FailureReason);
        Assert.NotNull(result.ManifestHash);
        Assert.NotNull(result.PlanHash);
        Assert.Equal(1, result.ManifestItemCount);
        Assert.Equal(1, result.SnapshotModifiedCount);
        Assert.Contains("UNEXECUTABLE", result.HumanReadableReport);
        Assert.Contains("UNEXECUTABLE_SEMANTIC_CONTRACT_UNVERIFIED", result.JsonOutput);

        // Verify strictly ZERO writes to the CRM database
        await using var verify = Context();
        var snapshot = await verify.InventorySnapshots.SingleAsync();
        Assert.Equal(100m, snapshot.OnHandQuantity); // Still 100m, NOT 250m!
        Assert.Empty(await verify.InventoryMovements.ToListAsync());
        Assert.Empty(await verify.AuditEvents.ToListAsync());
    }

    [Fact]
    public async Task InventoryPilotPreviewTool_LiveConnectionWithoutSemanticVerification_IsMarkedUnexecutable()
    {
        var reader = new SepidarPilotReader(ConnectionString);
        await using var crmDb = Context();

        var options = new PilotPreviewOptions(
            WarehouseCode: "WH-MAIN",
            SourceWarehouseId: "1",
            FiscalYearTitle: "1405",
            DataProvenance: "LIVE_READONLY_CONNECTION",
            SemanticContractVerified: false);

        var result = await InventoryPilotPreviewTool.GeneratePreviewAsync(crmDb, reader, options);

        // Transactional consistency is verified, but semantic contract is unverified!
        Assert.False(result.IsExecutable);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("PILOT_SEMANTIC_CONTRACT_UNVERIFIED", result.FailureReason);
        Assert.Contains("UNEXECUTABLE", result.HumanReadableReport);
        Assert.Contains("UNEXECUTABLE_SEMANTIC_CONTRACT_UNVERIFIED", result.JsonOutput);
    }

    [Fact]
    public async Task InventoryPilotPreviewTool_LiveConnectionWithVerifySemanticContractSwitch_CannotBypassUnexecutableConstraint()
    {
        var reader = new SepidarPilotReader(ConnectionString);
        await using var crmDb = Context();

        // Attempting to pass SemanticContractVerified = true on a live database reader
        var options = new PilotPreviewOptions(
            WarehouseCode: "WH-MAIN",
            SourceWarehouseId: "1",
            FiscalYearTitle: "1405",
            DataProvenance: "LIVE_READONLY_CONNECTION",
            SemanticContractVerified: true);

        var result = await InventoryPilotPreviewTool.GeneratePreviewAsync(crmDb, reader, options);

        // MUST REMAIN FALSE: switch cannot bypass unverified contract!
        Assert.False(result.IsExecutable);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("PILOT_SEMANTIC_CONTRACT_UNVERIFIED", result.FailureReason);
        Assert.Contains("ارسال سوئیچ --verify-semantic-contract نمی‌تواند این محدودیت را دور بزند", result.FailureReason);
    }

    [Fact]
    public async Task InventoryPilotPreviewTool_LiveConnectionWithTamperedSimulatedProvenance_CannotBypassUnexecutableConstraint()
    {
        var reader = new SepidarPilotReader(ConnectionString);
        await using var crmDb = Context();

        // Attempting to pass DataProvenance = "SIMULATED_TEST" while reading from real SepidarPilotReader
        var options = new PilotPreviewOptions(
            WarehouseCode: "WH-MAIN",
            SourceWarehouseId: "1",
            FiscalYearTitle: "1405",
            DataProvenance: "SIMULATED_TEST",
            SemanticContractVerified: true);

        var result = await InventoryPilotPreviewTool.GeneratePreviewAsync(crmDb, reader, options);

        // MUST REMAIN FALSE: tampering with provenance tag cannot bypass live database safety!
        Assert.False(result.IsExecutable);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("PILOT_SEMANTIC_CONTRACT_UNVERIFIED", result.FailureReason);
        Assert.Contains("تغییر برچسب منشأ داده‌ها به 'SIMULATED_TEST' یا ارسال سوئیچ اعتبارسنجی نمی‌تواند محدودیت ایمنی قرارداد اثبات‌نشده سپیدار را دور بزند", result.FailureReason);
    }

    [Fact]
    public async Task SepidarPilotReader_ConcurrentWriter_IsolatedBySnapshotTransaction()
    {
        var reader = new SepidarPilotReader(ConnectionString)
        {
            DesiredIsolationLevel = System.Data.IsolationLevel.Snapshot
        };

        // Hook between reading stocks and reading movements
        reader.OnAfterStockReadAsync = async () =>
        {
            // Simulate a concurrent writer (e.g. ERP user) inserting a new receipt on a separate connection
            await using var writerConn = new SqlConnection(ConnectionString);
            await writerConn.OpenAsync();
            await using var cmd = writerConn.CreateCommand();
            cmd.CommandText = $@"
                INSERT INTO INV.InventoryReceipt (InventoryReceiptID, StockRef, FiscalYearRef, Number, Date, Type, IsReturn, AccountingVoucherRef, Description)
                VALUES (99, 1, 15, N'REC-99', '{captured.AddHours(-1):yyyy-MM-dd HH:mm:ss}', 1, 0, 1002, N'رسید همزمان بعد از خواندن موجودی');

                INSERT INTO INV.InventoryReceiptItem (InventoryReceiptItemID, InventoryReceiptRef, ItemRef, Quantity)
                VALUES (99, 99, 1, 50.0000);
            ";
            await cmd.ExecuteNonQueryAsync();
        };

        var result = await reader.CaptureAsync("1", "1405");
        Assert.True(result.IsConsistent, result.FailureReason);
        Assert.Equal(1, result.StockCount);

        // Under snapshot isolation, the reader MUST NOT see the concurrent insert of REC-99
        // because its point-in-time snapshot was locked at transaction start time!
        Assert.Equal(1, result.MovementCount);
        var mov = Assert.Single(result.Capture!.Movements);
        Assert.Equal("REC-10", mov.VoucherRef);
        Assert.DoesNotContain(result.Capture.Movements, m => m.VoucherRef == "REC-99");

        // Verify with a separate connection that REC-99 was indeed written to the DB
        await using var verifyConn = new SqlConnection(ConnectionString);
        await verifyConn.OpenAsync();
        await using var verifyCmd = verifyConn.CreateCommand();
        verifyCmd.CommandText = "SELECT COUNT(*) FROM INV.InventoryReceipt WHERE InventoryReceiptID = 99";
        var count = (int)(await verifyCmd.ExecuteScalarAsync())!;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SepidarPilotReader_MissingUnitTitle_And_UnitRef1WithNonKgTitle_EvaluatedAndExcluded()
    {
        await using (var db = Context())
        {
            await db.Database.ExecuteSqlRawAsync(@"
                -- Unit without title (NULL)
                INSERT INTO INV.Unit (UnitID, Title) VALUES (2, NULL);
                INSERT INTO INV.Item (ItemID, Code, Title, UnitRef, IsActive) VALUES (2, N'ITEM-NO-TITLE', N'کالای بدون عنوان واحد', 2, 1);
                INSERT INTO INV.ItemStockSummary (ItemRef, StockRef, FiscalYearRef, UnitRef, Quantity) VALUES (2, 1, 15, 2, 10.0000);

                -- Unit 3 with title 'بسته' (non-KG)
                INSERT INTO INV.Unit (UnitID, Title) VALUES (3, N'بسته');
                INSERT INTO INV.Item (ItemID, Code, Title, UnitRef, IsActive) VALUES (3, N'ITEM-BOX', N'کالای بسته‌ای', 3, 1);
                INSERT INTO INV.ItemStockSummary (ItemRef, StockRef, FiscalYearRef, UnitRef, Quantity) VALUES (3, 1, 15, 3, 20.0000);
            ");
        }

        var reader = new SepidarPilotReader(ConnectionString);
        var result = await reader.CaptureAsync("1", "1405");
        Assert.True(result.IsConsistent, result.FailureReason);

        var noTitleStock = result.Capture!.Stocks.FirstOrDefault(s => s.ItemRef == "2");
        Assert.NotNull(noTitleStock);
        Assert.Equal("UNKNOWN_UNIT", noTitleStock.UnitCode); // Must evaluate to UNKNOWN_UNIT, never fallback to KG!

        var boxStock = result.Capture.Stocks.FirstOrDefault(s => s.ItemRef == "3");
        Assert.NotNull(boxStock);
        Assert.Equal("بسته", boxStock.UnitCode); // Must evaluate to 'بسته', never assume KG!
    }

    [Fact]
    public async Task SepidarPilotReader_WhenSnapshotIsolationDisabled_ReturnsUnexecutableWithoutAlteringSettings()
    {
        var noSnapDbName = "PP_CodexPilot_NoSnap_" + Guid.NewGuid().ToString("N");
        var masterConn = "Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;";
        await using var conn = new SqlConnection(masterConn);
        await conn.OpenAsync();
        await using (var createCmd = conn.CreateCommand())
        {
            createCmd.CommandText = $"CREATE DATABASE [{noSnapDbName}]";
            await createCmd.ExecuteNonQueryAsync();
        }

        try
        {
            var noSnapConnStr = $"Server=localhost;Database={noSnapDbName};Integrated Security=True;TrustServerCertificate=True;";
            await using (var initConn = new SqlConnection(noSnapConnStr))
            {
                await initConn.OpenAsync();
                await using var initCmd = initConn.CreateCommand();
                initCmd.CommandText = @"
                    EXEC('CREATE SCHEMA FMK');
                    CREATE TABLE FMK.FiscalYear (
                        FiscalYearId INT PRIMARY KEY,
                        Title NVARCHAR(50) NOT NULL,
                        StartDate DATETIME2 NOT NULL,
                        EndDate DATETIME2 NOT NULL
                    );
                    INSERT INTO FMK.FiscalYear VALUES (15, N'1405', '2026-03-21', '2027-03-21');
                ";
                await initCmd.ExecuteNonQueryAsync();
            }

            var reader = new SepidarPilotReader(noSnapConnStr)
            {
                DesiredIsolationLevel = System.Data.IsolationLevel.Snapshot
            };

            var result = await reader.CaptureAsync("1", "1405");
            Assert.False(result.IsConsistent);
            Assert.Contains("PILOT_CONSISTENCY_PREREQUISITE_MISSING", result.FailureReason);

            // Verify database setting was NOT mutated
            await using var verifyCmd = conn.CreateCommand();
            verifyCmd.CommandText = $"SELECT snapshot_isolation_state FROM sys.databases WHERE name = '{noSnapDbName}'";
            var snapState = Convert.ToByte(await verifyCmd.ExecuteScalarAsync());
            Assert.Equal(0, snapState); // Still 0 (OFF)! Reader never altered database settings!
        }
        finally
        {
            await using var dropCmd = conn.CreateCommand();
            dropCmd.CommandText = $"ALTER DATABASE [{noSnapDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{noSnapDbName}]";
            await dropCmd.ExecuteNonQueryAsync();
        }
    }

    private sealed class FailJournal : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (data.Context!.ChangeTracker.Entries<AuditEvent>().Any(e => e.State == EntityState.Added && e.Entity.Action == "InventoryPilot_Applied"))
                throw new InvalidOperationException("Injected failure after inventory save");
            return base.SavingChangesAsync(data, result, cancellationToken);
        }
    }
}
