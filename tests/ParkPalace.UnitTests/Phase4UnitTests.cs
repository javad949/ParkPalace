using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Adapters;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Services;
using Xunit;

namespace ParkPalace.UnitTests;

public class Phase4UnitTests
{
    private CrmDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CrmDbContext(options);
    }

    [Fact]
    public async Task ConsumptionCalculator_OnlyCountsPostedNonCancelledConsumption_IgnoresTransfersAndCancelled()
    {
        using var context = CreateInMemoryDbContext();
        var kgUnit = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم", Precision = 2 };
        var category = new Category { Code = "CAT-FLOUR", Name = "آرد" };
        var material = new Material
        {
            InternalCode = "MAT-FLOUR-01",
            Name = "آرد نان باگت",
            CategoryId = category.Id,
            BaseUnitId = kgUnit.Id
        };
        var warehouse = new Warehouse { Code = "WH-MAIN", Name = "انبار مرکزی" };

        context.UnitsOfMeasure.Add(kgUnit);
        context.Categories.Add(category);
        context.Materials.Add(material);
        context.Warehouses.Add(warehouse);

        var now = DateTime.UtcNow;

        // Valid consumption: 500 kg (Posted, Not Cancelled)
        context.InventoryMovements.Add(new InventoryMovement
        {
            MaterialId = material.Id,
            WarehouseId = warehouse.Id,
            UnitId = kgUnit.Id,
            VoucherNumber = "VCH-01",
            MovementType = InventoryMovementType.Consumption,
            Quantity = 500m,
            PostingStatus = 4,
            IsCancelled = false,
            TransactionDateUtc = now.AddDays(-2)
        });

        // Cancelled consumption: 1000 kg (Must be IGNORED)
        context.InventoryMovements.Add(new InventoryMovement
        {
            MaterialId = material.Id,
            WarehouseId = warehouse.Id,
            UnitId = kgUnit.Id,
            VoucherNumber = "VCH-02",
            MovementType = InventoryMovementType.Consumption,
            Quantity = 1000m,
            PostingStatus = 4,
            IsCancelled = true,
            TransactionDateUtc = now.AddDays(-3)
        });

        // Draft consumption: 2000 kg (Must be IGNORED)
        context.InventoryMovements.Add(new InventoryMovement
        {
            MaterialId = material.Id,
            WarehouseId = warehouse.Id,
            UnitId = kgUnit.Id,
            VoucherNumber = "VCH-03",
            MovementType = InventoryMovementType.Consumption,
            Quantity = 2000m,
            PostingStatus = 1, // Draft
            IsCancelled = false,
            TransactionDateUtc = now.AddDays(-1)
        });

        // Transfer: 500 kg (Must be IGNORED from consumption)
        context.InventoryMovements.Add(new InventoryMovement
        {
            MaterialId = material.Id,
            WarehouseId = warehouse.Id,
            UnitId = kgUnit.Id,
            VoucherNumber = "VCH-04",
            MovementType = InventoryMovementType.Transfer,
            Quantity = 500m,
            PostingStatus = 4,
            IsCancelled = false,
            TransactionDateUtc = now.AddDays(-2)
        });

        await context.SaveChangesAsync();

        var calculator = new ConsumptionCalculator(context);
        var consumption = await calculator.CalculateConsumptionAsync(material.Id, now);

        // Only the 500 kg valid consumption should be counted in 7d and 30d totals
        Assert.Equal(500m, consumption.Consumption7dTotal);
        Assert.Equal(500m, consumption.Consumption30dTotal);
        Assert.Equal(Math.Round(500m / 7m, 2), consumption.Consumption7dAverage);
        Assert.Equal(Math.Round(500m / 30m, 2), consumption.Consumption30dAverage);
    }

    [Fact]
    public async Task ConsumptionCalculator_ZeroConsumptionDays_AreProperlyAveragedWithoutDistortion()
    {
        using var context = CreateInMemoryDbContext();
        var unit = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم" };
        var category = new Category { Code = "CAT-TEST", Name = "تست" };
        var material = new Material { InternalCode = "MAT-TEST", Name = "کالای تستی", CategoryId = category.Id, BaseUnitId = unit.Id };
        var warehouse = new Warehouse { Code = "WH-MAIN", Name = "انبار مرکزی" };

        context.UnitsOfMeasure.Add(unit);
        context.Categories.Add(category);
        context.Materials.Add(material);
        context.Warehouses.Add(warehouse);

        var now = DateTime.UtcNow;

        // Only 2 days of consumption in 7 days: Day -1 = 140 kg, Day -3 = 140 kg (Total 280 kg)
        // 5 zero days! Average daily must be 280 / 7 = 40 kg/day (NOT 280 / 2 = 140)
        context.InventoryMovements.Add(new InventoryMovement
        {
            MaterialId = material.Id,
            WarehouseId = warehouse.Id,
            UnitId = unit.Id,
            VoucherNumber = "V1",
            MovementType = InventoryMovementType.Consumption,
            Quantity = 140m,
            PostingStatus = 4,
            TransactionDateUtc = now.AddDays(-1)
        });

        context.InventoryMovements.Add(new InventoryMovement
        {
            MaterialId = material.Id,
            WarehouseId = warehouse.Id,
            UnitId = unit.Id,
            VoucherNumber = "V2",
            MovementType = InventoryMovementType.Consumption,
            Quantity = 140m,
            PostingStatus = 4,
            TransactionDateUtc = now.AddDays(-3)
        });

        await context.SaveChangesAsync();

        var calculator = new ConsumptionCalculator(context);
        var result = await calculator.CalculateConsumptionAsync(material.Id, now);

        Assert.Equal(280m, result.Consumption7dTotal);
        Assert.Equal(40m, result.Consumption7dAverage); // 280 / 7 = 40
    }

    [Fact]
    public async Task ConsumptionCalculator_WithUnitConversion_NormalizesToBaseUnit()
    {
        using var context = CreateInMemoryDbContext();
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم" };
        var bag50 = new UnitOfMeasure { Code = "BAG50", Name = "کیسه ۵۰ کیلویی" };
        var category = new Category { Code = "CAT-FLOUR", Name = "آرد" };
        var material = new Material { InternalCode = "MAT-FLOUR-CONV", Name = "آرد ستاره", CategoryId = category.Id, BaseUnitId = kg.Id };
        var warehouse = new Warehouse { Code = "WH-MAIN", Name = "انبار مرکزی" };

        context.UnitsOfMeasure.AddRange(kg, bag50);
        context.Categories.Add(category);
        context.Materials.Add(material);
        context.Warehouses.Add(warehouse);

        // Conversion: 1 BAG50 = 50 KG
        context.MaterialUnitConversions.Add(new MaterialUnitConversion
        {
            MaterialId = material.Id,
            FromUnitId = bag50.Id,
            ToUnitId = kg.Id,
            Factor = 50m,
            IsConfirmed = true,
            Notes = "تأیید رسمی: هر کیسه ۵۰ کیلوگرم"
        });

        var now = DateTime.UtcNow;

        // Consumption recorded in BAG50: 10 bags (= 500 kg)
        context.InventoryMovements.Add(new InventoryMovement
        {
            MaterialId = material.Id,
            WarehouseId = warehouse.Id,
            UnitId = bag50.Id,
            VoucherNumber = "V-BAG",
            MovementType = InventoryMovementType.Consumption,
            Quantity = 10m,
            PostingStatus = 4,
            TransactionDateUtc = now.AddDays(-2)
        });

        await context.SaveChangesAsync();

        var calculator = new ConsumptionCalculator(context);
        var result = await calculator.CalculateConsumptionAsync(material.Id, now);

        // 10 bags * 50 = 500 kg
        Assert.Equal(500m, result.Consumption7dTotal);
    }

    [Fact]
    public async Task PurchaseRecommendation_StandardFormula_ComputesTargetAndSuggestedQuantitiesCorrectly()
    {
        using var context = CreateInMemoryDbContext();
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم" };
        var cat = new Category { Code = "CAT-TEST", Name = "دسته" };
        var mat = new Material { InternalCode = "MAT-REC-01", Name = "ماده تستی", CategoryId = cat.Id, BaseUnitId = kg.Id, SafetyStock = 500m, LeadTimeDays = 2 };
        var wh = new Warehouse { Code = "WH-MAIN", Name = "انبار مرکزی" };

        context.UnitsOfMeasure.Add(kg);
        context.Categories.Add(cat);
        context.Materials.Add(mat);
        context.Warehouses.Add(wh);

        // Snapshot: OnHand = 1000 kg, Reservable = 800 kg, Incoming = 200 kg, Reserved = 100 kg
        context.InventorySnapshots.Add(new InventorySnapshot
        {
            MaterialId = mat.Id,
            WarehouseId = wh.Id,
            UnitId = kg.Id,
            OnHandQuantity = 1000m,
            ReservableQuantity = 800m,
            IncomingQuantity = 200m,
            ReservedQuantity = 100m,
            SnapshotTimeUtc = DateTime.UtcNow
        });

        // 14 days of consumption = 1400 kg => Average = 100 kg/day
        var now = DateTime.UtcNow;
        for (int i = 1; i <= 14; i++)
        {
            context.InventoryMovements.Add(new InventoryMovement
            {
                MaterialId = mat.Id,
                WarehouseId = wh.Id,
                UnitId = kg.Id,
                VoucherNumber = $"V-{i}",
                MovementType = InventoryMovementType.Consumption,
                Quantity = 100m,
                PostingStatus = 4,
                TransactionDateUtc = now.AddDays(-i)
            });
        }

        // Policy: ReviewPeriodDays = 3, LeadTimeDays = 2, SafetyStock = 500, MOQ = 0
        context.ReplenishmentPolicies.Add(new ReplenishmentPolicy
        {
            MaterialId = mat.Id,
            ReviewPeriodDays = 3,
            LeadTimeDays = 2,
            SafetyStock = 500m,
            MinOrderQuantity = 0m
        });

        await context.SaveChangesAsync();

        var calc = new ConsumptionCalculator(context);
        var engine = new PurchaseRecommendationEngine(context, calc);

        var rec = await engine.GenerateRecommendationForMaterialAsync(mat.Id, wh.Id);

        Assert.NotNull(rec);
        // Average daily consumption = 100 kg
        Assert.Equal(100m, rec.AverageDailyConsumption);
        // TargetStock = 100 * (2 + 3) + 500 = 1000 kg
        Assert.Equal(1000m, rec.TargetStock);
        // InventoryPosition = 800 (Reservable) + 200 (Incoming) - 100 (Reserved) = 900 kg
        Assert.Equal(900m, rec.InventoryPosition);
        // RawSuggested = max(0, 1000 - 900) = 100 kg
        Assert.Equal(100m, rec.RawSuggestedQuantity);
        Assert.Equal(100m, rec.RoundedSuggestedQuantity);
    }

    [Fact]
    public async Task PurchaseRecommendation_AppliesMOQ_WhenRawSuggestedIsBelowMOQ()
    {
        using var context = CreateInMemoryDbContext();
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم" };
        var cat = new Category { Code = "CAT-MOQ", Name = "دسته" };
        var mat = new Material { InternalCode = "MAT-MOQ-01", Name = "روغن مخصوص", CategoryId = cat.Id, BaseUnitId = kg.Id };
        var wh = new Warehouse { Code = "WH-MAIN", Name = "انبار مرکزی" };

        context.UnitsOfMeasure.Add(kg);
        context.Categories.Add(cat);
        context.Materials.Add(mat);
        context.Warehouses.Add(wh);

        // Snapshot: Reservable = 850 kg
        context.InventorySnapshots.Add(new InventorySnapshot
        {
            MaterialId = mat.Id,
            WarehouseId = wh.Id,
            UnitId = kg.Id,
            OnHandQuantity = 850m,
            ReservableQuantity = 850m,
            SnapshotTimeUtc = DateTime.UtcNow
        });

        // 14 days consumption = 700 kg => Avg = 50 kg/day
        var now = DateTime.UtcNow;
        for (int i = 1; i <= 14; i++)
        {
            context.InventoryMovements.Add(new InventoryMovement
            {
                MaterialId = mat.Id,
                WarehouseId = wh.Id,
                UnitId = kg.Id,
                VoucherNumber = $"V-MOQ-{i}",
                MovementType = InventoryMovementType.Consumption,
                Quantity = 50m,
                PostingStatus = 4,
                TransactionDateUtc = now.AddDays(-i)
            });
        }

        // Policy: Review = 2, Lead = 2, Safety = 800 => Target = 50 * 4 + 800 = 1000 kg
        // Raw Suggested = 1000 - 850 = 150 kg
        // BUT MOQ is 500 kg! So RoundedSuggested must be 500 kg!
        context.ReplenishmentPolicies.Add(new ReplenishmentPolicy
        {
            MaterialId = mat.Id,
            ReviewPeriodDays = 2,
            LeadTimeDays = 2,
            SafetyStock = 800m,
            MinOrderQuantity = 500m
        });

        await context.SaveChangesAsync();

        var calc = new ConsumptionCalculator(context);
        var engine = new PurchaseRecommendationEngine(context, calc);
        var rec = await engine.GenerateRecommendationForMaterialAsync(mat.Id, wh.Id);

        Assert.NotNull(rec);
        Assert.Equal(150m, rec.RawSuggestedQuantity);
        Assert.Equal(500m, rec.RoundedSuggestedQuantity); // Adjusted up to MOQ
    }

    [Fact]
    public async Task PurchaseRecommendation_AppliesPackageMultipleCeiling_RoundsUpToFullSacks()
    {
        using var context = CreateInMemoryDbContext();
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم" };
        var cat = new Category { Code = "CAT-PKG", Name = "دسته" };
        var mat = new Material { InternalCode = "MAT-PKG-01", Name = "آرد نان", CategoryId = cat.Id, BaseUnitId = kg.Id };
        var wh = new Warehouse { Code = "WH-MAIN", Name = "انبار مرکزی" };

        context.UnitsOfMeasure.Add(kg);
        context.Categories.Add(cat);
        context.Materials.Add(mat);
        context.Warehouses.Add(wh);

        // Reservable = 880 kg
        context.InventorySnapshots.Add(new InventorySnapshot
        {
            MaterialId = mat.Id,
            WarehouseId = wh.Id,
            UnitId = kg.Id,
            OnHandQuantity = 880m,
            ReservableQuantity = 880m,
            SnapshotTimeUtc = DateTime.UtcNow
        });

        // 14 days consumption = 1400 => Avg = 100 kg/day
        var now = DateTime.UtcNow;
        for (int i = 1; i <= 14; i++)
        {
            context.InventoryMovements.Add(new InventoryMovement
            {
                MaterialId = mat.Id,
                WarehouseId = wh.Id,
                UnitId = kg.Id,
                VoucherNumber = $"V-PKG-{i}",
                MovementType = InventoryMovementType.Consumption,
                Quantity = 100m,
                PostingStatus = 4,
                TransactionDateUtc = now.AddDays(-i)
            });
        }

        // Policy: Target = 100 * 4 + 800 = 1200 kg
        // Raw Suggested = 1200 - 880 = 320 kg
        // PackageMultiple is 50 kg sacks!
        // 320 / 50 = 6.4 => Math.Ceiling(6.4) * 50 = 7 * 50 = 350 kg!
        context.ReplenishmentPolicies.Add(new ReplenishmentPolicy
        {
            MaterialId = mat.Id,
            ReviewPeriodDays = 2,
            LeadTimeDays = 2,
            SafetyStock = 800m,
            PackageMultiple = 50m
        });

        await context.SaveChangesAsync();

        var calc = new ConsumptionCalculator(context);
        var engine = new PurchaseRecommendationEngine(context, calc);
        var rec = await engine.GenerateRecommendationForMaterialAsync(mat.Id, wh.Id);

        Assert.NotNull(rec);
        Assert.Equal(320m, rec.RawSuggestedQuantity);
        Assert.Equal(350m, rec.RoundedSuggestedQuantity); // Rounded up to full 50kg bags
    }

    [Fact]
    public async Task InventorySync_Idempotency_PreventDuplicateMovementImports()
    {
        using var context = CreateInMemoryDbContext();
        var adapter = new SepidarFixtureInventoryAdapter(NullLogger<SepidarFixtureInventoryAdapter>.Instance);
        var calc = new ConsumptionCalculator(context);
        var engine = new PurchaseRecommendationEngine(context, calc);
        var syncService = new InventorySyncService(
            context, adapter, calc, engine, NullLogger<InventorySyncService>.Instance);

        // First sync run
        var job1 = await syncService.TriggerSyncAsync(SyncJobType.Full, "TestUser");
        Assert.Equal("Success", job1.Status);
        int movementsFirstRun = await context.InventoryMovements.CountAsync();
        Assert.True(movementsFirstRun > 0);

        // Second sync run with identical source fixtures
        var job2 = await syncService.TriggerSyncAsync(SyncJobType.Incremental, "TestUser");
        Assert.Equal("Success", job2.Status);
        int movementsSecondRun = await context.InventoryMovements.CountAsync();

        // Count MUST be strictly identical because SourceMovementKey prevents duplicates!
        Assert.Equal(movementsFirstRun, movementsSecondRun);
    }

    [Fact]
    public async Task SepidarLiveInventoryAdapter_ThrowsExplicitlyWhenNotConfigured_NoSilentFallback()
    {
        var config = new ConfigurationBuilder().Build();
        var adapter = new SepidarLiveInventoryAdapter(config, NullLogger<SepidarLiveInventoryAdapter>.Instance);

        Assert.False(adapter.IsLiveConnected);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.GetWarehousesAsync());
        Assert.Contains("Sepidar connection string 'SepidarDatabase' is not configured", ex.Message);
    }

    [Fact]
    public async Task SepidarLiveInventoryAdapter_ThrowsTransparentErrorWhenConnectionFails_NoSilentFallback()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SepidarDatabase"] = "Server=127.0.0.1,65534;Database=NonExistentDb;Connect Timeout=1;"
            })
            .Build();

        var adapter = new SepidarLiveInventoryAdapter(config, NullLogger<SepidarLiveInventoryAdapter>.Instance);
        Assert.True(adapter.IsLiveConnected);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.GetWarehousesAsync());
        Assert.Contains("خطا در برقراری ارتباط با پایگاه داده سپیدار", ex.Message);
    }

    [Fact]
    public async Task InventorySync_DeduplicationAndPreservation_PreservesCrmFieldsAndUpdatesExplicitMapping()
    {
        using var context = CreateInMemoryDbContext();
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم" };
        var cat = new Category { Code = "CAT-RAW", Name = "مواد اولیه" };
        context.UnitsOfMeasure.Add(kg);
        context.Categories.Add(cat);

        // Pre-existing CRM Material with custom enrichments
        var existingMat = new Material
        {
            InternalCode = "404229",
            Name = "تخم مرغ محلی ممتاز",
            CategoryId = cat.Id,
            BaseUnitId = kg.Id,
            MinStock = 5000m,
            SafetyStock = 10000m,
            LeadTimeDays = 3,
            Brand = "مزرعه سحر",
            IsActive = true
        };
        context.Materials.Add(existingMat);

        // Pre-existing explicit mapping
        var existingMapping = new InventoryItemMapping
        {
            MaterialId = existingMat.Id,
            SepidarItemId = "721",
            SepidarItemCode = "404229",
            SepidarItemTitle = "تخم مرغ",
            SepidarBaseUnit = "COUNT",
            IsActive = true
        };
        context.InventoryItemMappings.Add(existingMapping);
        await context.SaveChangesAsync();

        // Mock adapter returning Sepidar item with matching Code 404229 but inactive status
        var mockAdapter = new MockSepidarInventoryAdapter(new List<SepidarItemRecord>
        {
            new("721", "404229", "تخم مرغ", "COUNT", "عدد", false)
        });

        var calc = new ConsumptionCalculator(context);
        var engine = new PurchaseRecommendationEngine(context, calc);
        var syncService = new InventorySyncService(context, mockAdapter, calc, engine, NullLogger<InventorySyncService>.Instance);

        var job = await syncService.TriggerSyncAsync(SyncJobType.Incremental, "TestRunner");
        Assert.Equal("Success", job.Status);

        // 1. Verify NO duplicate material was inserted
        var materials = await context.Materials.Where(m => m.InternalCode == "404229").ToListAsync();
        Assert.Single(materials);

        var matched = materials.First();
        // 2. Verify CRM-specific fields are preserved!
        Assert.Equal(5000m, matched.MinStock);
        Assert.Equal(10000m, matched.SafetyStock);
        Assert.Equal(3, matched.LeadTimeDays);
        Assert.Equal("مزرعه سحر", matched.Brand);

        // 3. Verify Mapping was updated and synced inactive status
        var mapping = await context.InventoryItemMappings.FirstOrDefaultAsync(m => m.SepidarItemId == "721");
        Assert.NotNull(mapping);
        Assert.False(mapping.IsActive);
        Assert.False(matched.IsActive); // Synced inactive status
    }

    [Fact]
    public async Task InventorySync_AmbiguousCodeMatch_BlocksAutoLinkingAndRecordsConflict()
    {
        using var context = CreateInMemoryDbContext();
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم" };
        var cat = new Category { Code = "CAT-RAW", Name = "مواد اولیه" };
        context.UnitsOfMeasure.Add(kg);
        context.Categories.Add(cat);

        // Pre-existing CRM Material with code "404229" but NO mapping
        var existingMat = new Material
        {
            InternalCode = "404229",
            Name = "تخم مرغ محلی سحر",
            CategoryId = cat.Id,
            BaseUnitId = kg.Id,
            IsActive = true
        };
        context.Materials.Add(existingMat);
        await context.SaveChangesAsync();

        // Sepidar item with same code "404229" and ItemRef "721", but no mapping exists
        var mockAdapter = new MockSepidarInventoryAdapter(new List<SepidarItemRecord>
        {
            new("721", "404229", "تخم مرغ سپیدار", "COUNT", "عدد", true)
        });

        var calc = new ConsumptionCalculator(context);
        var engine = new PurchaseRecommendationEngine(context, calc);
        var syncService = new InventorySyncService(context, mockAdapter, calc, engine, NullLogger<InventorySyncService>.Instance);

        var job = await syncService.TriggerSyncAsync(SyncJobType.Incremental, "TestRunner");
        Assert.Equal("Success", job.Status);

        // Verify: Automatic linking was BLOCKED!
        var mapping = await context.InventoryItemMappings.FirstOrDefaultAsync(m => m.SepidarItemId == "721");
        Assert.Null(mapping); // Must NOT auto-link ambiguous code match!

        // Verify: Conflict was recorded in InventorySyncErrors
        var conflict = await context.InventorySyncErrors.FirstOrDefaultAsync(e => e.ErrorCode == "AMBIGUOUS_CODE_MATCH");
        Assert.NotNull(conflict);
        Assert.Contains("404229", conflict.ErrorMessage);
    }

    [Fact]
    public async Task InventorySync_MidRunFailure_RecoversCleanlyWithoutDuplicateOrDoubledInventory()
    {
        using var context = CreateInMemoryDbContext();
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم" };
        var cat = new Category { Code = "CAT-RAW", Name = "مواد اولیه" };
        context.UnitsOfMeasure.Add(kg);
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var item = new SepidarItemRecord("101", "MAT-01", "کالای آزمایشی", "KG", "کیلوگرم", true);
        var stock = new SepidarStockRecord("101", "1", 500m, null, null, 0m, "KG", DateTime.UtcNow);
        var movement = new SepidarMovementRecord("REC-1", "V-1", "101", "1", InventoryMovementType.Receipt, 500m, "KG", DateTime.UtcNow, 4, false, null);

        // 1. Run partial sync with a failing adapter on movements
        var failingAdapter = new ControllableSepidarInventoryAdapter(
            new List<SepidarItemRecord> { item },
            new List<SepidarStockRecord> { stock },
            throwOnMovements: true);

        var calc = new ConsumptionCalculator(context);
        var engine = new PurchaseRecommendationEngine(context, calc);
        var syncService = new InventorySyncService(context, failingAdapter, calc, engine, NullLogger<InventorySyncService>.Instance);

        var job1 = await syncService.TriggerSyncAsync(SyncJobType.Full, "TestRunner");
        Assert.Equal("Failed", job1.Status);

        // Verify intermediate state
        int matCount1 = await context.Materials.CountAsync();
        int snapCount1 = await context.InventorySnapshots.CountAsync();
        decimal onHand1 = (await context.InventorySnapshots.FirstAsync()).OnHandQuantity;
        Assert.Equal(1, matCount1);
        Assert.Equal(1, snapCount1);
        Assert.Equal(500m, onHand1);

        // 2. Re-run sync with healthy adapter
        var healthyAdapter = new ControllableSepidarInventoryAdapter(
            new List<SepidarItemRecord> { item },
            new List<SepidarStockRecord> { stock },
            new List<SepidarMovementRecord> { movement },
            throwOnMovements: false);

        var syncService2 = new InventorySyncService(context, healthyAdapter, calc, engine, NullLogger<InventorySyncService>.Instance);
        var job2 = await syncService2.TriggerSyncAsync(SyncJobType.Full, "TestRunner");
        Assert.Equal("Success", job2.Status);

        // Verify: No duplicate materials, no duplicate snapshots, no doubled inventory!
        int matCount2 = await context.Materials.CountAsync();
        int snapCount2 = await context.InventorySnapshots.CountAsync();
        decimal onHand2 = (await context.InventorySnapshots.FirstAsync()).OnHandQuantity;
        int movCount2 = await context.InventoryMovements.CountAsync();

        Assert.Equal(1, matCount2);
        Assert.Equal(1, snapCount2);
        Assert.Equal(500m, onHand2); // Strictly 500m, NOT doubled to 1000m!
        Assert.Equal(1, movCount2);
    }

    [Fact]
    public async Task InventorySync_Rollback_RestoresChangesWhilePreservingSubsequentUserEdits()
    {
        using var context = CreateInMemoryDbContext();
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم" };
        var cat = new Category { Code = "CAT-RAW", Name = "مواد اولیه" };
        var wh = new Warehouse { Code = "WH-MAIN", Name = "انبار اصلی", SepidarWarehouseRef = "1" };
        var matA = new Material { InternalCode = "MAT-A", Name = "کالای الف", CategoryId = cat.Id, BaseUnitId = kg.Id };
        var matB = new Material { InternalCode = "MAT-B", Name = "کالای ب", CategoryId = cat.Id, BaseUnitId = kg.Id };

        context.UnitsOfMeasure.Add(kg);
        context.Categories.Add(cat);
        context.Warehouses.Add(wh);
        context.Materials.AddRange(matA, matB);

        // Initial snapshots
        var snapA = new InventorySnapshot { WarehouseId = wh.Id, MaterialId = matA.Id, OnHandQuantity = 100m, UnitId = kg.Id };
        var snapB = new InventorySnapshot { WarehouseId = wh.Id, MaterialId = matB.Id, OnHandQuantity = 200m, UnitId = kg.Id };
        context.InventorySnapshots.AddRange(snapA, snapB);

        // Explicit mappings
        context.InventoryItemMappings.Add(new InventoryItemMapping { MaterialId = matA.Id, SepidarItemId = "101", SepidarItemCode = "MAT-A", SepidarBaseUnit = "KG" });
        context.InventoryItemMappings.Add(new InventoryItemMapping { MaterialId = matB.Id, SepidarItemId = "102", SepidarItemCode = "MAT-B", SepidarBaseUnit = "KG" });
        await context.SaveChangesAsync();

        // Sync run: sets MatA to 150m, MatB to 300m
        var adapter = new ControllableSepidarInventoryAdapter(
            new List<SepidarItemRecord>
            {
                new("101", "MAT-A", "کالای الف", "KG", "کیلوگرم", true),
                new("102", "MAT-B", "کالای ب", "KG", "کیلوگرم", true)
            },
            new List<SepidarStockRecord>
            {
                new("101", "1", 150m, null, null, 0m, "KG", DateTime.UtcNow),
                new("102", "1", 300m, null, null, 0m, "KG", DateTime.UtcNow)
            });

        var calc = new ConsumptionCalculator(context);
        var engine = new PurchaseRecommendationEngine(context, calc);
        var syncService = new InventorySyncService(context, adapter, calc, engine, NullLogger<InventorySyncService>.Instance);

        var job = await syncService.TriggerSyncAsync(SyncJobType.Incremental, "Operator");
        Assert.Equal("Success", job.Status);

        // Verify updated
        Assert.Equal(150m, (await context.InventorySnapshots.FirstAsync(s => s.MaterialId == matA.Id)).OnHandQuantity);
        Assert.Equal(300m, (await context.InventorySnapshots.FirstAsync(s => s.MaterialId == matB.Id)).OnHandQuantity);

        // NOW: Simulate a user manual adjustment on MatB (e.g., physical stock recount changed MatB to 350m)
        var snapB_current = await context.InventorySnapshots.FirstAsync(s => s.MaterialId == matB.Id);
        snapB_current.OnHandQuantity = 350m;
        await context.SaveChangesAsync();

        // Perform fine-grained rollback of the sync job!
        var rollbackResult = await syncService.RollbackSyncJobAsync(job.Id, "AdminUser");
        Assert.True(rollbackResult.Success);
        Assert.Equal(1, rollbackResult.SnapshotsReverted); // Only MatA should be reverted!
        Assert.Equal(1, rollbackResult.SnapshotsPreservedDueToUserEdit); // MatB must be PRESERVED!

        // MatA was restored to prior value (100m)
        var snapA_reverted = await context.InventorySnapshots.FirstAsync(s => s.MaterialId == matA.Id);
        Assert.Equal(100m, snapA_reverted.OnHandQuantity);

        // MatB USER EDIT (350m) WAS NOT OVERWRITTEN!
        var snapB_preserved = await context.InventorySnapshots.FirstAsync(s => s.MaterialId == matB.Id);
        Assert.Equal(350m, snapB_preserved.OnHandQuantity);
    }

    [Fact]
    public async Task PurchaseRecommendationEngine_WhenReservableIsUnproven_CalculatesPositionWithoutSpeculativeReserve()
    {
        using var context = CreateInMemoryDbContext();
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم" };
        var cat = new Category { Code = "CAT-RAW", Name = "مواد اولیه" };
        var wh = new Warehouse { Code = "WH-MAIN", Name = "انبار مرکزی" };
        var mat = new Material
        {
            InternalCode = "MAT-FLOUR",
            Name = "آرد ستاره",
            CategoryId = cat.Id,
            BaseUnitId = kg.Id,
            SafetyStock = 1000m,
            LeadTimeDays = 2
        };

        context.UnitsOfMeasure.Add(kg);
        context.Categories.Add(cat);
        context.Warehouses.Add(wh);
        context.Materials.Add(mat);

        // Snapshot with OnHand = 1500m, Reservable = 0m (unproven), Incoming = 200m
        context.InventorySnapshots.Add(new InventorySnapshot
        {
            WarehouseId = wh.Id,
            MaterialId = mat.Id,
            OnHandQuantity = 1500m,
            ReservableQuantity = 0m, // Unproven in Sepidar!
            ReservedQuantity = 0m,
            IncomingQuantity = 200m,
            UnitId = kg.Id
        });

        // 10 days of consumption: 200 kg/day
        var now = DateTime.UtcNow;
        for (int i = 1; i <= 10; i++)
        {
            context.InventoryMovements.Add(new InventoryMovement
            {
                MaterialId = mat.Id,
                WarehouseId = wh.Id,
                UnitId = kg.Id,
                MovementType = InventoryMovementType.Consumption,
                Quantity = 200m,
                PostingStatus = 4,
                IsCancelled = false,
                TransactionDateUtc = now.AddDays(-i)
            });
        }
        await context.SaveChangesAsync();

        var calc = new ConsumptionCalculator(context);
        var engine = new PurchaseRecommendationEngine(context, calc);
        var rec = await engine.GenerateRecommendationForMaterialAsync(mat.Id, wh.Id);

        Assert.NotNull(rec);
        // Position must be OnHand (1500) + Incoming (200) = 1700
        Assert.Equal(1700m, rec.InventoryPosition);
        // ReservableOnHand in DTO must be null (unknown / unproven)
        Assert.Null(rec.ReservableOnHand);
        // Warning must indicate unverified reserve in Sepidar
        Assert.Contains("وضعیت رزرو در سپیدار نامعلوم است", rec.DataWarning);
    }

    [Fact]
    public async Task InventorySync_Rollback_PreservesSubsequentUserEditsWhenOtherFieldsModifiedWithConstantOnHand()
    {
        using var context = CreateInMemoryDbContext();
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم" };
        var cat = new Category { Code = "CAT-RAW", Name = "مواد اولیه" };
        var wh = new Warehouse { Code = "WH-MAIN", Name = "انبار اصلی", SepidarWarehouseRef = "1" };
        var matA = new Material { InternalCode = "MAT-A", Name = "کالای الف", CategoryId = cat.Id, BaseUnitId = kg.Id };
        var matB = new Material { InternalCode = "MAT-B", Name = "کالای ب", CategoryId = cat.Id, BaseUnitId = kg.Id };

        context.UnitsOfMeasure.Add(kg);
        context.Categories.Add(cat);
        context.Warehouses.Add(wh);
        context.Materials.AddRange(matA, matB);

        // Initial snapshots
        var snapA = new InventorySnapshot { WarehouseId = wh.Id, MaterialId = matA.Id, OnHandQuantity = 100m, ReservableQuantity = 90m, ReservedQuantity = 10m, IncomingQuantity = 0m, UnitId = kg.Id };
        var snapB = new InventorySnapshot { WarehouseId = wh.Id, MaterialId = matB.Id, OnHandQuantity = 200m, ReservableQuantity = 180m, ReservedQuantity = 20m, IncomingQuantity = 0m, UnitId = kg.Id };
        context.InventorySnapshots.AddRange(snapA, snapB);

        // Mappings
        context.InventoryItemMappings.Add(new InventoryItemMapping { MaterialId = matA.Id, SepidarItemId = "101", SepidarItemCode = "MAT-A", SepidarBaseUnit = "KG" });
        context.InventoryItemMappings.Add(new InventoryItemMapping { MaterialId = matB.Id, SepidarItemId = "102", SepidarItemCode = "MAT-B", SepidarBaseUnit = "KG" });
        await context.SaveChangesAsync();

        // Sync run: sets MatA to OnHand 150m, Reservable 120m, Reserved 30m
        // sets MatB to OnHand 300m, Reservable 250m, Reserved 50m
        var adapter = new ControllableSepidarInventoryAdapter(
            new List<SepidarItemRecord>
            {
                new("101", "MAT-A", "کالای الف", "KG", "کیلوگرم", true),
                new("102", "MAT-B", "کالای ب", "KG", "کیلوگرم", true)
            },
            new List<SepidarStockRecord>
            {
                new("101", "1", 150m, 120m, 30m, 0m, "KG", DateTime.UtcNow),
                new("102", "1", 300m, 250m, 50m, 0m, "KG", DateTime.UtcNow)
            });

        var calc = new ConsumptionCalculator(context);
        var engine = new PurchaseRecommendationEngine(context, calc);
        var syncService = new InventorySyncService(context, adapter, calc, engine, NullLogger<InventorySyncService>.Instance);

        var job = await syncService.TriggerSyncAsync(SyncJobType.Incremental, "Operator");
        Assert.Equal("Success", job.Status);

        // CRITICAL TEST CASE: User modifies ReservedQuantity on MatB (from 50m to 85m) while OnHand REMAINS STRICTLY 300m!
        var snapB_current = await context.InventorySnapshots.FirstAsync(s => s.MaterialId == matB.Id);
        Assert.Equal(300m, snapB_current.OnHandQuantity);
        snapB_current.ReservedQuantity = 85m; // Changed!
        await context.SaveChangesAsync();

        // Rollback sync job
        var rollbackResult = await syncService.RollbackSyncJobAsync(job.Id, "AdminUser");
        Assert.True(rollbackResult.Success);
        Assert.Equal(1, rollbackResult.SnapshotsReverted); // MatA reverted
        Assert.Equal(1, rollbackResult.SnapshotsPreservedDueToUserEdit); // MatB PRESERVED even though OnHand was identical!

        // MatA restored to prior values (OnHand: 100m, Reservable: 90m, Reserved: 10m)
        var snapA_reverted = await context.InventorySnapshots.FirstAsync(s => s.MaterialId == matA.Id);
        Assert.Equal(100m, snapA_reverted.OnHandQuantity);
        Assert.Equal(90m, snapA_reverted.ReservableQuantity);
        Assert.Equal(10m, snapA_reverted.ReservedQuantity);

        // MatB user modification was NOT overwritten by rollback!
        var snapB_preserved = await context.InventorySnapshots.FirstAsync(s => s.MaterialId == matB.Id);
        Assert.Equal(300m, snapB_preserved.OnHandQuantity);
        Assert.Equal(85m, snapB_preserved.ReservedQuantity);
    }

    [Fact]
    public async Task InventorySync_ConcurrentExecution_ThrowsInvalidOperationException()
    {
        using var context = CreateInMemoryDbContext();
        var wh = new Warehouse { Code = "WH-MAIN", Name = "انبار اصلی", SepidarWarehouseRef = "1" };
        context.Warehouses.Add(wh);
        await context.SaveChangesAsync();

        var slowTcs = new TaskCompletionSource<List<SepidarItemRecord>>();
        var blockingAdapter = new BlockingSepidarInventoryAdapter(slowTcs.Task);

        var calc = new ConsumptionCalculator(context);
        var engine = new PurchaseRecommendationEngine(context, calc);
        var syncService = new InventorySyncService(context, blockingAdapter, calc, engine, NullLogger<InventorySyncService>.Instance);

        // Launch Job 1 in background task - it will wait on slowTcs
        var job1Task = Task.Run(() => syncService.TriggerSyncAsync(SyncJobType.Incremental, "User1"));

        // Wait a short moment to ensure job1 has entered and acquired lock
        await Task.Delay(60);

        // Attempt to launch Job 2 concurrently - MUST throw InvalidOperationException!
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await syncService.TriggerSyncAsync(SyncJobType.Incremental, "User2");
        });

        Assert.Contains("Multiple overlapping jobs are forbidden", ex.Message);

        // Now release Job 1
        slowTcs.SetResult(new List<SepidarItemRecord>());
        var job1Result = await job1Task;
        Assert.Equal("Success", job1Result.Status);
    }

    [Fact]
    public async Task PurchaseRecommendationEngine_MidRunFailure_HaltsCalculationAndPreventsPartialDataExposure()
    {
        using var context = CreateInMemoryDbContext();
        var wh = new Warehouse { Code = "WH-MAIN", Name = "انبار اصلی" };
        var cat = new Category { Code = "CAT-RAW", Name = "مواد اولیه" };
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم" };
        var mat = new Material { InternalCode = "MAT-1", Name = "کالای تستی", CategoryId = cat.Id, BaseUnitId = kg.Id };

        context.Warehouses.Add(wh);
        context.Categories.Add(cat);
        context.UnitsOfMeasure.Add(kg);
        context.Materials.Add(mat);

        // Simulate a failed sync job in the database
        var failedJob = new InventorySyncJob
        {
            JobType = SyncJobType.Incremental,
            Status = SyncJobStatus.Failed,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            CompletedAtUtc = DateTime.UtcNow.AddMinutes(-4),
            ErrorMessage = "Network connection to Sepidar ERP interrupted mid-run."
        };
        context.InventorySyncJobs.Add(failedJob);
        await context.SaveChangesAsync();

        var calc = new ConsumptionCalculator(context);
        var engine = new PurchaseRecommendationEngine(context, calc);

        // Attempting to calculate recommendations MUST throw InvalidOperationException
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await engine.GenerateAllRecommendationsAsync(wh.Id);
        });

        Assert.Contains("وضعیت آخرین اجرای همگام‌سازی انبار نامعتبر است", ex.Message);

        // Now simulate that the issue was resolved and a subsequent sync completed successfully
        var successfulJob = new InventorySyncJob
        {
            JobType = SyncJobType.Incremental,
            Status = SyncJobStatus.Success,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-2),
            CompletedAtUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        context.InventorySyncJobs.Add(successfulJob);
        await context.SaveChangesAsync();

        // Engine should now safely proceed
        var recs = await engine.GenerateAllRecommendationsAsync(wh.Id);
        Assert.NotNull(recs);
    }

    [Fact]
    public async Task PurchaseOrderService_DirectIssue_BlockedWhenReserveUnprovenUnlessManualReviewConfirmed()
    {
        using var context = CreateInMemoryDbContext();
        var wh = new Warehouse { Code = "WH-MAIN", Name = "انبار اصلی" };
        var supplier = new Party { DisplayName = "تأمین‌کننده آرد", PartyType = PartyType.LegalEntity, IsActive = true };
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم" };
        var cat = new Category { Code = "CAT-RAW", Name = "مواد اولیه" };
        var mat = new Material { InternalCode = "MAT-FLOUR", Name = "آرد گندم", CategoryId = cat.Id, BaseUnitId = kg.Id };

        context.Warehouses.Add(wh);
        context.Parties.Add(supplier);
        context.UnitsOfMeasure.Add(kg);
        context.Categories.Add(cat);
        context.Materials.Add(mat);

        // Snapshot with OnHand = 5000kg, but Reservable = 0kg (Unproven reserve in Sepidar!)
        context.InventorySnapshots.Add(new InventorySnapshot
        {
            WarehouseId = wh.Id,
            MaterialId = mat.Id,
            OnHandQuantity = 5000m,
            ReservableQuantity = 0m,
            IncomingQuantity = 0m,
            ReservedQuantity = 0m,
            UnitId = kg.Id
        });
        await context.SaveChangesAsync();

        var poService = new PurchaseOrderService(context);

        var unconfirmedReq = new CreatePurchaseOrderRequest
        {
            SupplierId = supplier.Id,
            WarehouseId = wh.Id,
            DirectIssue = true,
            ManualReviewConfirmed = false, // NOT confirmed!
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { MaterialId = mat.Id, OrderedQuantity = 1000m, UnitPrice = 150000m }
            }
        };

        // Must throw InvalidOperationException
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await poService.CreateOrderAsync(unconfirmedReq, "ProcurementUser");
        });

        Assert.Contains("برای اقلام دارای وضعیت رزرو نامعلوم در سپیدار، بدون تأیید صریح بررسی دستی", ex.Message);

        // When manual review is explicitly confirmed, direct issuance is permitted
        var confirmedReq = new CreatePurchaseOrderRequest
        {
            SupplierId = supplier.Id,
            WarehouseId = wh.Id,
            DirectIssue = true,
            ManualReviewConfirmed = true, // Confirmed after manual kardex review!
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { MaterialId = mat.Id, OrderedQuantity = 1000m, UnitPrice = 150000m }
            }
        };

        var created = await poService.CreateOrderAsync(confirmedReq, "ProcurementUser");
        Assert.NotNull(created);
        Assert.Equal("Issued", created.Status);
    }

    [Fact]
    public async Task PurchaseOrderService_DraftCreation_BlockedWhenReserveUnprovenUnlessManualReviewConfirmed()
    {
        using var context = CreateInMemoryDbContext();
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم" };
        var cat = new Category { Code = "CAT-RAW", Name = "مواد اولیه" };
        var wh = new Warehouse { Code = "WH-MAIN", Name = "انبار اصلی" };
        var mat = new Material { InternalCode = "MAT-UNPROVEN", Name = "کالای با رزرو نامعلوم", CategoryId = cat.Id, BaseUnitId = kg.Id };
        var supplier = new Party { DisplayName = "تأمین‌کننده آرد", PartyType = PartyType.LegalEntity, IsActive = true };

        context.UnitsOfMeasure.Add(kg);
        context.Categories.Add(cat);
        context.Warehouses.Add(wh);
        context.Materials.Add(mat);
        context.Parties.Add(supplier);

        // Snapshot with unproven reserve (OnHand > 0, Reservable == 0)
        context.InventorySnapshots.Add(new InventorySnapshot
        {
            WarehouseId = wh.Id,
            MaterialId = mat.Id,
            OnHandQuantity = 10000m,
            ReservableQuantity = 0m,
            UnitId = kg.Id
        });
        await context.SaveChangesAsync();

        var poService = new PurchaseOrderService(context);

        // Attempt to create a Draft order (DirectIssue = false) without manual review confirmation
        var draftReq = new CreatePurchaseOrderRequest
        {
            SupplierId = supplier.Id,
            WarehouseId = wh.Id,
            DirectIssue = false,
            ManualReviewConfirmed = false, // Not confirmed!
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { MaterialId = mat.Id, OrderedQuantity = 2000m, UnitPrice = 50000m }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await poService.CreateOrderAsync(draftReq, "BuyerUser");
        });

        Assert.Contains("ایجاد سفارش خرید (اعم از پیش‌نویس یا صدور مستقیم) برای اقلام دارای وضعیت رزرو نامعلوم در سپیدار، بدون تأیید صریح بررسی دستی", ex.Message);
    }

    [Fact]
    public async Task PurchaseOrderService_IssueDraftOrder_BlockedWhenReserveUnprovenUnlessManualReviewConfirmed()
    {
        using var context = CreateInMemoryDbContext();
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم" };
        var cat = new Category { Code = "CAT-RAW", Name = "مواد اولیه" };
        var wh = new Warehouse { Code = "WH-MAIN", Name = "انبار اصلی" };
        var mat = new Material { InternalCode = "MAT-EGG", Name = "تخم‌مرغ", CategoryId = cat.Id, BaseUnitId = kg.Id };
        var supplier = new Party { DisplayName = "مرغداری سحر", PartyType = PartyType.LegalEntity, IsActive = true };

        context.UnitsOfMeasure.Add(kg);
        context.Categories.Add(cat);
        context.Warehouses.Add(wh);
        context.Materials.Add(mat);
        context.Parties.Add(supplier);

        // Snapshot with unproven reserve
        context.InventorySnapshots.Add(new InventorySnapshot
        {
            WarehouseId = wh.Id,
            MaterialId = mat.Id,
            OnHandQuantity = 67811.92m,
            ReservableQuantity = 0m,
            UnitId = kg.Id
        });

        // Draft PO created with initial confirmation
        var po = new PurchaseOrder
        {
            OrderNumber = "PO-202610-0099",
            SupplierId = supplier.Id,
            WarehouseId = wh.Id,
            Status = PurchaseOrderStatus.Draft,
            ConcurrencyToken = "TOKEN-INIT-123",
            Lines = new List<PurchaseOrderLine>
            {
                new() { MaterialId = mat.Id, OrderedQuantity = 500m, UnitPrice = 120000m, UnitId = kg.Id }
            }
        };
        context.PurchaseOrders.Add(po);
        await context.SaveChangesAsync();

        var poService = new PurchaseOrderService(context);

        // Attempt to issue draft order without manualReviewConfirmed = false
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await poService.IssueOrderAsync(po.Id, "ManagerUser", "TOKEN-INIT-123", manualReviewConfirmed: false);
        });

        Assert.Contains("صدور و تأیید این سفارش به دلیل وجود اقلام با وضعیت رزرو نامعلوم در سپیدار، نیازمند تأیید صریح بررسی دستی است", ex.Message);

        // With manualReviewConfirmed = true, issuing succeeds
        var issued = await poService.IssueOrderAsync(po.Id, "ManagerUser", "TOKEN-INIT-123", manualReviewConfirmed: true);
        Assert.NotNull(issued);
        Assert.Equal("Issued", issued.Status);
    }

    [Fact]
    public async Task InventorySync_MovementDeduplication_MatchesBothRawNumericAndPrefixedKeys()
    {
        using var context = CreateInMemoryDbContext();
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم" };
        var cat = new Category { Code = "CAT-RAW", Name = "مواد اولیه" };
        var wh = new Warehouse { Code = "WH-MAIN", Name = "انبار اصلی", SepidarWarehouseRef = "1" };
        var mat = new Material { InternalCode = "404229", Name = "تخم‌مرغ", CategoryId = cat.Id, BaseUnitId = kg.Id };

        context.UnitsOfMeasure.Add(kg);
        context.Categories.Add(cat);
        context.Warehouses.Add(wh);
        context.Materials.Add(mat);
        context.InventoryItemMappings.Add(new InventoryItemMapping
        {
            MaterialId = mat.Id,
            SepidarItemId = "1001",
            SepidarItemCode = "404229",
            SepidarBaseUnit = "KG"
        });

        // Existing movement in CRM with legacy raw numeric key '45168'
        var existingMov = new InventoryMovement
        {
            WarehouseId = wh.Id,
            MaterialId = mat.Id,
            VoucherNumber = "158",
            SourceMovementKey = "45168", // Raw numeric key without prefix!
            MovementType = InventoryMovementType.Receipt,
            Quantity = 360m,
            UnitId = kg.Id,
            PostingStatus = 2
        };
        context.InventoryMovements.Add(existingMov);
        await context.SaveChangesAsync();

        // Sync brings voucher with 'REC-45168' (with prefix) and PostingStatus = 4 (updated)
        var adapter = new ControllableSepidarInventoryAdapter(
            new List<SepidarItemRecord> { new("1001", "404229", "تخم‌مرغ", "KG", "کیلوگرم", true) },
            new List<SepidarStockRecord> { new("1001", "1", 360m, null, null, 0m, "KG", DateTime.UtcNow) },
            new List<SepidarMovementRecord>
            {
                new("REC-45168", "158", "1001", "1", InventoryMovementType.Receipt, 360m, "KG", DateTime.UtcNow, 4, false, "Ref1")
            });

        var calc = new ConsumptionCalculator(context);
        var engine = new PurchaseRecommendationEngine(context, calc);
        var syncService = new InventorySyncService(context, adapter, calc, engine, NullLogger<InventorySyncService>.Instance);

        var job = await syncService.TriggerSyncAsync(SyncJobType.Full, "DeduplicationTest");
        Assert.Equal("Success", job.Status);

        // Verify: Did NOT insert a duplicate movement; updated existing movement posting status to 4
        var allMovements = await context.InventoryMovements.ToListAsync();
        Assert.Single(allMovements);
        Assert.Equal("45168", allMovements[0].SourceMovementKey);
        Assert.Equal(4, allMovements[0].PostingStatus); // Updated from 2 to 4!
    }

    [Fact]
    public async Task InventorySync_SnapshotSync_PreservesCrmReservedAndIncomingWhenSepidarDoesNotOwnThem()
    {
        using var context = CreateInMemoryDbContext();
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم" };
        var cat = new Category { Code = "CAT-RAW", Name = "مواد اولیه" };
        var wh = new Warehouse { Code = "WH-MAIN", Name = "انبار اصلی", SepidarWarehouseRef = "1" };
        var mat = new Material { InternalCode = "404016", Name = "خمیر منجمد", CategoryId = cat.Id, BaseUnitId = kg.Id };

        context.UnitsOfMeasure.Add(kg);
        context.Categories.Add(cat);
        context.Warehouses.Add(wh);
        context.Materials.Add(mat);
        context.InventoryItemMappings.Add(new InventoryItemMapping
        {
            MaterialId = mat.Id,
            SepidarItemId = "2001",
            SepidarItemCode = "404016",
            SepidarBaseUnit = "KG"
        });

        // Snapshot in CRM already has internal production ReservedQuantity = 11000m and PO IncomingQuantity = 500m
        var snap = new InventorySnapshot
        {
            WarehouseId = wh.Id,
            MaterialId = mat.Id,
            OnHandQuantity = 11000m,
            ReservableQuantity = 0m,
            ReservedQuantity = 11000m, // Work order reservation in CRM!
            IncomingQuantity = 500m,   // PO in progress in CRM!
            UnitId = kg.Id
        };
        context.InventorySnapshots.Add(snap);
        await context.SaveChangesAsync();

        // Sepidar stock balance has stock.Reserved = null and stock.Incoming = 0 (Sepidar does not own these!)
        var adapter = new ControllableSepidarInventoryAdapter(
            new List<SepidarItemRecord> { new("2001", "404016", "خمیر منجمد", "KG", "کیلوگرم", true) },
            new List<SepidarStockRecord> { new("2001", "1", 11000m, null, null, 0m, "KG", DateTime.UtcNow) });

        var calc = new ConsumptionCalculator(context);
        var engine = new PurchaseRecommendationEngine(context, calc);
        var syncService = new InventorySyncService(context, adapter, calc, engine, NullLogger<InventorySyncService>.Instance);

        var job = await syncService.TriggerSyncAsync(SyncJobType.Full, "OwnershipPreservationTest");
        Assert.Equal("Success", job.Status);

        // Verify: CRM's internal ReservedQuantity (11000) and IncomingQuantity (500) were NOT wiped out!
        var updatedSnap = await context.InventorySnapshots.FirstAsync();
        Assert.Equal(11000m, updatedSnap.OnHandQuantity);
        Assert.Equal(11000m, updatedSnap.ReservedQuantity); // PRESERVED!
        Assert.Equal(500m, updatedSnap.IncomingQuantity);     // PRESERVED!
    }

    [Fact]
    public async Task InventorySync_ReceiptAndDelivery_WithSameNumericId_BothCorrectlyDistinguished()
    {
        using var context = CreateInMemoryDbContext();
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم", Dimension = "Mass" };
        var cat = new Category { Code = "CAT-RAW", Name = "مواد اولیه" };
        var wh = new Warehouse { Code = "WH-MAIN", Name = "انبار اصلی", SepidarWarehouseRef = "1" };
        var mat = new Material { InternalCode = "MAT-100", Name = "شکر سفید", CategoryId = cat.Id, BaseUnitId = kg.Id };

        context.UnitsOfMeasure.Add(kg);
        context.Categories.Add(cat);
        context.Warehouses.Add(wh);
        context.Materials.Add(mat);
        context.InventoryItemMappings.Add(new InventoryItemMapping
        {
            MaterialId = mat.Id,
            SepidarItemId = "3001",
            SepidarItemCode = "MAT-100",
            SepidarBaseUnit = "KG"
        });

        // Existing legacy movement in CRM: A RECEIPT with numeric key "500"
        var legacyReceipt = new InventoryMovement
        {
            WarehouseId = wh.Id,
            MaterialId = mat.Id,
            VoucherNumber = "REC-V-1",
            SourceMovementKey = "500", // Raw numeric key
            MovementType = InventoryMovementType.Receipt,
            Quantity = 200m,
            UnitId = kg.Id,
            PostingStatus = 2
        };
        context.InventoryMovements.Add(legacyReceipt);
        await context.SaveChangesAsync();

        // Sepidar sends:
        // 1. Receipt with VoucherRef = "REC-500" (matches legacy receipt "500")
        // 2. Delivery with VoucherRef = "DEL-500" (SAME numeric ID, but Outbound Consumption; must NOT match receipt "500"!)
        var adapter = new ControllableSepidarInventoryAdapter(
            new List<SepidarItemRecord> { new("3001", "MAT-100", "شکر سفید", "KG", "کیلوگرم", true) },
            new List<SepidarStockRecord> { new("3001", "1", 200m, null, null, 0m, "KG", DateTime.UtcNow) },
            new List<SepidarMovementRecord>
            {
                new("REC-500", "REC-V-1", "3001", "1", InventoryMovementType.Receipt, 200m, "KG", DateTime.UtcNow, 4, false, "Receipt 500"),
                new("DEL-500", "DEL-V-1", "3001", "1", InventoryMovementType.Consumption, 50m, "KG", DateTime.UtcNow, 4, false, "Delivery 500")
            });

        var calc = new ConsumptionCalculator(context);
        var engine = new PurchaseRecommendationEngine(context, calc);
        var syncService = new InventorySyncService(context, adapter, calc, engine, NullLogger<InventorySyncService>.Instance);

        var job = await syncService.TriggerSyncAsync(SyncJobType.Full, "SameIdTest");
        Assert.Equal("Success", job.Status);

        var allMovements = await context.InventoryMovements.ToListAsync();
        // Total movements should be 2: the updated legacy receipt, and the newly inserted delivery!
        Assert.Equal(2, allMovements.Count);

        var receiptMov = allMovements.First(m => m.MovementType == InventoryMovementType.Receipt);
        Assert.Equal("500", receiptMov.SourceMovementKey);
        Assert.Equal(4, receiptMov.PostingStatus); // Updated from 2 to 4!

        var deliveryMov = allMovements.First(m => m.MovementType == InventoryMovementType.Consumption);
        Assert.Equal("DEL-500", deliveryMov.SourceMovementKey); // Inserted as NEW delivery!
        Assert.Equal(50m, deliveryMov.Quantity);
    }

    [Theory]
    [InlineData(500.0)]   // Positive OnHand
    [InlineData(0.0)]     // Zero OnHand
    [InlineData(-10.0)]   // Negative OnHand
    public async Task PurchaseOrder_ManualReviewRequired_WhenReserveUnproven_WithZeroPositiveAndNegativeOnHand(double onHandVal)
    {
        using var context = CreateInMemoryDbContext();
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم", Dimension = "Mass" };
        var cat = new Category { Code = "CAT-RAW", Name = "مواد اولیه" };
        var wh = new Warehouse { Code = "WH-MAIN", Name = "انبار اصلی" };
        var mat = new Material { InternalCode = $"MAT-{onHandVal}", Name = $"کالا موجودی {onHandVal}", CategoryId = cat.Id, BaseUnitId = kg.Id };
        var supplier = new Party { DisplayName = "تأمین‌کننده", PartyType = PartyType.LegalEntity, IsActive = true };

        context.UnitsOfMeasure.Add(kg);
        context.Categories.Add(cat);
        context.Warehouses.Add(wh);
        context.Materials.Add(mat);
        context.Parties.Add(supplier);

        // Snapshot with unproven reserve tag (:RES-UNPROVEN)
        context.InventorySnapshots.Add(new InventorySnapshot
        {
            WarehouseId = wh.Id,
            MaterialId = mat.Id,
            OnHandQuantity = (decimal)onHandVal,
            ReservableQuantity = 0m,
            UnitId = kg.Id,
            SourceSnapshotKey = $"WH-WH-MAIN-ITEM-1:RES-UNPROVEN" // Explicitly unproven
        });
        await context.SaveChangesAsync();

        var poService = new PurchaseOrderService(context);

        // 1. Draft creation blocked without manual review confirmation
        var draftReq = new CreatePurchaseOrderRequest
        {
            SupplierId = supplier.Id,
            WarehouseId = wh.Id,
            DirectIssue = false,
            ManualReviewConfirmed = false,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { MaterialId = mat.Id, OrderedQuantity = 100m, UnitPrice = 10000m }
            }
        };

        var exDraft = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await poService.CreateOrderAsync(draftReq, "BuyerUser");
        });
        Assert.Contains("ایجاد سفارش خرید (اعم از پیش‌نویس یا صدور مستقیم) برای اقلام دارای وضعیت رزرو نامعلوم در سپیدار، بدون تأیید صریح بررسی دستی", exDraft.Message);

        // 2. Draft creation succeeds when confirmed
        draftReq.ManualReviewConfirmed = true;
        var createdDraft = await poService.CreateOrderAsync(draftReq, "BuyerUser");
        Assert.NotNull(createdDraft);
        Assert.Equal("Draft", createdDraft.Status);

        // 3. Draft issuance blocked without manual review confirmation
        var exIssue = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await poService.IssueOrderAsync(createdDraft.Id, "ManagerUser", createdDraft.ConcurrencyToken, manualReviewConfirmed: false);
        });
        Assert.Contains("صدور و تأیید این سفارش به دلیل وجود اقلام با وضعیت رزرو نامعلوم در سپیدار، نیازمند تأیید صریح بررسی دستی است", exIssue.Message);

        // 4. Draft issuance succeeds when confirmed
        var issued = await poService.IssueOrderAsync(createdDraft.Id, "ManagerUser", createdDraft.ConcurrencyToken, manualReviewConfirmed: true);
        Assert.NotNull(issued);
        Assert.Equal("Issued", issued.Status);
    }

    [Fact]
    public async Task InventorySync_UnitMismatch_BlocksBothSnapshotOnHandAndMovements()
    {
        using var context = CreateInMemoryDbContext();
        var kg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم", Dimension = "Mass" };
        var countUnit = new UnitOfMeasure { Code = "COUNT", Name = "عدد", Dimension = "Count" };
        var cat = new Category { Code = "CAT-RAW", Name = "مواد اولیه" };
        var wh = new Warehouse { Code = "WH-MAIN", Name = "انبار اصلی", SepidarWarehouseRef = "1" };
        var mat = new Material { InternalCode = "404016", Name = "خمیر منجمد", CategoryId = cat.Id, BaseUnitId = kg.Id, BaseUnit = kg };

        context.UnitsOfMeasure.AddRange(kg, countUnit);
        context.Categories.Add(cat);
        context.Warehouses.Add(wh);
        context.Materials.Add(mat);
        context.InventoryItemMappings.Add(new InventoryItemMapping
        {
            MaterialId = mat.Id,
            SepidarItemId = "2001",
            SepidarItemCode = "404016",
            SepidarBaseUnit = "COUNT"
        });

        // Initial snapshot in CRM has OnHand = 100 KG
        var snap = new InventorySnapshot
        {
            WarehouseId = wh.Id,
            MaterialId = mat.Id,
            OnHandQuantity = 100m,
            ReservableQuantity = 100m,
            UnitId = kg.Id,
            IsStale = false
        };
        context.InventorySnapshots.Add(snap);
        await context.SaveChangesAsync();

        // Sepidar sends stock with unit "COUNT" (Dimension mismatch, no conversion rule!) and quantity 11000
        var adapter = new ControllableSepidarInventoryAdapter(
            new List<SepidarItemRecord> { new("2001", "404016", "خمیر منجمد", "COUNT", "عدد", true) },
            new List<SepidarStockRecord> { new("2001", "1", 11000m, null, null, 0m, "COUNT", DateTime.UtcNow) },
            new List<SepidarMovementRecord>
            {
                new("REC-9999", "V-9999", "2001", "1", InventoryMovementType.Receipt, 500m, "COUNT", DateTime.UtcNow, 4, false, "Receipt Count")
            });

        var calc = new ConsumptionCalculator(context);
        var engine = new PurchaseRecommendationEngine(context, calc);
        var syncService = new InventorySyncService(context, adapter, calc, engine, NullLogger<InventorySyncService>.Instance);

        var job = await syncService.TriggerSyncAsync(SyncJobType.Full, "UnitMismatchTest");
        Assert.Equal("Success", job.Status);

        // 1. Snapshot OnHandQuantity MUST NOT be overwritten by 11000!
        var afterSnap = await context.InventorySnapshots.FirstAsync();
        Assert.Equal(100m, afterSnap.OnHandQuantity); // Preserved! Incompatible 11000 was BLOCKED!
        Assert.True(afterSnap.IsStale); // Marked stale due to unit mismatch

        // 2. Movements for this item MUST NOT be imported into CRM!
        var movements = await context.InventoryMovements.ToListAsync();
        Assert.Empty(movements); // BLOCKED from CRM movements!
    }
}

