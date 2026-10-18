using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
            Factor = 50m
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
}
