using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Services;
using Xunit;
using Xunit.Abstractions;

namespace ParkPalace.IntegrationTests;

public class PurchaseWorkflowSqlServerTests : IDisposable
{
    private readonly string _dbName;
    private readonly string _connectionString;
    private readonly ITestOutputHelper _output;
    private readonly CrmDbContext _dbContext;

    public PurchaseWorkflowSqlServerTests(ITestOutputHelper output)
    {
        _output = output;
        _dbName = $"PP_Test_{Guid.NewGuid():N}".Substring(0, 22);
        _connectionString = $"Server=localhost;Database={_dbName};Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        _dbContext = new CrmDbContext(options);

        // Ensure clean unique isolated test database on local SQL Server instance
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        try
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
        }
        catch
        {
            // Ignore cleanup failure in test dispose
        }
    }

    private CrmDbContext CreateNewDbContext()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer(_connectionString)
            .Options;
        return new CrmDbContext(options);
    }

    private async Task<(Guid materialId, Guid unitId, Guid supplierId, Guid warehouseId)> SeedPrerequisitesAsync(CrmDbContext db)
    {
        var unitId = Guid.NewGuid();
        var unit = new UnitOfMeasure
        {
            Id = unitId,
            Code = "KG",
            Name = "کیلوگرم",
            Precision = 2,
            Dimension = "Mass",
            IsActive = true
        };
        db.UnitsOfMeasure.Add(unit);

        var warehouseId = Guid.NewGuid();
        var warehouse = new Warehouse
        {
            Id = warehouseId,
            Code = "WH-SQL-TEST-01",
            Name = "انبار مرکزی تست ایزوله SQL Server",
            IsActive = true
        };
        db.Warehouses.Add(warehouse);

        var categoryId = Guid.NewGuid();
        var category = new Category
        {
            Id = categoryId,
            Code = "CAT-SQL-RAW",
            Name = "مواد اولیه و غلات صنعتی",
            IsActive = true
        };
        db.Categories.Add(category);

        var materialId = Guid.NewGuid();
        var material = new Material
        {
            Id = materialId,
            InternalCode = "MAT-SQL-WHEAT-01",
            Name = "گندم آسیاب‌شده درجه یک بهاره",
            CategoryId = categoryId,
            BaseUnitId = unitId,
            SafetyStock = 100m,
            MinStock = 500m,
            IsActive = true
        };
        db.Materials.Add(material);

        var supplierId = Guid.NewGuid();
        var supplier = new Party
        {
            Id = supplierId,
            DisplayName = "شرکت کشت و صنعت غلات سحر البرز (سهامی خاص)",
            LegalName = "شرکت توسعه کشت و فرآوری غلات سحر نوین",
            NationalId = "14009876543",
            EconomicCode = "411987654321",
            IsActive = true
        };
        db.Parties.Add(supplier);

        await db.SaveChangesAsync();
        return (materialId, unitId, supplierId, warehouseId);
    }

    [Fact]
    public async Task ConcurrentIssuanceWithSameToken_OnIsolatedSqlServer_ExactlyOneSucceedsAndOneFailsWithControlledConflict()
    {
        _output.WriteLine("================================================================================");
        _output.WriteLine(" ATOMIC CONCURRENCY TEST: TWO CONCURRENT ISSUANCE REQUESTS WITH SAME TOKEN");
        _output.WriteLine($" Engine: Microsoft SQL Server 2019 (Localhost Instance) | DB: {_dbName}");
        _output.WriteLine("================================================================================");

        var (materialId, unitId, supplierId, warehouseId) = await SeedPrerequisitesAsync(_dbContext);

        // 1. Create a Draft Purchase Order
        var poServiceInit = new PurchaseOrderService(_dbContext);
        var createPayload = new CreatePurchaseOrderRequest
        {
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            Notes = "سفارش آزمایشی برای ارزیابی همزمانی واقعی در پایگاه داده SQL Server",
            DirectIssue = false,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new()
                {
                    MaterialId = materialId,
                    UnitId = unitId,
                    OrderedQuantity = 500m,
                    UnitPrice = 200000m,
                    TaxRate = 0.10m
                }
            }
        };

        var initialPo = await poServiceInit.CreateOrderAsync(createPayload, "sahar\\procurement");
        initialPo.Status.Should().Be("Draft");
        var initialToken = initialPo.ConcurrencyToken;
        var poId = initialPo.Id;

        _output.WriteLine($"[سفارش اولیه ایجاد شد]");
        _output.WriteLine($"  شناسه: {poId}");
        _output.WriteLine($"  توکن همزمانی اولیه (Initial Token): {initialToken}");

        // 2. Prepare Two Separate DbContext Instances and Services to simulate two concurrent HTTP threads
        using var contextThreadA = CreateNewDbContext();
        using var contextThreadB = CreateNewDbContext();

        var serviceThreadA = new PurchaseOrderService(contextThreadA);
        var serviceThreadB = new PurchaseOrderService(contextThreadB);

        _output.WriteLine("\n[اجرای همزمان دو درخواست صدور با توکن اولیه یکسان...]");

        PurchaseOrderDto? successResult = null;
        Exception? threadAException = null;
        Exception? threadBException = null;

        // Run both asynchronously in parallel
        var taskA = Task.Run(async () =>
        {
            try
            {
                var res = await serviceThreadA.IssueOrderAsync(poId, "sahar\\procurement-A", initialToken);
                successResult = res;
            }
            catch (Exception ex)
            {
                threadAException = ex;
            }
        });

        var taskB = Task.Run(async () =>
        {
            try
            {
                var res = await serviceThreadB.IssueOrderAsync(poId, "sahar\\procurement-B", initialToken);
                successResult = res;
            }
            catch (Exception ex)
            {
                threadBException = ex;
            }
        });

        await Task.WhenAll(taskA, taskB);

        // 3. Evaluate results: EXACTLY ONE SUCCESS, EXACTLY ONE CONFLICT!
        var successCount = (threadAException == null ? 1 : 0) + (threadBException == null ? 1 : 0);
        var conflictCount = (threadAException is DbUpdateConcurrencyException or InvalidOperationException ? 1 : 0) + 
                            (threadBException is DbUpdateConcurrencyException or InvalidOperationException ? 1 : 0);

        _output.WriteLine($"  تعداد درخواست‌های موفق (Status 200 OK): {successCount}");
        _output.WriteLine($"  تعداد تعارض‌های کنترل‌شده (DbUpdateConcurrencyException / Status 409): {conflictCount}");

        if (threadAException != null)
        {
            _output.WriteLine($"  Thread A با تعارض مسدود شد: {threadAException.GetType().Name} - {threadAException.Message}");
        }
        else
        {
            _output.WriteLine($"  Thread A با موفقیت انجام شد.");
        }

        if (threadBException != null)
        {
            _output.WriteLine($"  Thread B با تعارض مسدود شد: {threadBException.GetType().Name} - {threadBException.Message}");
        }
        else
        {
            _output.WriteLine($"  Thread B با موفقیت انجام شد.");
        }

        successCount.Should().Be(1, "دقیقاً یک درخواست از دو درخواست همزمان باید موفق شود");
        conflictCount.Should().Be(1, "درخواست دوم باید با تعارض کنترل‌شده (DbUpdateConcurrencyException) مسدود شود");

        // 4. Verify Database State in a fresh context: NO DUPLICATE AUDITS OR BUSINESS EFFECTS
        using var verificationContext = CreateNewDbContext();
        var poInDb = await verificationContext.PurchaseOrders
            .Include(p => p.Audits)
            .Include(p => p.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == poId);

        poInDb.Should().NotBeNull();
        poInDb!.Status.Should().Be(PurchaseOrderStatus.Issued, "وضعیت نهایی سفارش باید صادر شده (Issued) باشد");
        poInDb.ConcurrencyToken.Should().NotBe(initialToken, "توکن همزمانی باید پس از صدور موفق به مقدار جدید تغییر کرده باشد");

        // Exactly 2 audits: 1 for Created, 1 for DirectlyIssuedByProcurement. ZERO duplicates!
        poInDb.Audits.Should().HaveCount(2, "نباید هیچ رکورد بازرسی (Audit) تکراری ایجاد شده باشد");
        poInDb.Audits.Select(a => a.Action).Should().BeEquivalentTo(new[] { "Created", "DirectlyIssuedByProcurement" });

        _output.WriteLine($"\n[تایید وضعیت پایگاه‌داده SQL Server]");
        _output.WriteLine($"  وضعیت نهایی سفارش: {poInDb.Status}");
        _output.WriteLine($"  توکن نهایی: {poInDb.ConcurrencyToken}");
        _output.WriteLine($"  تعداد رکوردهای بازرسی (Audit): {poInDb.Audits.Count}");
        foreach (var audit in poInDb.Audits)
        {
            _output.WriteLine($"    - اکشن: {audit.Action} | ثبت‌کننده: {audit.PerformedBy} | زمان: {audit.TimestampUtc:HH:mm:ss.fff}");
        }

        _output.WriteLine("\n================================================================================");
        _output.WriteLine(" آزمون حفاظت اتمیک همزمانی در SQL Server با موفقیت ۱۰۰٪ تایید شد");
        _output.WriteLine("================================================================================");
    }

    [Fact]
    public async Task CancellationBehavior_OnIsolatedSqlServer_AllRulesPreserved()
    {
        _output.WriteLine("================================================================================");
        _output.WriteLine($" CANCELLATION BEHAVIOR TEST | DB: {_dbName}");
        _output.WriteLine("================================================================================");

        var (materialId, unitId, supplierId, warehouseId) = await SeedPrerequisitesAsync(_dbContext);
        var poService = new PurchaseOrderService(_dbContext);
        var grService = new GoodsReceiptService(_dbContext);

        // 1. Create Draft PO
        var createPayload = new CreatePurchaseOrderRequest
        {
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            Notes = "سفارش آزمایشی برای اعتبارسنجی فرآیند ابطال",
            DirectIssue = false,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { MaterialId = materialId, UnitId = unitId, OrderedQuantity = 100m, UnitPrice = 50000m }
            }
        };

        var po = await poService.CreateOrderAsync(createPayload, "sahar\\procurement");

        // 2. Cancellation with invalid / stale token must fail with DbUpdateConcurrencyException
        var staleCancelReq = new CancelPurchaseOrderRequest { Reason = "دلیل لغو تست", ConcurrencyToken = "INVALID_TOKEN" };
        var actStaleCancel = () => poService.CancelOrderAsync(po.Id, staleCancelReq, "sahar\\procurement");
        await actStaleCancel.Should().ThrowAsync<DbUpdateConcurrencyException>("ابطال با توکن ناسازگار باید مسدود شود");
        _output.WriteLine("  [ابطال ۱] تلاش ابطال با توکن نامعتبر با DbUpdateConcurrencyException مسدود شد.");

        // 3. Successful Cancellation of Draft PO
        var validCancelReq = new CancelPurchaseOrderRequest { Reason = "تغییر برنامه تولید و عدم نیاز به ماده اولیه", ConcurrencyToken = po.ConcurrencyToken };
        var cancelledPo = await poService.CancelOrderAsync(po.Id, validCancelReq, "sahar\\procurement");

        cancelledPo.Status.Should().Be("Cancelled");
        cancelledPo.Audits.Should().Contain(a => a.Action == "Cancelled" && a.PerformedBy == "sahar\\procurement");

        var cancelledEntityInDb = await _dbContext.PurchaseOrders.AsNoTracking().FirstOrDefaultAsync(p => p.Id == po.Id);
        cancelledEntityInDb.Should().NotBeNull();
        cancelledEntityInDb!.CancelledBy.Should().Be("sahar\\procurement");
        cancelledEntityInDb.CancellationReason.Should().Be("تغییر برنامه تولید و عدم نیاز به ماده اولیه");
        cancelledEntityInDb.CancelledAtUtc.Should().NotBeNull();
        _output.WriteLine("  [ابطال ۲] ابطال سفارش پیش‌نویس با موفقیت ثبت شد.");

        // 4. Repeated cancellation on already cancelled PO must fail
        var repeatCancelReq = new CancelPurchaseOrderRequest { Reason = "تلاش مجدد", ConcurrencyToken = cancelledPo.ConcurrencyToken };
        var actRepeatCancel = () => poService.CancelOrderAsync(po.Id, repeatCancelReq, "sahar\\procurement");
        var exRepeat = await actRepeatCancel.Should().ThrowAsync<InvalidOperationException>();
        exRepeat.WithMessage("*سفارش پیش از این ابطال شده است*");
        _output.WriteLine($"  [ابطال ۳] تلاش ابطال مجدد مسدود شد: '{exRepeat.Which.Message}'");

        // 5. Cancellation of PO with Accepted Goods Receipts must be blocked
        var po2 = await poService.CreateOrderAsync(createPayload, "sahar\\procurement");
        var issuedPo2 = await poService.IssueOrderAsync(po2.Id, "sahar\\procurement", po2.ConcurrencyToken);

        // Receive goods
        var receiptReq = new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = issuedPo2.Id,
            WarehouseId = warehouseId,
            Lines = new List<CreateGoodsReceiptLineRequest>
            {
                new()
                {
                    PurchaseOrderLineId = issuedPo2.Lines[0].Id,
                    DeliveredQuantity = 50m,
                    AcceptedQuantity = 50m,
                    QcStatus = "Passed"
                }
            }
        };
        await grService.CreateReceiptAsync(receiptReq, "sahar\\warehouse");

        // Now attempt cancellation on PO with accepted receipt
        var cancelPoWithReceiptReq = new CancelPurchaseOrderRequest { Reason = "ابطال پس از تحویل کالا", ConcurrencyToken = issuedPo2.ConcurrencyToken };
        var actCancelWithReceipt = () => poService.CancelOrderAsync(po2.Id, cancelPoWithReceiptReq, "sahar\\procurement");
        var exReceipt = await actCancelWithReceipt.Should().ThrowAsync<InvalidOperationException>();
        exReceipt.WithMessage("*به دلیل وجود رسید انبار پذیرفته‌شده برای این سفارش، امکان ابطال مستقیم سفارش خرید وجود ندارد*");
        _output.WriteLine($"  [ابطال ۴] ابطال سفارش دارای رسید پذیرفته‌شده مسدود شد: '{exReceipt.Which.Message}'");

        _output.WriteLine("\n================================================================================");
        _output.WriteLine(" کلیه قوانین ابطال سفارش خرید در SQL Server با موفقیت ۱۰۰٪ پاس شدند");
        _output.WriteLine("================================================================================");
    }

    [Fact]
    public async Task ExecuteFullPurchaseWorkflow_OnIsolatedSqlServer_AllAssertionsPass()
    {
        _output.WriteLine("================================================================================");
        _output.WriteLine($" ISOLATED SQL SERVER TEST: PURCHASE WORKFLOW | DB: {_dbName}");
        _output.WriteLine("================================================================================");

        var (materialId, unitId, supplierId, warehouseId) = await SeedPrerequisitesAsync(_dbContext);

        var poService = new PurchaseOrderService(_dbContext);
        var grService = new GoodsReceiptService(_dbContext);

        // 1. CREATE DRAFT PURCHASE ORDER
        var createPayload = new CreatePurchaseOrderRequest
        {
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            ExpectedDeliveryDateUtc = DateTime.UtcNow.AddDays(7),
            PaymentTerms = "چک صیادی ۴۵ روزه با ضمانت بانکی",
            DeliveryTerms = "تحویل انبار کارخانه با بارنامه رسمی",
            Notes = "سفارش آزمایشی اعتبارسنجی SQL Server مستقل",
            DirectIssue = false,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new()
                {
                    MaterialId = materialId,
                    UnitId = unitId,
                    OrderedQuantity = 1000m,
                    UnitPrice = 180000m,
                    TaxRate = 0.10m,
                    DiscountAmount = 5000000m
                }
            }
        };

        var createdPo = await poService.CreateOrderAsync(createPayload, "sahar\\procurement");

        createdPo.Should().NotBeNull();
        createdPo.Status.Should().Be("Draft");
        createdPo.OrderNumber.Should().StartWith("PO-");
        createdPo.GrandTotal.Should().Be(193000000m);
        createdPo.ConcurrencyToken.Should().NotBeNullOrWhiteSpace();

        // Verify Audit entry created in SQL
        var auditsAfterDraft = await _dbContext.PurchaseOrderAudits
            .Where(a => a.PurchaseOrderId == createdPo.Id)
            .ToListAsync();
        auditsAfterDraft.Should().ContainSingle(a => a.Action == "Created");

        // 2. PREVENT STALE ISSUANCE (CONCURRENCY CHECK)
        var staleToken = "INVALID-OR-STALE-TOKEN-" + Guid.NewGuid();
        var actStale = () => poService.IssueOrderAsync(createdPo.Id, "sahar\\procurement", staleToken);
        await actStale.Should().ThrowAsync<DbUpdateConcurrencyException>();

        // 3. ISSUE ORDER (DRAFT -> ISSUED) WITH CORRECT TOKEN
        var issuedPo = await poService.IssueOrderAsync(createdPo.Id, "sahar\\procurement", createdPo.ConcurrencyToken);

        issuedPo.Should().NotBeNull();
        issuedPo.Status.Should().Be("Issued");
        issuedPo.IssuedByUsername.Should().Be("sahar\\procurement");
        issuedPo.IssuedAtUtc.Should().NotBeNull();
        issuedPo.ConcurrencyToken.Should().NotBe(createdPo.ConcurrencyToken);

        // Verify Frozen Snapshots in SQL Server table
        var lineInDb = await _dbContext.PurchaseOrderLines
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.PurchaseOrderId == createdPo.Id);
        lineInDb.Should().NotBeNull();
        lineInDb!.MaterialCodeSnapshot.Should().Be("MAT-SQL-WHEAT-01");
        lineInDb.MaterialNameSnapshot.Should().Be("گندم آسیاب‌شده درجه یک بهاره");
        lineInDb.UnitNameSnapshot.Should().Be("کیلوگرم");
        lineInDb.SupplierNameSnapshot.Should().Be("شرکت کشت و صنعت غلات سحر البرز (سهامی خاص)");

        // Verify Audit entry in SQL Server table
        var auditsAfterIssue = await _dbContext.PurchaseOrderAudits
            .AsNoTracking()
            .Where(a => a.PurchaseOrderId == createdPo.Id)
            .OrderBy(a => a.TimestampUtc)
            .ToListAsync();
        auditsAfterIssue.Should().HaveCount(2);
        auditsAfterIssue.Select(a => a.Action).Should().ContainInOrder("Created", "DirectlyIssuedByProcurement");

        // 4. PREVENT DUPLICATE ISSUANCE
        var actDuplicate = () => poService.IssueOrderAsync(createdPo.Id, "sahar\\procurement", issuedPo.ConcurrencyToken);
        var ex = await actDuplicate.Should().ThrowAsync<InvalidOperationException>();
        ex.WithMessage("*تنها سفارشات در وضعیت پیش‌نویس قابل صدور هستند*");

        // 5. GOODS RECEIPT IN SQL SERVER (ISSUED -> FULLY RECEIVED)
        var receiptReq = new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = issuedPo.Id,
            WarehouseId = warehouseId,
            CarrierName = "باربری حمل‌ونقل ریلی البرز",
            DeliveryNoteNumber = "WAYBILL-1405-883",
            VehiclePlateNumber = "ایران ۲۲ - ۹۹۸ د ۱۱",
            Notes = "تخلیه کامل در سیلوی شماره ۳ با آزمون رطوبت استاندارد ۱۲ درصد",
            Lines = new List<CreateGoodsReceiptLineRequest>
            {
                new()
                {
                    PurchaseOrderLineId = lineInDb.Id,
                    DeliveredQuantity = 1000m,
                    AcceptedQuantity = 1000m,
                    RejectedQuantity = 0m,
                    QcStatus = "Passed",
                    BatchNumber = "BATCH-SQL-WHEAT-2026",
                    ExpiryDateUtc = DateTime.UtcNow.AddMonths(12),
                    StorageLocation = "سیلو ۳ - بخش غلات",
                    Notes = "کیفیت دانه و سختی با آزمون برابک تایید شد"
                }
            }
        };

        var receiptDto = await grService.CreateReceiptAsync(receiptReq, "sahar\\warehouse");

        receiptDto.Should().NotBeNull();
        receiptDto.ReceiptNumber.Should().StartWith("GRN-");
        receiptDto.Lines[0].AcceptedQuantity.Should().Be(1000m);

        // Verify PO transitioned to FullyReceived in SQL Server
        var finalPoInDb = await _dbContext.PurchaseOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == createdPo.Id);

        finalPoInDb.Should().NotBeNull();
        finalPoInDb!.Status.Should().Be(PurchaseOrderStatus.FullyReceived);

        _output.WriteLine("\n================================================================================");
        _output.WriteLine(" آزمون کامل روی موتور واقعی Microsoft SQL Server با موفقیت ۱۰۰٪ پاس شد");
        _output.WriteLine("================================================================================");
    }

    [Fact]
    public async Task Rollback_DatabaseLevelConcurrency_WhenConcurrentSessionModifiesSnapshotRowInSqlServer_UserEditIsNotOverwritten()
    {
        _output.WriteLine("\n================================================================================");
        _output.WriteLine(" PROVING DATABASE-LEVEL CONCURRENCY PROTECTION IN ROLLBACK (MICROSOFT SQL SERVER)");
        _output.WriteLine($" Instance: Localhost | Isolated Test Database: {_dbName}");
        _output.WriteLine("================================================================================");

        var (matId, unitId, _, _) = await SeedPrerequisitesAsync(_dbContext);

        var whMain = new Warehouse
        {
            Code = "WH-MAIN",
            Name = "انبار اصلی",
            SepidarWarehouseRef = "1",
            IsActive = true
        };
        _dbContext.Warehouses.Add(whMain);
        await _dbContext.SaveChangesAsync();

        // 1. Initial snapshot in SQL Server
        var snapshot = new InventorySnapshot
        {
            WarehouseId = whMain.Id,
            MaterialId = matId,
            OnHandQuantity = 100m,
            ReservableQuantity = 90m,
            ReservedQuantity = 10m,
            IncomingQuantity = 5m,
            UnitId = unitId,
            SnapshotTimeUtc = DateTime.UtcNow
        };
        _dbContext.InventorySnapshots.Add(snapshot);

        // Map item
        _dbContext.InventoryItemMappings.Add(new InventoryItemMapping
        {
            MaterialId = matId,
            SepidarItemId = "7001",
            SepidarItemCode = "SQL-TEST-MAT",
            SepidarBaseUnit = "KG"
        });
        await _dbContext.SaveChangesAsync();

        // 2. Perform Sync via InventorySyncService
        var adapter = new SqlServerTestSepidarAdapter(
            new List<SepidarItemRecord> { new("7001", "SQL-TEST-MAT", "آزمون همزمانی اس کیو ال", "KG", "کیلوگرم", true) },
            new List<SepidarStockRecord> { new("7001", "1", 200m, null, null, 0m, "KG", DateTime.UtcNow) });

        var calc = new ConsumptionCalculator(_dbContext);
        var engine = new PurchaseRecommendationEngine(_dbContext, calc);
        var syncService = new InventorySyncService(_dbContext, adapter, calc, engine, Microsoft.Extensions.Logging.Abstractions.NullLogger<InventorySyncService>.Instance);

        var syncJob = await syncService.TriggerSyncAsync(SyncJobType.Full, "SyncTester");
        syncJob.Status.Should().Be("Success");

        // Verify snapshot was updated to 200
        using (var verifyCtx = CreateNewDbContext())
        {
            var snapAfterSync = await verifyCtx.InventorySnapshots.FirstAsync(s => s.Id == snapshot.Id);
            snapAfterSync.OnHandQuantity.Should().Be(200m);
        }

        // 3. CONCURRENT MODIFICATION IN SQL SERVER (Simulate external user/API modifying row in database)
        // Context B modifies ReservedQuantity directly in SQL Server, causing RowVersion to advance
        using (var externalUserCtx = CreateNewDbContext())
        {
            var snapToEdit = await externalUserCtx.InventorySnapshots.FirstAsync(s => s.Id == snapshot.Id);
            snapToEdit.ReservedQuantity = 75m; // User reserves 75kg for production!
            await externalUserCtx.SaveChangesAsync();
            _output.WriteLine("  [کاربر هم‌زمان در SQL Server]: فیلد ReservedQuantity را به 75 تغییر داد (RowVersion افزایش یافت).");
        }

        // 4. NOW EXECUTE ROLLBACK from SyncService (using a fresh context that read the audit details)
        using (var rollbackCtx = CreateNewDbContext())
        {
            var rollbackCalc = new ConsumptionCalculator(rollbackCtx);
            var rollbackEngine = new PurchaseRecommendationEngine(rollbackCtx, rollbackCalc);
            var rollbackSyncService = new InventorySyncService(rollbackCtx, adapter, rollbackCalc, rollbackEngine, Microsoft.Extensions.Logging.Abstractions.NullLogger<InventorySyncService>.Instance);

            var rollbackResult = await rollbackSyncService.RollbackSyncJobAsync(syncJob.Id, "RollbackTester");

            _output.WriteLine($"  [نتیجه Rollback]: SnapshotsReverted = {rollbackResult.SnapshotsReverted}, PreservedUserEdits = {rollbackResult.SnapshotsPreservedDueToUserEdit}");

            // 5. VERIFY DATABASE STATE IN A FRESH CONTEXT:
            using var finalCtx = CreateNewDbContext();
            var finalSnap = await finalCtx.InventorySnapshots.AsNoTracking().FirstAsync(s => s.Id == snapshot.Id);

            _output.WriteLine($"  [وضعیت نهایی در دیتابیس]: OnHand = {finalSnap.OnHandQuantity}, Reserved = {finalSnap.ReservedQuantity}");

            // CRITICAL PROOF: The user's concurrent edit in SQL Server was PRESERVED and NOT overwritten!
            finalSnap.ReservedQuantity.Should().Be(75m, "تغییر کاربر در سطح پایگاه داده SQL Server نباید توسط Rollback رونویسی شود");
            rollbackResult.SnapshotsPreservedDueToUserEdit.Should().Be(1, "سامانه باید تشخیص دهد که رکورد دستخوش تغییر کاربر شده و آن را حفظ کند");
        }
    }
}

public class SqlServerTestSepidarAdapter : ParkPalace.Application.Interfaces.ISepidarInventoryAdapter
{
    private readonly List<SepidarItemRecord> _items;
    private readonly List<SepidarStockRecord> _stock;

    public SqlServerTestSepidarAdapter(List<SepidarItemRecord> items, List<SepidarStockRecord> stock)
    {
        _items = items;
        _stock = stock;
    }

    public bool IsLiveConnected => false;
    public string AdapterName => "SqlServerTestSepidarAdapter";

    public Task<List<SepidarWarehouseRecord>> GetWarehousesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<SepidarWarehouseRecord> { new("1", "1", "انبار اصلی", true) });

    public Task<List<SepidarItemRecord>> GetItemsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_items);

    public Task<List<SepidarStockRecord>> GetStockBalancesAsync(string warehouseRef, CancellationToken cancellationToken = default) =>
        Task.FromResult(_stock);

    public Task<List<SepidarMovementRecord>> GetInventoryMovementsAsync(string warehouseRef, DateTime? sinceDateUtc = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<SepidarMovementRecord>());
}
