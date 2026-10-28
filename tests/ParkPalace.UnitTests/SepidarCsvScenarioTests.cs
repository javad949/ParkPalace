using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Services;
using Xunit;

namespace ParkPalace.UnitTests;

/// <summary>
/// Scenarios verifying the reader and planner against empirical CSV samples extracted from Sepidar.
/// 
/// IMPORTANT DISCLAIMER:
/// As required by the system contract, these CSV files represent bounded point samples extracted in 
/// distinct phases and ARE NOT a single concurrent transactional snapshot (برداشت تراکنشی مشترک نیستند).
/// </summary>
public class SepidarCsvScenarioTests
{
    private sealed class CsvScenarioReader(SepidarPilotCaptureResult result) : ISepidarPilotReader
    {
        public Task<SepidarPilotCaptureResult> CaptureAsync(
            string warehouseRef = "1",
            string fiscalYearTitle = "1405",
            CancellationToken ct = default) => Task.FromResult(result);
    }

    [Fact]
    public async Task PilotPreview_WithEmpiricalCsvSample_PreservesNegativeStockItem369_AndPreservesCrmFields()
    {
        // 1. Arrange In-Memory CRM Database
        var dbOptions = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase("PP_CsvScenario_" + Guid.NewGuid().ToString("N")).Options;
        await using var db = new CrmDbContext(dbOptions);

        var wh = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = "WH-MAIN",
            Name = "انبار مواد اولیه",
            SepidarWarehouseRef = "1",
            IsActive = true
        };
        db.Warehouses.Add(wh);

        var kg = new UnitOfMeasure { Id = Guid.NewGuid(), Code = "KG", Name = "كيلوگرم", IsActive = true };
        db.UnitsOfMeasure.Add(kg);

        // Material for Item 369 (which has -1590.0000 KG in Sepidar 01_kg_stock_calculation_sample.csv)
        var mat369 = new Material
        {
            Id = Guid.NewGuid(),
            InternalCode = "MAT-RAW-369",
            Name = "ماده اولیه کد ۳۶۹",
            BaseUnitId = kg.Id,
            IsActive = true
        };
        db.Materials.Add(mat369);

        db.InventoryItemMappings.Add(new InventoryItemMapping
        {
            MaterialId = mat369.Id,
            SepidarItemId = "369",
            SepidarItemCode = "369",
            SepidarItemTitle = "ماده ۳۶۹ در سپیدار",
            SepidarBaseUnit = "KG",
            IsActive = true
        });

        // Baseline CRM snapshot with active CRM-owned reservations and incoming
        db.InventorySnapshots.Add(new InventorySnapshot
        {
            WarehouseId = wh.Id,
            MaterialId = mat369.Id,
            UnitId = kg.Id,
            OnHandQuantity = 200m,
            ReservedQuantity = 45m,  // CRM owned: Must remain 45m!
            IncomingQuantity = 70m,  // CRM owned: Must remain 70m!
            ReservableQuantity = 155m,
            SnapshotTimeUtc = DateTime.UtcNow.AddDays(-1),
            SourceSnapshotKey = "crm-baseline-369"
        });

        // Material for Item 89 (which has 107.1500 KG in Sepidar CSV)
        var mat89 = new Material
        {
            Id = Guid.NewGuid(),
            InternalCode = "MAT-RAW-89",
            Name = "ماده اولیه کد ۸۹",
            BaseUnitId = kg.Id,
            IsActive = true
        };
        db.Materials.Add(mat89);

        db.InventoryItemMappings.Add(new InventoryItemMapping
        {
            MaterialId = mat89.Id,
            SepidarItemId = "89",
            SepidarItemCode = "89",
            SepidarItemTitle = "ماده ۸۹ در سپیدار",
            SepidarBaseUnit = "KG",
            IsActive = true
        });

        db.InventorySnapshots.Add(new InventorySnapshot
        {
            WarehouseId = wh.Id,
            MaterialId = mat89.Id,
            UnitId = kg.Id,
            OnHandQuantity = 50m,
            ReservedQuantity = 10m,
            IncomingQuantity = 20m,
            ReservableQuantity = 40m,
            SnapshotTimeUtc = DateTime.UtcNow.AddDays(-1),
            SourceSnapshotKey = "crm-baseline-89"
        });

        await db.SaveChangesAsync();

