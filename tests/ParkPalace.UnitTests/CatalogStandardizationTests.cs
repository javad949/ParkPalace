using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Services;
using ParkPalace.Domain.Constants;
using Xunit;

namespace ParkPalace.UnitTests;

public class CatalogStandardizationTests
{
    private CrmDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new CrmDbContext(options);

        // Seed basic standard units
        context.UnitsOfMeasure.AddRange(
            new UnitOfMeasure { Id = Guid.NewGuid(), Code = "KG", Name = "کیلوگرم", Dimension = "Mass", Precision = 2 },
            new UnitOfMeasure { Id = Guid.NewGuid(), Code = "GR", Name = "گرم", Dimension = "Mass", Precision = 0 },
            new UnitOfMeasure { Id = Guid.NewGuid(), Code = "L", Name = "لیتر", Dimension = "Volume", Precision = 2 },
            new UnitOfMeasure { Id = Guid.NewGuid(), Code = "ML", Name = "میلی‌لیتر", Dimension = "Volume", Precision = 0 },
            new UnitOfMeasure { Id = Guid.NewGuid(), Code = "COUNT", Name = "عدد", Dimension = "Count", Precision = 0 }
        );
        context.SaveChanges();

        return context;
    }

    [Fact]
    public void Acceptance_Damdaran1L_Milk_ParsedAndStandardizedCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var engine = new CatalogStandardizationEngine(context, NullLogger<CatalogStandardizationEngine>.Instance);

        // Act - Raw title as received from Sepidar ERP: "شیر پاکتی دامداران-۱لیتری"
        var suggestion = engine.ParseAndStandardize("شیر پاکتی دامداران-۱لیتری", "COUNT");

        // Assert - Acceptance criteria 4
        suggestion.Should().NotBeNull();
        suggestion.StandardName.Should().Be("شیر پاکتی دامداران ۱ لیتری");
        suggestion.Brand.Should().Be("دامداران");
        suggestion.Packaging.Should().Be("پاکت");
        suggestion.ParentCategoryName.Should().Be("لبنیات");
        suggestion.SubcategoryName.Should().Be("شیر");
        suggestion.BaseUnitCode.Should().Be("COUNT"); // واحد شمارش
        suggestion.PurchaseUnitCode.Should().Be("COUNT"); // واحد خرید
        suggestion.ConsumptionUnitCode.Should().Be("L"); // واحد مصرف پیشنهادی: لیتر
        suggestion.PackageSize.Should().Be(1.0m); // محتویات هر عدد: ۱
        suggestion.PackageSizeUnitCode.Should().Be("L"); // لیتر
        suggestion.SuggestedConversionFactor.Should().Be(1.0m); // تبدیل عدد به لیتر
        suggestion.SuggestedConversionToUnit.Should().Be("L");
        suggestion.ProductFamily.Should().Be("شیر پاکتی دامداران");
        suggestion.Status.Should().Be("AutoConfirmed");

        // Safe conversion assertion: must NOT convert Liter to KG
        suggestion.SuggestedConversionToUnit.Should().NotBe("KG");
    }

    [Fact]
    public void Acceptance_Damdaran250ML_Milk_ParsedAndStandardizedCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var engine = new CatalogStandardizationEngine(context, NullLogger<CatalogStandardizationEngine>.Instance);

        // Act - Raw title: "شیر پاکتی دامداران ۲۵۰ سی‌سی"
        var suggestion = engine.ParseAndStandardize("شیر پاکتی دامداران ۲۵۰ سی‌سی", "COUNT");

        // Assert
        suggestion.Should().NotBeNull();
        suggestion.StandardName.Should().Be("شیر پاکتی دامداران ۲۵۰ سی‌سی");
        suggestion.Brand.Should().Be("دامداران");
        suggestion.Packaging.Should().Be("پاکت");
        suggestion.ParentCategoryName.Should().Be("لبنیات");
        suggestion.SubcategoryName.Should().Be("شیر");
        suggestion.BaseUnitCode.Should().Be("COUNT");
        suggestion.PurchaseUnitCode.Should().Be("COUNT");
        suggestion.ConsumptionUnitCode.Should().Be("L");
        suggestion.PackageSize.Should().Be(250m);
        suggestion.PackageSizeUnitCode.Should().Be("ML"); // سی‌سی استاندارد شده به ML
        suggestion.SuggestedConversionFactor.Should().Be(0.25m); // 250 ML = 0.25 L
        suggestion.SuggestedConversionToUnit.Should().Be("L");
        suggestion.ProductFamily.Should().Be("شیر پاکتی دامداران");
        suggestion.Status.Should().Be("AutoConfirmed");
    }

    [Fact]
    public async Task Acceptance_BothMilkSizes_RegisteredSimultaneously_CoexistAsIndependentItems_WithIndependentConversions()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var engine = new CatalogStandardizationEngine(context, NullLogger<CatalogStandardizationEngine>.Instance);

        // Create categories and units
        var dairyCat = await engine.EnsureUnifiedCategoryAsync("CAT-DAIRY", "لبنیات", "CAT-MILK", "شیر");
        var countUnit = await context.UnitsOfMeasure.FirstAsync(u => u.Code == "COUNT");
        var literUnit = await context.UnitsOfMeasure.FirstAsync(u => u.Code == "L");
        var mlUnit = await context.UnitsOfMeasure.FirstAsync(u => u.Code == "ML");

        // Register Item 1: 1L Milk
        var mat1L = new Material
        {
            Id = Guid.NewGuid(),
            InternalCode = "MAT-DAM-MILK-1L",
            Name = "شیر پاکتی دامداران ۱ لیتری",
            Brand = "دامداران",
            Packaging = "پاکت",
            PackageSize = 1.0m,
            PackageSizeUnitId = literUnit.Id,
            CategoryId = dairyCat.Id,
            BaseUnitId = countUnit.Id,
            PurchaseUnitId = countUnit.Id,
            ConsumptionUnitId = literUnit.Id,
            ProductFamily = "شیر پاکتی دامداران",
            CategorizationStatus = "AutoConfirmed",
            IsActive = true
        };
        mat1L.UnitConversions.Add(new MaterialUnitConversion
        {
            MaterialId = mat1L.Id,
            FromUnitId = countUnit.Id,
            ToUnitId = literUnit.Id,
            Factor = 1.0m,
            IsConfirmed = true,
            Notes = "هر عدد برابر ۱ لیتر"
        });
        context.Materials.Add(mat1L);

        var map1L = new InventoryItemMapping
        {
            Id = Guid.NewGuid(),
            MaterialId = mat1L.Id,
            SepidarItemId = "SEP-701",
            SepidarItemCode = "1001",
            SepidarItemTitle = "شیر پاکتی دامداران-۱لیتری",
            SepidarBaseUnit = "عددی",
            IsActive = true,
            LastSyncUtc = DateTime.UtcNow
        };
        context.InventoryItemMappings.Add(map1L);

        // Register Item 2: 250 ML Milk
        var mat250ML = new Material
        {
            Id = Guid.NewGuid(),
            InternalCode = "MAT-DAM-MILK-250ML",
            Name = "شیر پاکتی دامداران ۲۵۰ سی‌سی",
            Brand = "دامداران",
            Packaging = "پاکت",
            PackageSize = 250m,
            PackageSizeUnitId = mlUnit.Id,
            CategoryId = dairyCat.Id,
            BaseUnitId = countUnit.Id,
            PurchaseUnitId = countUnit.Id,
            ConsumptionUnitId = literUnit.Id,
            ProductFamily = "شیر پاکتی دامداران",
            CategorizationStatus = "AutoConfirmed",
            IsActive = true
        };
        mat250ML.UnitConversions.Add(new MaterialUnitConversion
        {
            MaterialId = mat250ML.Id,
            FromUnitId = countUnit.Id,
            ToUnitId = literUnit.Id,
            Factor = 0.25m,
            IsConfirmed = true,
            Notes = "هر عدد برابر ۰.۲۵ لیتر"
        });
        context.Materials.Add(mat250ML);

        var map250ML = new InventoryItemMapping
        {
            Id = Guid.NewGuid(),
            MaterialId = mat250ML.Id,
            SepidarItemId = "SEP-702",
            SepidarItemCode = "1002",
            SepidarItemTitle = "شیر پاکتی دامداران ۲۵۰ سی‌سی",
            SepidarBaseUnit = "عددی",
            IsActive = true,
            LastSyncUtc = DateTime.UtcNow
        };
        context.InventoryItemMappings.Add(map250ML);

        await context.SaveChangesAsync();

        // Assert - Both items exist independently
        var allDairy = await context.Materials
            .Include(m => m.Category)
            .Include(m => m.PackageSizeUnit)
            .Include(m => m.UnitConversions)
            .Where(m => m.CategoryId == dairyCat.Id)
            .ToListAsync();

        allDairy.Should().HaveCount(2, "Both milk packages must be stored as separate catalog materials");
        allDairy.Select(m => m.Id).Distinct().Should().HaveCount(2);
        allDairy.Select(m => m.InternalCode).Distinct().Should().HaveCount(2);

        // Shared attributes
        allDairy.Select(m => m.Brand).Distinct().Should().ContainSingle().Which.Should().Be("دامداران");
        allDairy.Select(m => m.ProductFamily).Distinct().Should().ContainSingle().Which.Should().Be("شیر پاکتی دامداران");
        allDairy.Select(m => m.CategoryId).Distinct().Should().ContainSingle().Which.Should().Be(dairyCat.Id);

        // Independent Package Sizes
        var item1 = allDairy.First(m => m.PackageSize == 1.0m);
        item1.PackageSizeUnit!.Code.Should().Be("L");
        var conv1 = item1.UnitConversions.First(c => c.ToUnitId == literUnit.Id);
        conv1.Factor.Should().Be(1.0m);

        var item2 = allDairy.First(m => m.PackageSize == 250m);
        item2.PackageSizeUnit!.Code.Should().Be("ML");
        var conv2 = item2.UnitConversions.First(c => c.ToUnitId == literUnit.Id);
        conv2.Factor.Should().Be(0.25m);

        // Verify volume calculations: 10 units of 1L = 10L, 20 units of 250ML = 5L
        decimal volume1 = 10 * conv1.Factor; // 10 L
        decimal volume2 = 20 * conv2.Factor; // 5 L
        volume1.Should().Be(10.0m);
        volume2.Should().Be(5.0m);
    }

    [Fact]
    public void AmbiguousItem_WithoutSizeOrClearCategory_MarkedNeedsReview_WithoutGuessing()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var engine = new CatalogStandardizationEngine(context, NullLogger<CatalogStandardizationEngine>.Instance);

        // Act - Item with ambiguous title and no size
        var suggestion = engine.ParseAndStandardize("مواد افزودنی متفرقه");

        // Assert
        suggestion.Status.Should().Be("NeedsReview");
        suggestion.PackageSize.Should().BeNull();
        suggestion.PackageSizeUnitCode.Should().BeNull();
        suggestion.Explanation.Should().Contain("نیازمند بررسی");
    }

    [Fact]
    public async Task UnifiedCategoryTaxonomy_PreventsDuplicateCategories()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var engine = new CatalogStandardizationEngine(context, NullLogger<CatalogStandardizationEngine>.Instance);

        // Act - Call EnsureUnifiedCategory twice with same codes
        var cat1 = await engine.EnsureUnifiedCategoryAsync("CAT-DAIRY", "لبنیات", "CAT-MILK", "شیر");
        var cat2 = await engine.EnsureUnifiedCategoryAsync("CAT-DAIRY", "لبنیات", "CAT-MILK", "شیر");

        // Assert - Same category returned, no duplicate records created
        cat1.Id.Should().Be(cat2.Id);

        var allMilkCats = await context.Categories.Where(c => c.Code == "CAT-MILK").ToListAsync();
        allMilkCats.Should().HaveCount(1);

        var allDairyCats = await context.Categories.Where(c => c.Code == "CAT-DAIRY").ToListAsync();
        allDairyCats.Should().HaveCount(1);
    }

    [Fact]
    public async Task InventorySync_PreservesManualUserCustomizations_NeverOverwritesWithSepidarRawData()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var engine = new CatalogStandardizationEngine(context, NullLogger<CatalogStandardizationEngine>.Instance);

        var catCustom = new Category { Id = Guid.NewGuid(), Code = "CAT-SPECIAL-DAIRY", Name = "لبنیات خاص کارخانه" };
        var unitCount = await context.UnitsOfMeasure.FirstAsync(u => u.Code == "COUNT");
        var unitLiter = await context.UnitsOfMeasure.FirstAsync(u => u.Code == "L");
        context.Categories.Add(catCustom);

        var customizedMaterial = new Material
        {
            Id = Guid.NewGuid(),
            InternalCode = "MAT-CUSTOM-001",
            Name = "شیر صنعتی پاستوریزه سفارشی سرآشپز", // Manually customized standard name
            Brand = "دامداران",
            Packaging = "پاکت",
            PackageSize = 1.0m,
            PackageSizeUnitId = unitLiter.Id,
            CategoryId = catCustom.Id, // Manually customized category
            BaseUnitId = unitCount.Id, // Manually customized unit
            IsCustomizedByUser = true, // User manual customization flag
            CategorizationStatus = "ManualConfirmed",
            IsActive = true
        };
        context.Materials.Add(customizedMaterial);

        var mapping = new InventoryItemMapping
        {
            Id = Guid.NewGuid(),
            MaterialId = customizedMaterial.Id,
            SepidarItemId = "SEP-888",
            SepidarItemCode = "RAW-888",
            SepidarItemTitle = "شیر خام درجه ۲ نامشخص سپیدار", // Raw ugly title in Sepidar
            SepidarBaseUnit = "KG", // Mismatched raw unit in Sepidar
            IsActive = true,
            LastSyncUtc = DateTime.UtcNow.AddDays(-1)
        };
        context.InventoryItemMappings.Add(mapping);
        await context.SaveChangesAsync();

        // Mock Sepidar Adapter returning raw unstandardized data
        var rawItems = new List<SepidarItemRecord>
        {
            new SepidarItemRecord("SEP-888", "RAW-888", "شیر خام درجه ۲ نامشخص سپیدار", "KG", "کیلوگرم", true)
        };
        var mockAdapter = new MockSepidarInventoryAdapter(rawItems);
        var calc = new ConsumptionCalculator(context);
        var recEngine = new PurchaseRecommendationEngine(context, calc);

        var syncService = new InventorySyncService(
            context,
            mockAdapter,
            calc,
            recEngine,
            NullLogger<InventorySyncService>.Instance,
            engine);

        // Act - Run full Sync
        var job = await syncService.TriggerSyncAsync(SyncJobType.Full, "TestUser");

        // Assert - Sync job completed
        job.Status.Should().Be("Success");

        // Reload material and mapping from database
        var refreshedMat = await context.Materials.FindAsync(customizedMaterial.Id);
        var refreshedMap = await context.InventoryItemMappings.FindAsync(mapping.Id);

        // Verification 1: Mapping holds raw Sepidar info
        refreshedMap!.SepidarItemTitle.Should().Be("شیر خام درجه ۲ نامشخص سپیدار");
        refreshedMap.SepidarBaseUnit.Should().Be("KG");

        // Verification 2: Standardized CRM catalog attributes were NOT overwritten!
        refreshedMat!.Name.Should().Be("شیر صنعتی پاستوریزه سفارشی سرآشپز");
        refreshedMat.CategoryId.Should().Be(catCustom.Id);
        refreshedMat.BaseUnitId.Should().Be(unitCount.Id);
        refreshedMat.IsCustomizedByUser.Should().BeTrue();
        refreshedMat.CategorizationStatus.Should().Be("ManualConfirmed");
    }

    [Fact]
    public async Task ValidateBaseUnitChange_RejectsChangeWithoutConfirmedConversion_WhenMaterialHasOperationalHistory()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var engine = new CatalogStandardizationEngine(context, NullLogger<CatalogStandardizationEngine>.Instance);

        var countUnit = await context.UnitsOfMeasure.FirstAsync(u => u.Code == "COUNT");
        var kgUnit = await context.UnitsOfMeasure.FirstAsync(u => u.Code == "KG");

        var material = new Material
        {
            Id = Guid.NewGuid(),
            InternalCode = "MAT-OPERATIONAL",
            Name = "کالای دارای سابقه انبار",
            BaseUnitId = countUnit.Id
        };
        context.Materials.Add(material);

        // Add operational stock snapshot (OnHand > 0)
        context.InventorySnapshots.Add(new InventorySnapshot
        {
            Id = Guid.NewGuid(),
            MaterialId = material.Id,
            WarehouseId = Guid.NewGuid(),
            OnHandQuantity = 150m
        });
        await context.SaveChangesAsync();

        // Act 1 - Attempt to change BaseUnitId to KG: must be strictly blocked due to operational history
        var (isAllowed, errorMessage) = await engine.ValidateBaseUnitChangeAsync(material.Id, kgUnit.Id);

        // Assert - Must be strictly blocked per Requirement 5
        isAllowed.Should().BeFalse();
        errorMessage.Should().Contain("سابقه گردش کاردکس");
        errorMessage.Should().Contain("تغییر مستقیم واحد مبنا برای اقلام دارای سابقه مسدود است");

        // Act 2 - Add a confirmed conversion between COUNT and KG
        context.MaterialUnitConversions.Add(new MaterialUnitConversion
        {
            Id = Guid.NewGuid(),
            MaterialId = material.Id,
            FromUnitId = countUnit.Id,
            ToUnitId = kgUnit.Id,
            Factor = 1.0m,
            IsConfirmed = true
        });
        await context.SaveChangesAsync();

        // Per Requirement 5: Even with confirmed conversion, direct BaseUnit change on item with history remains blocked!
        var (isAllowedAfterConversion, errorAfter) = await engine.ValidateBaseUnitChangeAsync(material.Id, kgUnit.Id);
        isAllowedAfterConversion.Should().BeFalse("Direct base unit change remains blocked for items with operational history until dedicated historical migration workflow is run.");
        errorAfter.Should().Contain("تغییر مستقیم واحد مبنا برای اقلام دارای سابقه مسدود است");

        // Act 3 - Retaining the same base unit (while editing name/category/packaging) must be permitted
        var (isAllowedSameUnit, _) = await engine.ValidateBaseUnitChangeAsync(material.Id, countUnit.Id);
        isAllowedSameUnit.Should().BeTrue("Editing other attributes while keeping base unit is always allowed.");
    }

    [Fact]
    public async Task GenerateSepidarCorrectionProposals_OutputsProposalsWithoutMutatingSepidar()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var engine = new CatalogStandardizationEngine(context, NullLogger<CatalogStandardizationEngine>.Instance);

        var dairyCat = await engine.EnsureUnifiedCategoryAsync("CAT-DAIRY", "لبنیات", "CAT-MILK", "شیر");
        var countUnit = await context.UnitsOfMeasure.FirstAsync(u => u.Code == "COUNT");
        var literUnit = await context.UnitsOfMeasure.FirstAsync(u => u.Code == "L");

        var material = new Material
        {
            Id = Guid.NewGuid(),
            InternalCode = "1001",
            Name = "شیر پاکتی دامداران ۱ لیتری", // Standard CRM name
            Brand = "دامداران",
            Packaging = "پاکت",
            PackageSize = 1.0m,
            PackageSizeUnitId = literUnit.Id,
            CategoryId = dairyCat.Id,
            BaseUnitId = countUnit.Id,
            PurchaseUnitId = countUnit.Id,
            ConsumptionUnitId = literUnit.Id
        };
        context.Materials.Add(material);

        var mapping = new InventoryItemMapping
        {
            Id = Guid.NewGuid(),
            MaterialId = material.Id,
            SepidarItemId = "701",
            SepidarItemCode = "1001",
            SepidarItemTitle = "شیر پاکتی دامداران-۱لیتری", // Raw Sepidar title
            SepidarBaseUnit = "عددی", // Raw Sepidar unit
            IsActive = true
        };
        context.InventoryItemMappings.Add(mapping);
        await context.SaveChangesAsync();

        // Act - Generate proposals
        var proposals = await engine.GenerateSepidarCorrectionProposalsAsync();

        // Assert
        proposals.Should().HaveCount(1);
        var p = proposals[0];
        p.SepidarItemId.Should().Be("701");
        p.SepidarItemCode.Should().Be("1001");
        p.CurrentSepidarTitle.Should().Be("شیر پاکتی دامداران-۱لیتری");
        p.ProposedStandardName.Should().Be("شیر پاکتی دامداران ۱ لیتری");
        p.CurrentSepidarUnit.Should().Be("عددی");
        p.ProposedPurchaseUnit.Should().Be("عدد");
        p.PackageSize.Should().Be(1.0m);
        p.PackageSizeUnit.Should().Be("لیتر");
        p.ProposedCategoryName.Should().Be("شیر");
        p.Justification.Should().Contain("استانداردسازی کاتالوگ CRM");
    }

    [Fact]
    public async Task UnconfirmedConversion_IsNotActivatedInConsumptionCalculations_UntilConfirmed()
    {
        // Requirement 4: Extracted conversion starts as a suggestion and must NOT activate in consumption/inventory until confirmed.
        using var context = CreateInMemoryContext();

        var unitCount = await context.UnitsOfMeasure.FirstAsync(u => u.Code == "COUNT");
        var unitLiter = await context.UnitsOfMeasure.FirstAsync(u => u.Code == "L");

        var material = new Material
        {
            Id = Guid.NewGuid(),
            InternalCode = "MAT-MILK-UNCONFIRMED",
            Name = "شیر پاکتی دامداران ۲۵۰ سی‌سی",
            BaseUnitId = unitLiter.Id, // Base unit is Liter in factory
            IsActive = true
        };
        context.Materials.Add(material);

        // Add UNCONFIRMED conversion: 1 Count = 0.25 Liter (suggestion from package size)
        var unconfirmedConv = new MaterialUnitConversion
        {
            Id = Guid.NewGuid(),
            MaterialId = material.Id,
            FromUnitId = unitCount.Id,
            ToUnitId = unitLiter.Id,
            Factor = 0.25m,
            IsConfirmed = false, // Suggestion only!
            Notes = "پیشنهاد اولیه ۲۵۰ سی‌سی"
        };
        context.MaterialUnitConversions.Add(unconfirmedConv);

        // Add a consumption movement in COUNT (40 units)
        var now = DateTime.UtcNow;
        context.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(),
            MaterialId = material.Id,
            WarehouseId = Guid.NewGuid(),
            MovementType = InventoryMovementType.Consumption,
            UnitId = unitCount.Id, // movement recorded in Count
            Quantity = 40m,
            PostingStatus = 4, // Posted
            IsCancelled = false,
            TransactionDateUtc = now.AddDays(-2)
        });
        await context.SaveChangesAsync();

        var calculator = new ConsumptionCalculator(context);

        // Act 1: Calculate consumption with UNCONFIRMED conversion
        var resultBeforeConfirm = await calculator.CalculateConsumptionAsync(material.Id, now);

        // Assert 1: Because conversion is unconfirmed, factor 0.25 is NOT applied.
        // It does not normalize to 10 Liters (40 * 0.25); instead, unconfirmed factor is excluded!
        resultBeforeConfirm.Consumption7dTotal.Should().Be(40m);

        // Act 2: User formally confirms the conversion
        unconfirmedConv.IsConfirmed = true;
        unconfirmedConv.Notes += " [تأیید رسمی]";
        await context.SaveChangesAsync();

        var resultAfterConfirm = await calculator.CalculateConsumptionAsync(material.Id, now);

        // Assert 2: Now that conversion is confirmed, factor 0.25 is ACTIVATED in calculations (40 * 0.25 = 10 Liters)
        resultAfterConfirm.Consumption7dTotal.Should().Be(10.0m);
    }

    [Fact]
    public async Task UnconfirmedConversion_DoesNotUnblockSyncUnitMismatch_UntilConfirmed()
    {
        // Requirement 4: In inventory sync, an unconfirmed conversion must NOT resolve unit mismatch.
        using var context = CreateInMemoryContext();
        var engine = new CatalogStandardizationEngine(context, NullLogger<CatalogStandardizationEngine>.Instance);

        var unitCount = await context.UnitsOfMeasure.FirstAsync(u => u.Code == "COUNT");
        var unitLiter = await context.UnitsOfMeasure.FirstAsync(u => u.Code == "L");

        var material = new Material
        {
            Id = Guid.NewGuid(),
            InternalCode = "MAT-SYNC-TEST",
            Name = "شیر آزمایشی همگام‌سازی",
            BaseUnitId = unitCount.Id, // Base unit in CRM is COUNT (Dimension: Count)
            IsActive = true
        };
        context.Materials.Add(material);

        var mapping = new InventoryItemMapping
        {
            Id = Guid.NewGuid(),
            MaterialId = material.Id,
            SepidarItemId = "SEP-SYNC-01",
            SepidarItemCode = "SYNC-01",
            SepidarItemTitle = "شیر خام لیتر",
            SepidarBaseUnit = "L", // Dimension: Volume
            IsActive = true
        };
        context.InventoryItemMappings.Add(mapping);

        // Add UNCONFIRMED conversion between COUNT and L
        var conv = new MaterialUnitConversion
        {
            Id = Guid.NewGuid(),
            MaterialId = material.Id,
            FromUnitId = unitLiter.Id,
            ToUnitId = unitCount.Id,
            Factor = 1.0m,
            IsConfirmed = false // UNCONFIRMED!
        };
        context.MaterialUnitConversions.Add(conv);
        context.Warehouses.Add(new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = "WH-MAIN",
            Name = "انبار اصلی",
            SepidarWarehouseRef = "1",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var sepidarStock = new List<SepidarStockRecord>
        {
            new SepidarStockRecord("SEP-SYNC-01", "1", 100m, 100m, 0m, 0m, "L", DateTime.UtcNow)
        };
        var mockAdapter = new ControllableSepidarInventoryAdapter(new List<SepidarItemRecord>(), sepidarStock);
        var calc = new ConsumptionCalculator(context);
        var recEngine = new PurchaseRecommendationEngine(context, calc);
        var syncService = new InventorySyncService(context, mockAdapter, calc, recEngine, NullLogger<InventorySyncService>.Instance, engine);

        // Act 1: Run dry run with unconfirmed conversion
        var dryRunResult = await syncService.ExecuteDryRunAsync("WH-MAIN");

        // Assert 1: Item is blocked due to unit mismatch because conversion is unconfirmed!
        dryRunResult.Snapshots.BlockedItemsUnitMismatch.Should().Be(1);
        dryRunResult.BlockedItemsChecklist.Should().Contain(b => b.ItemRef == "SEP-SYNC-01");

        // Act 2: Confirm conversion
        conv.IsConfirmed = true;
        await context.SaveChangesAsync();

        var dryRunAfterConfirm = await syncService.ExecuteDryRunAsync("WH-MAIN");

        // Assert 2: Once confirmed, conversion unblocks the item!
        dryRunAfterConfirm.Snapshots.BlockedItemsUnitMismatch.Should().Be(0);
    }

    [Fact]
    public async Task UserCanCustomizeCategoryAndName_WithoutDefiningConversionFactor_AndPreservesInSync()
    {
        // Requirement 2: User is NOT forced to define conversion factor to categorize or edit name,
        // and manual edits are preserved in subsequent syncs.
        using var context = CreateInMemoryContext();
        var engine = new CatalogStandardizationEngine(context, NullLogger<CatalogStandardizationEngine>.Instance);

        var dairyCat = await engine.EnsureUnifiedCategoryAsync("CAT-DAIRY", "لبنیات", "CAT-MILK", "شیر");
        var unitCount = await context.UnitsOfMeasure.FirstAsync(u => u.Code == "COUNT");

        var material = new Material
        {
            Id = Guid.NewGuid(),
            InternalCode = "MAT-NO-CONV",
            Name = "شیر پاکتی دامداران ۱ لیتری",
            Brand = "دامداران",
            Packaging = "پاکت",
            CategoryId = dairyCat.Id,
            BaseUnitId = unitCount.Id,
            IsCustomizedByUser = true, // User customized name & category
            CategorizationStatus = "ManualConfirmed",
            IsActive = true
            // NOTE: Absolutely NO MaterialUnitConversions added!
        };
        context.Materials.Add(material);

        var mapping = new InventoryItemMapping
        {
            Id = Guid.NewGuid(),
            MaterialId = material.Id,
            SepidarItemId = "SEP-999",
            SepidarItemCode = "999",
            SepidarItemTitle = "شیر کارخانه‌ای نامشخص",
            SepidarBaseUnit = "COUNT",
            IsActive = true,
            LastSyncUtc = DateTime.UtcNow.AddHours(-1)
        };
        context.InventoryItemMappings.Add(mapping);
        await context.SaveChangesAsync();

        // Material has zero conversions
        material.UnitConversions.Should().BeEmpty("User was not forced to define any unit conversion factor");

        // Run sync
        var rawItems = new List<SepidarItemRecord>
        {
            new SepidarItemRecord("SEP-999", "999", "شیر کارخانه‌ای نامشخص", "COUNT", "عدد", true)
        };
        var mockAdapter = new MockSepidarInventoryAdapter(rawItems);
        var calc = new ConsumptionCalculator(context);
        var recEngine = new PurchaseRecommendationEngine(context, calc);
        var syncService = new InventorySyncService(context, mockAdapter, calc, recEngine, NullLogger<InventorySyncService>.Instance, engine);

        var syncJob = await syncService.TriggerSyncAsync(SyncJobType.Full, "AdminUser");
        syncJob.Status.Should().Be("Success");

        // Verify material retained user edits
        var refreshedMat = await context.Materials.FindAsync(material.Id);
        refreshedMat!.Name.Should().Be("شیر پاکتی دامداران ۱ لیتری");
        refreshedMat.CategoryId.Should().Be(dairyCat.Id);
        refreshedMat.IsCustomizedByUser.Should().BeTrue();
        refreshedMat.CategorizationStatus.Should().Be("ManualConfirmed");
        refreshedMat.UnitConversions.Should().BeEmpty();
    }

    [Fact]
    public async Task CrmDbSeeder_WhenProduction_DoesNotSeedPilotTestItems()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        // Act - Call SeedAsync with isDevelopmentOrTest = false (simulating Production)
        await CrmDbSeeder.SeedAsync(context, isDevelopmentOrTest: false);

        // Assert - Test items must NOT exist
        var damdaran1L = await context.Materials.FirstOrDefaultAsync(m => m.InternalCode == "TEST-DAM-MILK-1L");
        var damdaran250ML = await context.Materials.FirstOrDefaultAsync(m => m.InternalCode == "TEST-DAM-MILK-250ML");
        var sepidar1 = await context.InventoryItemMappings.FirstOrDefaultAsync(m => m.SepidarItemId == "TEST-SEP-701");
        var sepidar2 = await context.InventoryItemMappings.FirstOrDefaultAsync(m => m.SepidarItemId == "TEST-SEP-702");

        damdaran1L.Should().BeNull("TEST-DAM items must never be seeded into Production");
        damdaran250ML.Should().BeNull("TEST-DAM items must never be seeded into Production");
        sepidar1.Should().BeNull("TEST-SEP mappings must never be seeded into Production");
        sepidar2.Should().BeNull("TEST-SEP mappings must never be seeded into Production");

        // System roles and permissions must still exist
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == Roles.Admin);
        adminRole.Should().NotBeNull();
    }

    [Fact]
    public async Task CrmDbSeeder_WhenDevelopmentOrTest_SeedsPilotTestItems()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        // Act - Call SeedAsync with isDevelopmentOrTest = true
        await CrmDbSeeder.SeedAsync(context, isDevelopmentOrTest: true);

        // Assert - Test items must exist
        var damdaran1L = await context.Materials.FirstOrDefaultAsync(m => m.InternalCode == "TEST-DAM-MILK-1L");
        var damdaran250ML = await context.Materials.FirstOrDefaultAsync(m => m.InternalCode == "TEST-DAM-MILK-250ML");
        var sepidar1 = await context.InventoryItemMappings.FirstOrDefaultAsync(m => m.SepidarItemId == "TEST-SEP-701");
        var sepidar2 = await context.InventoryItemMappings.FirstOrDefaultAsync(m => m.SepidarItemId == "TEST-SEP-702");

        damdaran1L.Should().NotBeNull();
        damdaran250ML.Should().NotBeNull();
        sepidar1.Should().NotBeNull();
        sepidar2.Should().NotBeNull();
    }

    [Fact]
    public void Requirement1_FrozenDough_CategorizedAs_DoughAndSemiPrepared()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var engine = new CatalogStandardizationEngine(context, NullLogger<CatalogStandardizationEngine>.Instance);

        // Act - Item has both "خمیر منجمد" and "سبوس"
        var suggestion = engine.ParseAndStandardize("خمیر منجمد سبوس گرد ویژه", "KG");

        // Assert - Must be Dough & Semi-prepared, NOT Flour!
        suggestion.Should().NotBeNull();
        suggestion.ParentCategoryName.Should().Be("آرد و غلات");
        suggestion.SubcategoryCode.Should().Be("CAT-DOUGH-SEMI");
        suggestion.SubcategoryName.Should().Be("خمیر و نیمه‌ساخته");
        suggestion.CategoryStatus.Should().Be("Clear");
    }

    [Fact]
    public void Requirement1_PowderedMilk_CategorizedAs_MilkPowder_SeparatedFromLiquidMilk()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var engine = new CatalogStandardizationEngine(context, NullLogger<CatalogStandardizationEngine>.Instance);

        // Act 1 - Powdered milk
        var powderSuggestion = engine.ParseAndStandardize("شیر خشک", "COUNT");

        // Act 2 - Liquid milk
        var liquidSuggestion = engine.ParseAndStandardize("شیر پاکتی پاک", "COUNT");

        // Assert - Separated subcategories under Dairy
        powderSuggestion.ParentCategoryName.Should().Be("لبنیات");
        powderSuggestion.SubcategoryCode.Should().Be("CAT-MILK-POWDER");
        powderSuggestion.SubcategoryName.Should().Be("شیر خشک");
        powderSuggestion.CategoryStatus.Should().Be("Clear");

        liquidSuggestion.ParentCategoryName.Should().Be("لبنیات");
        liquidSuggestion.SubcategoryCode.Should().Be("CAT-MILK");
        liquidSuggestion.SubcategoryName.Should().Be("شیر");
        liquidSuggestion.CategoryStatus.Should().Be("Clear");
    }

    [Fact]
    public async Task Requirement4_Phase1_CategoryOnly_ApplyAndRollback_WithConcurrencyCheck()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var engine = new CatalogStandardizationEngine(context, NullLogger<CatalogStandardizationEngine>.Instance);

        var catGeneral = new Category { Id = Guid.NewGuid(), Code = "CAT-IMPROVER", Name = "بهبوددهنده‌ها و مخمر", IsActive = true };
        var catTargetParent = new Category { Id = Guid.NewGuid(), Code = "CAT-FLOUR", Name = "آرد و غلات", IsActive = true };
        context.Categories.AddRange(catGeneral, catTargetParent);

        var kgUnit = await context.UnitsOfMeasure.FirstAsync(u => u.Code == "KG");

        var material = new Material
        {
            Id = Guid.NewGuid(),
            InternalCode = "MAT-DOUGH-01",
            Name = "خمیر منجمد سبوس گرد ویژه",
            CategoryId = catGeneral.Id,
            BaseUnitId = kgUnit.Id,
            Brand = null,
            Packaging = null,
            PackageSize = null,
            CategorizationStatus = "NeedsReview",
            IsCustomizedByUser = false
        };
        context.Materials.Add(material);
        await context.SaveChangesAsync();

        // Act 1: Apply Category-Only change
        var result = await engine.ApplyCategoryStandardizationAsync(
            material.Id,
            expectedCurrentCategoryId: catGeneral.Id,
            targetParentCode: "CAT-FLOUR",
            targetParentName: "آرد و غلات",
            targetSubCode: "CAT-DOUGH-SEMI",
            targetSubName: "خمیر و نیمه‌ساخته");

        // Assert 1: Only CategoryId changed; Name, BaseUnit, Brand are untouched!
        result.Success.Should().BeTrue();
        result.IsUnchanged.Should().BeFalse();
        result.OldCategoryId.Should().Be(catGeneral.Id);

        var updated = await context.Materials.Include(m => m.Category).FirstAsync(m => m.Id == material.Id);
        updated.CategoryId.Should().NotBe(catGeneral.Id);
        updated.Category!.Name.Should().Be("خمیر و نیمه‌ساخته");
        updated.Name.Should().Be("خمیر منجمد سبوس گرد ویژه", "Name must be completely untouched in Phase 1");
        updated.BaseUnitId.Should().Be(kgUnit.Id, "BaseUnit must be completely untouched in Phase 1");
        updated.StandardizationNotes.Should().Contain("PreviousCategoryId");

        // Act 2: Concurrency check - someone attempts to apply with stale old category
        var staleResult = await engine.ApplyCategoryStandardizationAsync(
            material.Id,
            expectedCurrentCategoryId: catGeneral.Id, // Stale!
            targetParentCode: "CAT-DAIRY",
            targetParentName: "لبنیات");

        staleResult.Success.Should().BeFalse("Should fail concurrency check because current category changed");

        // Act 3: Rollback to previous category
        var rollbackResult = await engine.RollbackCategoryStandardizationAsync(material.Id);
        rollbackResult.Success.Should().BeTrue();
        rollbackResult.NewCategoryId.Should().Be(catGeneral.Id);

        var rolledBack = await context.Materials.FirstAsync(m => m.Id == material.Id);
        rolledBack.CategoryId.Should().Be(catGeneral.Id);
    }
}


