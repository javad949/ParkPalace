using Microsoft.EntityFrameworkCore;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Services;
using Xunit;

namespace ParkPalace.UnitTests;

public class PilotManifestExtractorTests
{
    private static (CrmDbContext Db, Guid WhId, Guid KgId, Guid CountId) CreateContext()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        var db = new CrmDbContext(options);

        var wh = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = "WH-MAIN",
            Name = "انبار مرکزی",
            SepidarWarehouseRef = "1",
            IsActive = true
        };
        db.Warehouses.Add(wh);

        var kg = new UnitOfMeasure { Id = Guid.NewGuid(), Code = "KG", Name = "کیلوگرم", IsActive = true };
        var count = new UnitOfMeasure { Id = Guid.NewGuid(), Code = "COUNT", Name = "عدد", IsActive = true };
        db.UnitsOfMeasure.AddRange(kg, count);
        db.SaveChanges();

        return (db, wh.Id, kg.Id, count.Id);
    }

    [Fact]
    public async Task ExtractAsync_WithValidItem_ExtractsManifestAndComputesDeterministicHash()
    {
        var (db, whId, kgId, _) = CreateContext();
        var matId = Guid.NewGuid();
        var mat = new Material { Id = matId, InternalCode = "MAT-1", Name = "آرد", BaseUnitId = kgId, IsActive = true };
        db.Materials.Add(mat);
        db.InventoryItemMappings.Add(new InventoryItemMapping
        {
            MaterialId = matId,
            SepidarItemId = "SEP-101",
            SepidarBaseUnit = "KG",
            IsActive = true
        });
        db.InventorySnapshots.Add(new InventorySnapshot
        {
            WarehouseId = whId,
            MaterialId = matId,
            UnitId = kgId,
            OnHandQuantity = 100
        });
        await db.SaveChangesAsync();

        var result = await PilotManifestExtractor.ExtractAsync(
            db, 10, new DateTime(2026, 3, 21), new DateTime(2027, 3, 21));

        Assert.NotNull(result.Manifest);
        Assert.NotNull(result.ManifestHash);
        var item = Assert.Single(result.ValidItems);
        Assert.Equal("SEP-101", item.SourceItemId);
        Assert.Equal(matId, item.MaterialId);
        Assert.Equal(kgId, item.UnitId);
        Assert.Equal("KG", item.UnitCode);
        Assert.Empty(result.ExcludedItems);
    }

    [Fact]
    public async Task ExtractAsync_ExcludesUnitNotKg_WithSpecificReason()
    {
        var (db, whId, kgId, countId) = CreateContext();
        var matId = Guid.NewGuid();
        var mat = new Material { Id = matId, InternalCode = "MAT-BAG", Name = "کیسه", BaseUnitId = countId, IsActive = true };
        db.Materials.Add(mat);
        db.InventoryItemMappings.Add(new InventoryItemMapping
        {
            MaterialId = matId,
            SepidarItemId = "SEP-BAG",
            SepidarBaseUnit = "COUNT",
            IsActive = true
        });
        db.InventorySnapshots.Add(new InventorySnapshot
        {
            WarehouseId = whId,
            MaterialId = matId,
            UnitId = countId,
            OnHandQuantity = 500
        });
        await db.SaveChangesAsync();

        var result = await PilotManifestExtractor.ExtractAsync(
            db, 10, new DateTime(2026, 3, 21), new DateTime(2027, 3, 21));

        Assert.Null(result.Manifest);
        Assert.Empty(result.ValidItems);
        var exclusion = Assert.Single(result.ExcludedItems);
        Assert.Equal("UNIT_NOT_KG", exclusion.Reason);
        Assert.Equal("SEP-BAG", exclusion.SourceItemId);
    }

    [Fact]
    public async Task ExtractAsync_ExcludesInactiveMappingAndInactiveMaterial()
    {
        var (db, whId, kgId, _) = CreateContext();

        // 1. Inactive mapping
        var mat1 = new Material { Id = Guid.NewGuid(), InternalCode = "MAT-INACTIVE-MAP", Name = "مخمر", BaseUnitId = kgId, IsActive = true };
        db.Materials.Add(mat1);
        db.InventoryItemMappings.Add(new InventoryItemMapping
        {
            MaterialId = mat1.Id,
            SepidarItemId = "SEP-INACTIVE-MAP",
            IsActive = false
        });
        db.InventorySnapshots.Add(new InventorySnapshot { WarehouseId = whId, MaterialId = mat1.Id, UnitId = kgId, OnHandQuantity = 10 });

        // 2. Inactive material
        var mat2 = new Material { Id = Guid.NewGuid(), InternalCode = "MAT-INACTIVE-MAT", Name = "نمک", BaseUnitId = kgId, IsActive = false };
        db.Materials.Add(mat2);
        db.InventoryItemMappings.Add(new InventoryItemMapping
        {
            MaterialId = mat2.Id,
            SepidarItemId = "SEP-INACTIVE-MAT",
            IsActive = true
        });
        db.InventorySnapshots.Add(new InventorySnapshot { WarehouseId = whId, MaterialId = mat2.Id, UnitId = kgId, OnHandQuantity = 20 });

        await db.SaveChangesAsync();

        var result = await PilotManifestExtractor.ExtractAsync(
            db, 10, new DateTime(2026, 3, 21), new DateTime(2027, 3, 21));

        Assert.Null(result.Manifest);
        Assert.Equal(2, result.ExcludedItems.Count);
        Assert.Contains(result.ExcludedItems, e => e.Reason == "MAPPING_INACTIVE");
        Assert.Contains(result.ExcludedItems, e => e.Reason == "MATERIAL_INACTIVE");
    }

    [Fact]
    public async Task ExtractAsync_ExcludesMissingOrMismatchedSnapshotBaseline()
    {
        var (db, whId, kgId, countId) = CreateContext();

        // 1. Missing snapshot
        var mat1 = new Material { Id = Guid.NewGuid(), InternalCode = "MAT-NO-SNAP", Name = "شکر", BaseUnitId = kgId, IsActive = true };
        db.Materials.Add(mat1);
        db.InventoryItemMappings.Add(new InventoryItemMapping { MaterialId = mat1.Id, SepidarItemId = "SEP-NO-SNAP", SepidarBaseUnit = "KG", IsActive = true });

        // 2. Snapshot unit mismatch
        var mat2 = new Material { Id = Guid.NewGuid(), InternalCode = "MAT-BAD-SNAP", Name = "روغن", BaseUnitId = kgId, IsActive = true };
        db.Materials.Add(mat2);
        db.InventoryItemMappings.Add(new InventoryItemMapping { MaterialId = mat2.Id, SepidarItemId = "SEP-BAD-SNAP", SepidarBaseUnit = "KG", IsActive = true });
        db.InventorySnapshots.Add(new InventorySnapshot { WarehouseId = whId, MaterialId = mat2.Id, UnitId = countId, OnHandQuantity = 5 });

        await db.SaveChangesAsync();

        var result = await PilotManifestExtractor.ExtractAsync(
            db, 10, new DateTime(2026, 3, 21), new DateTime(2027, 3, 21));

        Assert.Null(result.Manifest);
        Assert.Contains(result.ExcludedItems, e => e.Reason == "MISSING_SNAPSHOT_BASELINE");
        Assert.Contains(result.ExcludedItems, e => e.Reason == "SNAPSHOT_UNIT_MISMATCH");
    }

    [Fact]
    public async Task ExtractAsync_IncompleteCategoryDoesNotExcludeItem()
    {
        var (db, whId, kgId, _) = CreateContext();
        var matId = Guid.NewGuid();
        // Material with empty CategoryId (incomplete category)
        var mat = new Material { Id = matId, InternalCode = "MAT-NOCAT", Name = "کالای بدون دسته", BaseUnitId = kgId, CategoryId = Guid.Empty, IsActive = true };
        db.Materials.Add(mat);
        db.InventoryItemMappings.Add(new InventoryItemMapping { MaterialId = matId, SepidarItemId = "SEP-NOCAT", SepidarBaseUnit = "KG", IsActive = true });
        db.InventorySnapshots.Add(new InventorySnapshot { WarehouseId = whId, MaterialId = matId, UnitId = kgId, OnHandQuantity = 30 });
        await db.SaveChangesAsync();

        var result = await PilotManifestExtractor.ExtractAsync(
            db, 10, new DateTime(2026, 3, 21), new DateTime(2027, 3, 21));

        // It MUST enter the manifest
        Assert.NotNull(result.Manifest);
        var item = Assert.Single(result.ValidItems);
        Assert.Equal("SEP-NOCAT", item.SourceItemId);
        Assert.Empty(result.ExcludedItems);
        // Warning should be recorded
        Assert.Single(result.IncompleteCategoriesWarning);
    }

    [Fact]
    public async Task ExtractAsync_DuplicateSourceIdAcrossMappings_ExcludesBoth()
    {
        var (db, whId, kgId, _) = CreateContext();
        var mat1 = new Material { Id = Guid.NewGuid(), InternalCode = "MAT-DUP-1", Name = "کالا ۱", BaseUnitId = kgId, IsActive = true };
        var mat2 = new Material { Id = Guid.NewGuid(), InternalCode = "MAT-DUP-2", Name = "کالا ۲", BaseUnitId = kgId, IsActive = true };
        db.Materials.AddRange(mat1, mat2);
        // Both point to SEP-SHARED-ID
        db.InventoryItemMappings.Add(new InventoryItemMapping { MaterialId = mat1.Id, SepidarItemId = "SEP-SHARED-ID", SepidarBaseUnit = "KG", IsActive = true });
        db.InventoryItemMappings.Add(new InventoryItemMapping { MaterialId = mat2.Id, SepidarItemId = "SEP-SHARED-ID", SepidarBaseUnit = "KG", IsActive = true });
        db.InventorySnapshots.Add(new InventorySnapshot { WarehouseId = whId, MaterialId = mat1.Id, UnitId = kgId, OnHandQuantity = 10 });
        db.InventorySnapshots.Add(new InventorySnapshot { WarehouseId = whId, MaterialId = mat2.Id, UnitId = kgId, OnHandQuantity = 20 });
        await db.SaveChangesAsync();

        var result = await PilotManifestExtractor.ExtractAsync(
            db, 10, new DateTime(2026, 3, 21), new DateTime(2027, 3, 21));

        Assert.Null(result.Manifest);
        Assert.Equal(2, result.ExcludedItems.Count);
        Assert.All(result.ExcludedItems, e => Assert.Equal("DUPLICATE_SOURCE_ITEM_ID", e.Reason));
    }

    [Fact]
    public async Task ExtractAsync_ExcludesMissingUnitTitle_AndDoesNotFallbackToKg()
    {
        var (db, whId, kgId, _) = CreateContext();
        var matId = Guid.NewGuid();
        var mat = new Material { Id = matId, InternalCode = "MAT-NO-UNIT", Name = "کالای بدون واحد", BaseUnitId = kgId, IsActive = true };
        db.Materials.Add(mat);
        // SepidarBaseUnit is null / empty
        db.InventoryItemMappings.Add(new InventoryItemMapping
        {
            MaterialId = matId,
            SepidarItemId = "SEP-NO-UNIT",
            SepidarBaseUnit = null,
            IsActive = true
        });
        db.InventorySnapshots.Add(new InventorySnapshot { WarehouseId = whId, MaterialId = matId, UnitId = kgId, OnHandQuantity = 50 });
        await db.SaveChangesAsync();

        var result = await PilotManifestExtractor.ExtractAsync(
            db, 10, new DateTime(2026, 3, 21), new DateTime(2027, 3, 21));

        // Must NOT enter the manifest; no fallback to KG!
        Assert.Null(result.Manifest);
        var exclusion = Assert.Single(result.ExcludedItems);
        Assert.Equal("SEPIDAR_UNIT_MISSING", exclusion.Reason);
        Assert.Equal("SEP-NO-UNIT", exclusion.SourceItemId);
    }

    [Fact]
    public async Task ExtractAsync_UnitRef1WithNonKgTitle_IsExcludedFromPilot()
    {
        var (db, whId, kgId, _) = CreateContext();
        var matId = Guid.NewGuid();
        var mat = new Material { Id = matId, InternalCode = "MAT-BOX-1", Name = "بسته آزمایشی با شناسه ۱", BaseUnitId = kgId, IsActive = true };
        db.Materials.Add(mat);
        // UnitRef in Sepidar is 1, but Title is "بسته" (non-KG)
        db.InventoryItemMappings.Add(new InventoryItemMapping
        {
            MaterialId = matId,
            SepidarItemId = "1",
            SepidarItemCode = "1",
            SepidarBaseUnit = "بسته", // Even if Sepidar UnitRef = 1, title is NOT KG
            IsActive = true
        });
        db.InventorySnapshots.Add(new InventorySnapshot { WarehouseId = whId, MaterialId = matId, UnitId = kgId, OnHandQuantity = 100 });
        await db.SaveChangesAsync();

        var result = await PilotManifestExtractor.ExtractAsync(
            db, 10, new DateTime(2026, 3, 21), new DateTime(2027, 3, 21));

        // Must NOT enter the manifest
        Assert.Null(result.Manifest);
        var exclusion = Assert.Single(result.ExcludedItems);
        Assert.Equal("SEPIDAR_UNIT_INCOMPATIBLE", exclusion.Reason);
        Assert.Equal("1", exclusion.SourceItemId);
        Assert.Contains("بسته", exclusion.Details);
    }
}