public class MockSepidarInventoryAdapter : ISepidarInventoryAdapter
{
    private readonly List<SepidarItemRecord> _items;
    public MockSepidarInventoryAdapter(List<SepidarItemRecord> items) => _items = items;

    public bool IsLiveConnected => false;
    public string AdapterName => "MockSepidarInventoryAdapter";

    public Task<List<SepidarWarehouseRecord>> GetWarehousesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<SepidarWarehouseRecord> { new("1", "1", "مواد اولیه", true) });

    public Task<List<SepidarItemRecord>> GetItemsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_items);

    public Task<List<SepidarStockRecord>> GetStockBalancesAsync(string warehouseRef, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<SepidarStockRecord>());

    public Task<List<SepidarMovementRecord>> GetInventoryMovementsAsync(string warehouseRef, DateTime? sinceDateUtc = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<SepidarMovementRecord>());
}

public class ControllableSepidarInventoryAdapter : ISepidarInventoryAdapter
{
    private readonly List<SepidarItemRecord> _items;
    private readonly List<SepidarStockRecord> _stock;
    private readonly List<SepidarMovementRecord> _movements;
    private readonly bool _throwOnMovements;

    public ControllableSepidarInventoryAdapter(
        List<SepidarItemRecord> items,
        List<SepidarStockRecord> stock,
        List<SepidarMovementRecord>? movements = null,
        bool throwOnMovements = false)
    {
        _items = items;
        _stock = stock;
        _movements = movements ?? new List<SepidarMovementRecord>();
        _throwOnMovements = throwOnMovements;
    }