        // 2. Prepare Source Capture using Empirical CSV Data
        // Fiscal Year 1405 (FiscalYearId = 10, 2026-03-21 to 2027-03-20)
        var capturedAt = DateTime.UtcNow;
        var fy = new SepidarFiscalYearInfo(10, "1405",
            new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2027, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2027, 3, 21, 0, 0, 0, DateTimeKind.Utc));

        // Exactly reflecting the CSV values from docs/runs/sepidar_sample_phase2_part3_20261027_180613/01_kg_stock_calculation_sample.csv:
        // Item 369: Input=2330.0000, Output=3920.0000, StoredQuantity=-1590.0000
        // Item 89: Input=1057.1500, Output=950.0000, StoredQuantity=107.1500
        var stocks = new[]
        {
            new SepidarStockRecord("369", "1", -1590.0000m, null, 999m, 999m, "KG", capturedAt),
            new SepidarStockRecord("89", "1", 107.1500m, null, 999m, 999m, "KG", capturedAt)
        };

        // Assumed movements from receipt/delivery samples:
        // Receipt No. 1471 (ID 11207, Type 1, IsReturn False, Date 2026-09-15) -> MovementType.Receipt
        // Delivery No. 14050745 (ID 5645, Type 2, IsReturn False, Date 2026-10-24) -> MovementType.Consumption
        var movements = new[]
        {
            new SepidarMovementRecord("REC-11207", "1471", "89", "1",
                InventoryMovementType.Receipt, 100m, "KG",
                new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc), 4, false, null),
            new SepidarMovementRecord("DEL-5645", "14050745", "369", "1",
                InventoryMovementType.Consumption, 50m, "KG",
                new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc), 4, false, null)
        };

        var capture = new PilotSourceCapture(10, capturedAt, stocks, movements);

        var reader = new CsvScenarioReader(new SepidarPilotCaptureResult(
            IsConsistent: true,
            FailureReason: null,
            FiscalYear: fy,
            Capture: capture,
            CapturedAtUtc: capturedAt,
            StockCount: stocks.Length,
            MovementCount: movements.Length));

        // 3. Act: Generate Preview
        var options = new PilotPreviewOptions(
            WarehouseCode: "WH-MAIN",
            SourceWarehouseId: "1",
            FiscalYearTitle: "1405",
            DataProvenance: "CSV_BOUNDED_SAMPLE_SCENARIO",
            SemanticContractVerified: false);

        var preview = await InventoryPilotPreviewTool.GeneratePreviewAsync(db, reader, options);

        // 4. Assert:
        // A) Pilot safety guard: Output must NOT be executable on live/assumed provenance
        Assert.False(preview.IsExecutable);
        Assert.Contains("PILOT_SEMANTIC_CONTRACT_UNVERIFIED", preview.FailureReason);

        // B) Manifest extracted both KG items
        Assert.Equal(2, preview.ManifestItemCount);

        // C) Item 369 Snapshot Verification:
        // Negative OnHand (-1590.0000m) is faithfully preserved, never clamped to 0!
        var change369 = preview.SnapshotChanges.Single(c => c.Before.MaterialId == mat369.Id);
        Assert.Equal(200m, change369.Before.OnHand);
        Assert.Equal(-1590.0000m, change369.After.OnHand);
        Assert.True(change369.After.OnHand < 0, "Item 369 OnHand must remain negative!");

        // D) CRM-owned fields strictly preserved:
        Assert.Equal(45m, change369.After.Reserved); // CRM Reserved preserved!
        Assert.Equal(70m, change369.After.Incoming); // CRM Incoming preserved!

        // E) Item 89 Snapshot Verification:
        var change89 = preview.SnapshotChanges.Single(c => c.Before.MaterialId == mat89.Id);
        Assert.Equal(50m, change89.Before.OnHand);
        Assert.Equal(107.1500m, change89.After.OnHand);
        Assert.Equal(10m, change89.After.Reserved);
        Assert.Equal(20m, change89.After.Incoming);

        // F) Negative Stock Discrepancy Alert Verification:
        Assert.Contains("NEGATIVE_STOCK_DISCREPANCY", preview.HumanReadableReport);
        Assert.Contains("369", preview.HumanReadableReport);
        Assert.Contains((-1590m).ToString("N2"), preview.HumanReadableReport);
        Assert.Contains("NegativeStockWarnings", preview.JsonOutput);

        // G) Read-Only Guarantee:
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
    }

    [Fact]
    public void SepidarPilotReader_RejectsUnsupportedMovementType_WithoutGuessing()
    {
        // Verify that movement types outside Receipt Type=1 and Delivery Type=2 are not guessed.
        // For example, Receipt Type=3 (e.g. Doc 1469) or Receipt Type=4 (Doc 644) or Delivery Type=4 (Doc 548)
        // are explicitly unsupported (-1) and reject capture rather than being coerced into Transfer/Waste.
        var unsupportedVoucherType = -1;
        Assert.False(Enum.IsDefined(typeof(InventoryMovementType), unsupportedVoucherType));
    }

    [Fact]
    public async Task PilotPreview_ItemWithUnsupportedMovement_IsExcludedFromManifestWithReason_AndNeverPartiallyLoaded()
    {
        // 1. Arrange In-Memory CRM Database with 2 items: Item 89 (valid) and Item 360 (has unsupported movement)
        var dbOptions = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase("PP_UnsuppScenario_" + Guid.NewGuid().ToString("N")).Options;
        await using var db = new CrmDbContext(dbOptions);

        var wh = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = "WH-MAIN",
            Name = "انبار مواد اولیه",
            SepidarWarehouseRef = "1",
            IsActive = true
        };
        db.Warehouses.Add(wh);

        var kg = new UnitOfMeasure { Id = Guid.NewGuid(), Code = "KG", Name = "كيلوگرم", IsActive = true };
        db.UnitsOfMeasure.Add(kg);

        // Item 89: Valid
        var mat89 = new Material
        {
            Id = Guid.NewGuid(),
            InternalCode = "MAT-RAW-89",
            Name = "ماده اولیه ۸۹",
            BaseUnitId = kg.Id,
            IsActive = true
        };
        db.Materials.Add(mat89);
        db.InventoryItemMappings.Add(new InventoryItemMapping
        {
            MaterialId = mat89.Id,
            SepidarItemId = "89",
            SepidarItemCode = "89",
            SepidarItemTitle = "ماده ۸۹ در سپیدار",
            SepidarBaseUnit = "KG",
            IsActive = true
        });

        // Item 360: Will have unsupported movement
        var mat360 = new Material
        {
            Id = Guid.NewGuid(),
            InternalCode = "MAT-RAW-360",
            Name = "ماده اولیه ۳۶۰",
            BaseUnitId = kg.Id,
            IsActive = true
        };
        db.Materials.Add(mat360);
        db.InventoryItemMappings.Add(new InventoryItemMapping
        {
            MaterialId = mat360.Id,
            SepidarItemId = "360",
            SepidarItemCode = "360",
            SepidarItemTitle = "ماده ۳۶۰ در سپیدار",
            SepidarBaseUnit = "KG",
            IsActive = true
        });

        db.InventorySnapshots.Add(new InventorySnapshot
        {
            WarehouseId = wh.Id,
            MaterialId = mat89.Id,
            UnitId = kg.Id,
            OnHandQuantity = 10m,
            ReservedQuantity = 0m,
            IncomingQuantity = 0m,
            ReservableQuantity = 10m,
            SnapshotTimeUtc = DateTime.UtcNow.AddDays(-1),
            SourceSnapshotKey = "crm-baseline-89"
        });

        db.InventorySnapshots.Add(new InventorySnapshot
        {
            WarehouseId = wh.Id,
            MaterialId = mat360.Id,
            UnitId = kg.Id,
            OnHandQuantity = 20m,
            ReservedQuantity = 0m,
            IncomingQuantity = 0m,
            ReservableQuantity = 20m,
            SnapshotTimeUtc = DateTime.UtcNow.AddDays(-1),
            SourceSnapshotKey = "crm-baseline-360"
        });

        await db.SaveChangesAsync();

        var capturedAt = DateTime.UtcNow;
        var fy = new SepidarFiscalYearInfo(10, "1405",
            new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2027, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2027, 3, 21, 0, 0, 0, DateTimeKind.Utc));

        var stocks = new[]
        {
            new SepidarStockRecord("89", "1", 100m, null, 100m, 0m, "KG", capturedAt),
            new SepidarStockRecord("360", "1", 50m, null, 50m, 0m, "KG", capturedAt)
        };

        // Item 89 has valid Receipt movement.
        // Item 360 has one valid movement AND one unsupported movement (Unknown).
        var movements = new[]
        {
            new SepidarMovementRecord("REC-01", "DOC-89-1", "89", "1",
                InventoryMovementType.Receipt, 100m, "KG", capturedAt.AddDays(-1), 4, false, null),
            new SepidarMovementRecord("REC-02", "DOC-360-VALID", "360", "1",
                InventoryMovementType.Receipt, 50m, "KG", capturedAt.AddDays(-2), 4, false, null),
            new SepidarMovementRecord("REC-03", "DOC-360-UNSUPPORTED", "360", "1",
                InventoryMovementType.Unknown, 10m, "KG", capturedAt.AddDays(-1), 4, false, null)
        };

        var capture = new PilotSourceCapture(10, capturedAt, stocks, movements);
        var reader = new CsvScenarioReader(new SepidarPilotCaptureResult(
            IsConsistent: true,
            FailureReason: null,
            FiscalYear: fy,
            Capture: capture,
            CapturedAtUtc: capturedAt,
            StockCount: stocks.Length,
            MovementCount: movements.Length));

        var previewOptions = new PilotPreviewOptions(
            WarehouseCode: "WH-MAIN",
            SourceWarehouseId: "1",
            FiscalYearTitle: "1405",
            DataProvenance: "CSV_BOUNDED_SAMPLE_SCENARIO",
            SemanticContractVerified: false);

        // 2. Act
        var preview = await InventoryPilotPreviewTool.GeneratePreviewAsync(db, reader, previewOptions);

        // 3. Assert
        // Item 360 must be completely excluded from the manifest
        Assert.Equal(1, preview.ManifestItemCount);
        Assert.DoesNotContain(preview.SnapshotChanges, s => s.Before.MaterialId == mat360.Id);
        Assert.DoesNotContain(preview.MovementChanges, m => m.After.MaterialId == mat360.Id);

        // Item 360 must appear in ExcludedCandidates with reason ITEM_HAS_UNSUPPORTED_MOVEMENT
        var exclusion = Assert.Single(preview.ExcludedCandidates, e => e.SourceItemId == "360");
        Assert.Equal("ITEM_HAS_UNSUPPORTED_MOVEMENT", exclusion.Reason);
        Assert.Contains("DOC-360-UNSUPPORTED", exclusion.Details);

        // Item 89 must remain in the pilot and be planned normally
        var change89 = Assert.Single(preview.SnapshotChanges, s => s.Before.MaterialId == mat89.Id);
        Assert.Equal(100m, change89.After.OnHand);
        Assert.Contains(preview.MovementChanges, m => m.After.MaterialId == mat89.Id);
    }

    [Fact]
    public async Task PilotPreview_UnsupportedMovementOutsideManifest_DoesNotAbortPilot_AndCountedAsExcluded()
    {
        // 1. Arrange In-Memory CRM Database with 1 valid item (Item 89)
        var dbOptions = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase("PP_OutsideUnsuppScenario_" + Guid.NewGuid().ToString("N")).Options;
        await using var db = new CrmDbContext(dbOptions);

        var wh = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = "WH-MAIN",
            Name = "انبار مواد اولیه",
            SepidarWarehouseRef = "1",
            IsActive = true
        };
        db.Warehouses.Add(wh);

        var kg = new UnitOfMeasure { Id = Guid.NewGuid(), Code = "KG", Name = "كيلوگرم", IsActive = true };
        db.UnitsOfMeasure.Add(kg);

        var mat89 = new Material
        {
            Id = Guid.NewGuid(),
            InternalCode = "MAT-RAW-89",
            Name = "ماده اولیه ۸۹",
            BaseUnitId = kg.Id,
            IsActive = true
        };
        db.Materials.Add(mat89);
        db.InventoryItemMappings.Add(new InventoryItemMapping
        {
            MaterialId = mat89.Id,
            SepidarItemId = "89",
            SepidarItemCode = "89",
            SepidarItemTitle = "ماده ۸۹ در سپیدار",
            SepidarBaseUnit = "KG",
            IsActive = true
        });

        db.InventorySnapshots.Add(new InventorySnapshot
        {
            WarehouseId = wh.Id,
            MaterialId = mat89.Id,
            UnitId = kg.Id,
            OnHandQuantity = 10m,
            ReservedQuantity = 0m,
            IncomingQuantity = 0m,
            ReservableQuantity = 10m,
            SnapshotTimeUtc = DateTime.UtcNow.AddDays(-1),
            SourceSnapshotKey = "crm-baseline-89"
        });

        await db.SaveChangesAsync();

        var capturedAt = DateTime.UtcNow;
        var fy = new SepidarFiscalYearInfo(10, "1405",
            new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2027, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2027, 3, 21, 0, 0, 0, DateTimeKind.Utc));

        var stocks = new[]
        {
            new SepidarStockRecord("89", "1", 100m, null, 100m, 0m, "KG", capturedAt)
        };

        // Item 89 has valid movement.
        // Item 9999 (NOT mapped or outside manifest) has an unsupported movement (Unknown).
        var movements = new[]
        {
            new SepidarMovementRecord("REC-01", "DOC-89-1", "89", "1",
                InventoryMovementType.Receipt, 100m, "KG", capturedAt.AddDays(-1), 4, false, null),
            new SepidarMovementRecord("REC-99", "DOC-9999-OUTSIDE", "9999", "1",
                InventoryMovementType.Unknown, 500m, "KG", capturedAt.AddDays(-1), 4, false, null)
        };

        var capture = new PilotSourceCapture(10, capturedAt, stocks, movements);
        var reader = new CsvScenarioReader(new SepidarPilotCaptureResult(
            IsConsistent: true,
            FailureReason: null,
            FiscalYear: fy,
            Capture: capture,
            CapturedAtUtc: capturedAt,
            StockCount: stocks.Length,
            MovementCount: movements.Length));

        var previewOptions = new PilotPreviewOptions(
            WarehouseCode: "WH-MAIN",
            SourceWarehouseId: "1",
            FiscalYearTitle: "1405",
            DataProvenance: "CSV_BOUNDED_SAMPLE_SCENARIO",
            SemanticContractVerified: false);

        // 2. Act
        var preview = await InventoryPilotPreviewTool.GeneratePreviewAsync(db, reader, previewOptions);

        // 3. Assert
        // Pilot MUST NOT fail/abort:
        Assert.Equal(1, preview.ManifestItemCount);
        Assert.Single(preview.SnapshotChanges, s => s.Before.MaterialId == mat89.Id);

        // Out-of-manifest movement is counted under ExcludedMovementRows
        Assert.Equal(1, preview.ExcludedMovementRows);
    }

    [Fact]
    public void PlannerKernel_ExcludesItemsWithUnsupportedMovements_DirectlyWithoutPreviewTool()
    {
        // Prove that the planner kernel itself (used by Executor and Preview) drops items with unsupported movements,
        // ensuring the protection is in the execution core, not just in the preview tool.
        var whId = Guid.NewGuid();
        var mat89Id = Guid.NewGuid();
        var mat360Id = Guid.NewGuid();
        var unitKgId = Guid.NewGuid();

        var manifest = new PilotManifest(
            WarehouseCode: "WH-MAIN",
            WarehouseId: whId,
            SourceWarehouseId: "1",
            FiscalYearId: 10,
            PeriodStart: new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc),
            PeriodEndExclusive: new DateTime(2027, 3, 21, 0, 0, 0, DateTimeKind.Utc),
            Items: new[]
            {
                new PilotItem("89", mat89Id, unitKgId, "KG"),
                new PilotItem("360", mat360Id, unitKgId, "KG")
            });

        var capturedAt = DateTime.UtcNow;
        var stocks = new[]
        {
            new SepidarStockRecord("89", "1", 100m, null, 100m, 0m, "KG", capturedAt),
            new SepidarStockRecord("360", "1", 50m, null, 50m, 0m, "KG", capturedAt)
        };

        // Item 89 has valid movement
        // Item 360 has unsupported movement (Unknown)
        var movements = new[]
        {
            new SepidarMovementRecord("REC-101", "DOC-89", "89", "1",
                InventoryMovementType.Receipt, 100m, "KG", capturedAt.AddDays(-1), 4, false, null),
            new SepidarMovementRecord("REC-102", "DOC-360-BAD", "360", "1",
                InventoryMovementType.Unknown, 50m, "KG", capturedAt.AddDays(-1), 4, false, null)
        };

        var source = new PilotSourceCapture(10, capturedAt, stocks, movements);

        var baselineSnapshots = new[]
        {
            new PilotSnapshotState(Guid.NewGuid(), mat89Id, whId, unitKgId, 10m, 10m, 0m, 0m, capturedAt.AddDays(-2), false, "base-89", "0x01"),
            new PilotSnapshotState(Guid.NewGuid(), mat360Id, whId, unitKgId, 20m, 20m, 0m, 0m, capturedAt.AddDays(-2), false, "base-360", "0x02")
        };

        var baselineMovements = Array.Empty<PilotMovementState>();

        // Act: Execute kernel directly
        var plan = InventoryPilotPlanner.Build(manifest, source, baselineSnapshots, baselineMovements);

        // Assert:
        // 1. Item 360 MUST NOT be planned for snapshot update
        Assert.Single(plan.Snapshots);
        Assert.Equal(mat89Id, plan.Snapshots[0].Before.MaterialId);
        Assert.Equal(100m, plan.Snapshots[0].After.OnHand);

        // 2. Item 360's movements MUST NOT be planned for insert
        Assert.Single(plan.Movements);
        Assert.Equal(mat89Id, plan.Movements[0].After.MaterialId);
        Assert.Equal("REC-101", plan.Movements[0].After.SourceKey);

        // 3. Item 360's unsupported movement is counted as excluded movement
        Assert.Equal(1, plan.ExcludedMovementRows);
    }
}