    public bool IsLiveConnected => false;
    public string AdapterName => "ControllableSepidarInventoryAdapter";

    public Task<List<SepidarWarehouseRecord>> GetWarehousesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<SepidarWarehouseRecord> { new("1", "1", "مواد اولیه", true) });

    public Task<List<SepidarItemRecord>> GetItemsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_items);

    public Task<List<SepidarStockRecord>> GetStockBalancesAsync(string warehouseRef, CancellationToken cancellationToken = default) =>
        Task.FromResult(_stock);

    public Task<List<SepidarMovementRecord>> GetInventoryMovementsAsync(string warehouseRef, DateTime? sinceDateUtc = null, CancellationToken cancellationToken = default)
    {
        if (_throwOnMovements)
        {
            throw new InvalidOperationException("Simulated mid-run database network disconnection during movement synchronization.");
        }
        return Task.FromResult(_movements);
    }
}

public class BlockingSepidarInventoryAdapter : ISepidarInventoryAdapter
{
    private readonly Task<List<SepidarItemRecord>> _blockingTask;
    public BlockingSepidarInventoryAdapter(Task<List<SepidarItemRecord>> blockingTask) => _blockingTask = blockingTask;

    public bool IsLiveConnected => false;
    public string AdapterName => "BlockingSepidarInventoryAdapter";

    public Task<List<SepidarWarehouseRecord>> GetWarehousesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<SepidarWarehouseRecord> { new("1", "1", "مواد اولیه", true) });

    public async Task<List<SepidarItemRecord>> GetItemsAsync(CancellationToken cancellationToken = default) =>
        await _blockingTask;

    public Task<List<SepidarStockRecord>> GetStockBalancesAsync(string warehouseRef, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<SepidarStockRecord>());

    public Task<List<SepidarMovementRecord>> GetInventoryMovementsAsync(string warehouseRef, DateTime? sinceDateUtc = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<SepidarMovementRecord>());
}
