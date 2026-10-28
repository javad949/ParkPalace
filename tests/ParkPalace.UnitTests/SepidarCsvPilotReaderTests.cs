using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Services;
using Xunit;

namespace ParkPalace.UnitTests;

/// <summary>
/// Unit tests verifying SepidarCsvPilotReader against empirical CSV sample files
/// and custom test CSV configurations.
/// </summary>
public class SepidarCsvPilotReaderTests
{
    private static readonly string RunsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "runs"));

    private static IReadOnlyList<string> GetEmpiricalSampleDirectories()
    {
        return new[]
        {
            Path.Combine(RunsDir, "sepidar_sample_phase2_20261027_174332"),
            Path.Combine(RunsDir, "sepidar_sample_phase2_part2_20261027_175504"),
            Path.Combine(RunsDir, "sepidar_sample_phase2_part3_20261027_180613")
        };
    }

    [Fact]
    public async Task SepidarCsvPilotReader_ReadsFromEmpiricalCsvDirectories_SuccessfullyCapturesStocksAndNormalizesDates()
    {
        var dirs = GetEmpiricalSampleDirectories();
        // Ensure directories exist in this environment
        Assert.All(dirs, d => Assert.True(Directory.Exists(d), $"Required sample directory does not exist: {d}"));

        var reader = new SepidarCsvPilotReader(dirs);
        var result = await reader.CaptureAsync(warehouseRef: "1", fiscalYearTitle: "1405");

        // 1. Consistency & Metadata
        // Compliance: Discrete CSV samples are unverified for transactional snapshot consistency
        Assert.False(result.IsConsistent);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("PILOT_TRANSACTIONAL_SNAPSHOT_UNVERIFIED", result.FailureReason);
        Assert.True(result.MissingMovementData, "Empirical CSV samples lack movements item rows and must declare MissingMovementData = true.");
        Assert.Equal(0, result.MovementCount);

        // Input Manifest must be recorded with exact paths, hashes, and row counts
        Assert.NotNull(result.InputManifest);
        Assert.True(result.InputManifest.Count >= 4, $"Expected at least 4 manifest files, got {result.InputManifest.Count}");
        Assert.All(result.InputManifest, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Role));
            Assert.True(File.Exists(entry.Path), $"Manifest path not found: {entry.Path}");
            Assert.Equal(64, entry.Sha256Hash.Length);
            Assert.True(entry.RowCount >= 0);
        });

        Assert.NotNull(result.FiscalYear);
        Assert.Equal(10, result.FiscalYear.FiscalYearId);
        Assert.Equal("1405", result.FiscalYear.Title);
        Assert.Equal(new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc), result.FiscalYear.StartDate);
        Assert.Equal(new DateTime(2027, 3, 20, 0, 0, 0, DateTimeKind.Utc), result.FiscalYear.EndDate);
        
        // EndDate normalized to midnight of next day (2027-03-21)
        Assert.Equal(new DateTime(2027, 3, 21, 0, 0, 0, DateTimeKind.Utc), result.FiscalYear.PeriodEndExclusive);

        // 2. Stocks Captured
        Assert.NotNull(result.Capture);
        Assert.True(result.StockCount >= 20, $"Expected at least 20 stocks in sample, but got {result.StockCount}");

        // Item 369: Negative stock preserved (-1590.0000 KG)
        var stock369 = Assert.Single(result.Capture.Stocks, s => s.ItemRef == "369");
        Assert.Equal(-1590.0000m, stock369.OnHand);
        Assert.Equal("KG", stock369.UnitCode);
        Assert.Equal("1", stock369.WarehouseRef);

        // Item 89: 107.1500 KG
        var stock89 = Assert.Single(result.Capture.Stocks, s => s.ItemRef == "89");
        Assert.Equal(107.1500m, stock89.OnHand);
        Assert.Equal("KG", stock89.UnitCode);

        // Item 360: 1510.4100 KG
        var stock360 = Assert.Single(result.Capture.Stocks, s => s.ItemRef == "360");
        Assert.Equal(1510.4100m, stock360.OnHand);
        Assert.Equal("KG", stock360.UnitCode);
    }

    [Fact]
    public async Task SepidarCsvPilotReader_MissingFiscalYearsFile_ReturnsFailureReason()
    {
        var tempEmptyDir = Path.Combine(Path.GetTempPath(), "PP_Empty_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempEmptyDir);
        try
        {
            var reader = new SepidarCsvPilotReader(tempEmptyDir);
            var result = await reader.CaptureAsync("1", "1405");

            Assert.False(result.IsConsistent);
            Assert.Contains("PILOT_CSV_FILE_MISSING", result.FailureReason);
        }
        finally
        {
            if (Directory.Exists(tempEmptyDir)) Directory.Delete(tempEmptyDir, true);
        }
    }

    [Fact]
    public async Task SepidarCsvPilotReader_UnknownFiscalYear_ReturnsFailureReason()
    {
        var dirs = GetEmpiricalSampleDirectories();
        var reader = new SepidarCsvPilotReader(dirs);
        var result = await reader.CaptureAsync("1", "1499");

        Assert.False(result.IsConsistent);
        Assert.Contains("PILOT_FISCAL_YEAR_NOT_FOUND", result.FailureReason);
    }

    [Fact]
    public async Task SepidarCsvPilotReader_InactiveOrUnknownWarehouse_ReturnsFailureReason()
    {
        var dirs = GetEmpiricalSampleDirectories();
        var reader = new SepidarCsvPilotReader(dirs);
        var result = await reader.CaptureAsync("9999", "1405");

        Assert.False(result.IsConsistent);
        Assert.Contains("PILOT_SOURCE_WAREHOUSE_INVALID", result.FailureReason);
    }

    [Fact]
    public async Task SepidarCsvPilotReader_WithCustomMovementsCsv_MapsAcceptedAssumptionsCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PP_CsvMovementsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            // 1. Create fiscal year CSV
            var fyCsv = Path.Combine(tempDir, "01_fiscal_years.csv");
            await File.WriteAllTextAsync(fyCsv,
                "\"FiscalYearId\",\"Title\",\"StartDate\",\"EndDate\"\n" +
                "\"10\",\"1405\",\"2026-03-21T00:00:00Z\",\"2027-03-20T00:00:00Z\"\n");

            // 2. Create stocks CSV
            var stCsv = Path.Combine(tempDir, "01_stock1_verified_status.csv");
            await File.WriteAllTextAsync(stCsv,
                "\"StockID\",\"Code\",\"Title\",\"IsActive\"\n" +
                "\"1\",\"1\",\"انبار اصلی\",\"True\"\n");

            // 3. Create units CSV
            var uCsv = Path.Combine(tempDir, "04_units.csv");
            await File.WriteAllTextAsync(uCsv,
                "\"UnitID\",\"Title\"\n" +
                "\"1\",\"كيلوگرم\"\n" +
                "\"2\",\"عدد\"\n");

            // 4. Create stock summary CSV
            var sumCsv = Path.Combine(tempDir, "01_kg_stock_calculation_sample.csv");
            await File.WriteAllTextAsync(sumCsv,
                "\"ItemRef\",\"StockRef\",\"UnitRef\",\"StoredQuantity\"\n" +
                "\"89\",\"1\",\"1\",\"100.0000\"\n" +
                "\"360\",\"1\",\"1\",\"50.0000\"\n");

            // 5. Create movements CSV containing all assumed cases and unsupported types
            var movCsv = Path.Combine(tempDir, "movements.csv");
            await File.WriteAllTextAsync(movCsv,
                "\"VoucherRef\",\"DocumentNumber\",\"ItemRef\",\"WarehouseRef\",\"MovementType\",\"IsReturn\",\"Quantity\",\"UnitCode\",\"TransactionDateUtc\"\n" +
                // Type 1, Return 0 -> Receipt
                "\"REC-101\",\"DOC-1\",\"89\",\"1\",\"1\",\"False\",\"20.0000\",\"KG\",\"2026-05-01T10:00:00Z\"\n" +
                // Type 1, Return 1 -> Return to Vendor (Return)
                "\"REC-102\",\"DOC-2\",\"89\",\"1\",\"1\",\"True\",\"5.0000\",\"KG\",\"2026-05-02T10:00:00Z\"\n" +
                // Type 2, Return 0 -> Consumption
                "\"DEL-201\",\"DOC-3\",\"89\",\"1\",\"2\",\"False\",\"10.0000\",\"KG\",\"2026-05-03T10:00:00Z\"\n" +
                // Type 2, Return 1 -> Adjustment (Return from production)
                "\"DEL-202\",\"DOC-4\",\"89\",\"1\",\"2\",\"True\",\"2.0000\",\"KG\",\"2026-05-04T10:00:00Z\"\n" +
                // Unsupported Type 3 -> Unknown (0)
                "\"REC-301\",\"DOC-5\",\"360\",\"1\",\"3\",\"False\",\"15.0000\",\"KG\",\"2026-05-05T10:00:00Z\"\n" +
                // Unsupported Type 4 -> Unknown (0)
                "\"DEL-401\",\"DOC-6\",\"360\",\"1\",\"4\",\"False\",\"8.0000\",\"KG\",\"2026-05-06T10:00:00Z\"\n");

            var reader = new SepidarCsvPilotReader(tempDir);
            var result = await reader.CaptureAsync("1", "1405");

            // Consistency is unverified for CSV samples, but MissingMovementData is false because valid movement lines exist
            Assert.False(result.IsConsistent);
            Assert.Contains("PILOT_TRANSACTIONAL_SNAPSHOT_UNVERIFIED", result.FailureReason);
            Assert.False(result.MissingMovementData);
            Assert.Equal(2, result.StockCount);
            Assert.Equal(6, result.MovementCount);

            var movs = result.Capture!.Movements;

            // REC-101: Receipt
            Assert.Equal(InventoryMovementType.Receipt, movs.Single(m => m.VoucherRef == "REC-101").MovementType);

            // REC-102: Return
            Assert.Equal(InventoryMovementType.Return, movs.Single(m => m.VoucherRef == "REC-102").MovementType);

            // DEL-201: Consumption
            Assert.Equal(InventoryMovementType.Consumption, movs.Single(m => m.VoucherRef == "DEL-201").MovementType);

            // DEL-202: Adjustment
            Assert.Equal(InventoryMovementType.Adjustment, movs.Single(m => m.VoucherRef == "DEL-202").MovementType);

            // REC-301: Unknown (Unsupported)
            Assert.Equal(InventoryMovementType.Unknown, movs.Single(m => m.VoucherRef == "REC-301").MovementType);

            // DEL-401: Unknown (Unsupported)
            Assert.Equal(InventoryMovementType.Unknown, movs.Single(m => m.VoucherRef == "DEL-401").MovementType);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task PilotPreviewTool_WithSepidarCsvPilotReader_GeneratesPreviewAndPreservesCrmFields()
    {
        var dirs = GetEmpiricalSampleDirectories();
        var reader = new SepidarCsvPilotReader(dirs);

        // Arrange in-memory CRM DB
        var dbOptions = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase("PP_CsvReaderPreview_" + Guid.NewGuid().ToString("N")).Options;
        await using var db = new CrmDbContext(dbOptions);

        var wh = new Warehouse { Code = "WH-MAIN", Name = "انبار اصلی", SepidarWarehouseRef = "1", IsActive = true };
        var kg = new UnitOfMeasure { Code = "KG", Name = "كيلوگرم", IsActive = true };
        var mat369 = new Material { InternalCode = "MAT-RAW-369", Name = "ماده اولیه ۳۶۹ بومی", BaseUnitId = kg.Id, IsActive = true };
        var mat89 = new Material { InternalCode = "MAT-RAW-89", Name = "ماده اولیه ۸۹ بومی", BaseUnitId = kg.Id, IsActive = true };

        db.Warehouses.Add(wh);
        db.UnitsOfMeasure.Add(kg);
        db.Materials.AddRange(mat369, mat89);

        db.InventoryItemMappings.AddRange(
            new InventoryItemMapping { MaterialId = mat369.Id, SepidarItemId = "369", SepidarItemCode = "369", SepidarItemTitle = "ماده ۳۶۹ در سپیدار", SepidarBaseUnit = "KG", IsActive = true },
            new InventoryItemMapping { MaterialId = mat89.Id, SepidarItemId = "89", SepidarItemCode = "89", SepidarItemTitle = "ماده ۸۹ در سپیدار", SepidarBaseUnit = "KG", IsActive = true }
        );

        db.InventorySnapshots.AddRange(
            new InventorySnapshot
            {
                WarehouseId = wh.Id,
                MaterialId = mat369.Id,
                UnitId = kg.Id,
                OnHandQuantity = 100m,
                ReservedQuantity = 45m, // CRM-owned field: must remain 45m!
                IncomingQuantity = 70m, // CRM-owned field: must remain 70m!
                ReservableQuantity = 55m,
                SnapshotTimeUtc = DateTime.UtcNow.AddDays(-1),
                SourceSnapshotKey = "before-369"
            },
            new InventorySnapshot
            {
                WarehouseId = wh.Id,
                MaterialId = mat89.Id,
                UnitId = kg.Id,
                OnHandQuantity = 20m,
                ReservedQuantity = 10m,
                IncomingQuantity = 20m,
                ReservableQuantity = 10m,
                SnapshotTimeUtc = DateTime.UtcNow.AddDays(-1),
                SourceSnapshotKey = "before-89"
            }
        );
        await db.SaveChangesAsync();

        var previewOptions = new PilotPreviewOptions(
            WarehouseCode: "WH-MAIN",
            SourceWarehouseId: "1",
            FiscalYearTitle: "1405",
            DataProvenance: "CSV_EMPIRICAL_SAMPLES",
            SemanticContractVerified: false);

        var preview = await InventoryPilotPreviewTool.GeneratePreviewAsync(db, reader, previewOptions);

        // Safety Constraint: Offline CSV reader must remain IsExecutable = false
        Assert.False(preview.IsExecutable);
        Assert.NotNull(preview.FailureReason);
        Assert.Contains("PILOT_SEMANTIC_CONTRACT_UNVERIFIED", preview.FailureReason);
        Assert.Contains("PILOT_TRANSACTIONAL_SNAPSHOT_UNVERIFIED", preview.FailureReason);

        // Human-readable report checks
        Assert.Contains("سازگاری تراکنشی (Snapshot)** | ❌ تأییدنشده", preview.HumanReadableReport);
        Assert.Contains("MissingMovementData", preview.HumanReadableReport);
        Assert.Contains("Input File Manifest", preview.HumanReadableReport);

        // Manifest contains the 2 mapped items
        Assert.Equal(2, preview.ManifestItemCount);

        // Item 369: OnHand matches CSV stored quantity (-1590.0000m), CRM Reserved & Incoming preserved
        var change369 = preview.SnapshotChanges.Single(c => c.Before.MaterialId == mat369.Id);
        Assert.Equal(100m, change369.Before.OnHand);
        Assert.Equal(-1590.0000m, change369.After.OnHand);
        Assert.Equal(45m, change369.After.Reserved);
        Assert.Equal(70m, change369.After.Incoming);

        // Negative stock warning is present
        Assert.Contains("NEGATIVE_STOCK_DISCREPANCY", preview.HumanReadableReport);
        Assert.Contains("369", preview.HumanReadableReport);

        // Item 89: OnHand matches CSV stored quantity (107.1500m)
        var change89 = preview.SnapshotChanges.Single(c => c.Before.MaterialId == mat89.Id);
        Assert.Equal(20m, change89.Before.OnHand);
        Assert.Equal(107.1500m, change89.After.OnHand);
        Assert.Equal(10m, change89.After.Reserved);
        Assert.Equal(20m, change89.After.Incoming);

        // Zero changes written to CRM DbContext
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
    }

    [Fact]
    public async Task SepidarCsvPilotReader_MissingMovementItems_DeclaresMissingMovementData_CannotDeduceFromDocumentNumbersCsv()
    {
        var dirs = GetEmpiricalSampleDirectories();
        var reader = new SepidarCsvPilotReader(dirs);
        var result = await reader.CaptureAsync("1", "1405");

        // 1. Must flag MissingMovementData
        Assert.True(result.MissingMovementData, "Absence of movement item CSV must declare MissingMovementData = true.");
        Assert.Equal(0, result.MovementCount);

        // 2. Receipt and Delivery document numbers files exist in sample part 3, but do NOT provide movement item rows
        Assert.NotNull(result.InputManifest);
        var receiptHeaderEntry = result.InputManifest.FirstOrDefault(m => m.Role == "ReceiptsDocumentHeaders");
        var deliveryHeaderEntry = result.InputManifest.FirstOrDefault(m => m.Role == "DeliveriesDocumentHeaders");

        Assert.NotNull(receiptHeaderEntry);
        Assert.NotNull(deliveryHeaderEntry);
        Assert.True(File.Exists(receiptHeaderEntry.Path));
        Assert.True(File.Exists(deliveryHeaderEntry.Path));

        // 3. Inspect headers of receipt document numbers: contains document header info but NO ItemRef or Quantity
        var lines = await File.ReadAllLinesAsync(receiptHeaderEntry.Path);
        Assert.NotEmpty(lines);
        var header = lines[0];
        Assert.Contains("DocumentNumber", header);
        Assert.DoesNotContain("ItemRef", header);
        Assert.DoesNotContain("Quantity", header);
    }

    [Fact]
    public async Task InventoryPilotPreviewTool_Item360InPreviewWithoutMovements_IncludesExplicitUnsupportedMovementRiskDisclaimer()
    {
        var dirs = GetEmpiricalSampleDirectories();
        var reader = new SepidarCsvPilotReader(dirs);

        var dbOptions = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase("PP_Item360DisclaimerTest_" + Guid.NewGuid().ToString("N")).Options;
        await using var db = new CrmDbContext(dbOptions);

        var wh = new Warehouse { Code = "WH-MAIN", Name = "انبار [SYNTHETIC_TEST_DATA]", SepidarWarehouseRef = "1", IsActive = true };
        var kg = new UnitOfMeasure { Code = "KG", Name = "كيلوگرم", IsActive = true };
        var mat360 = new Material { InternalCode = "MAT-RAW-360", Name = "ماده ۳۶۰ [SYNTHETIC_TEST_DATA]", BaseUnitId = kg.Id, IsActive = true };

        db.Warehouses.Add(wh);
        db.UnitsOfMeasure.Add(kg);
        db.Materials.Add(mat360);
        db.InventoryItemMappings.Add(new InventoryItemMapping { MaterialId = mat360.Id, SepidarItemId = "360", SepidarItemCode = "360", SepidarItemTitle = "ماده ۳۶۰", SepidarBaseUnit = "KG", IsActive = true });
        db.InventorySnapshots.Add(new InventorySnapshot
        {
            WarehouseId = wh.Id,
            MaterialId = mat360.Id,
            UnitId = kg.Id,
            OnHandQuantity = 500m,
            ReservedQuantity = 50m,
            IncomingQuantity = 0m,
            ReservableQuantity = 450m,
            SnapshotTimeUtc = DateTime.UtcNow.AddDays(-1),
            SourceSnapshotKey = "SYNTHETIC_CRM_BASELINE_360"
        });
        await db.SaveChangesAsync();

        var previewOptions = new PilotPreviewOptions(
            WarehouseCode: "WH-MAIN",
            SourceWarehouseId: "1",
            FiscalYearTitle: "1405",
            DataProvenance: "CSV_EMPIRICAL_SAMPLES",
            SemanticContractVerified: false);

        var preview = await InventoryPilotPreviewTool.GeneratePreviewAsync(db, reader, previewOptions);

        // Verification:
        // 1. Must be unexecutable
        Assert.False(preview.IsExecutable);

        // 2. Item 360 is present in the snapshot preview diff
        Assert.Contains(preview.SnapshotChanges, c => c.Before.MaterialId == mat360.Id);

        // 3. Prominently disclaims that presence of Item 360 in the absence of movements data is NOT proof of no unsupported movements
        Assert.Contains("سلب مسئولیت قلم ۳۶۰", preview.HumanReadableReport);
        Assert.Contains("اثبات‌کننده نداشتن گردش‌های پشتیبانی‌نشده (Unsupported) در واقعیت نیست", preview.HumanReadableReport);
        Assert.Contains("ITEM_360_UNSUPPORTED_RISK_DISCLAIMER", preview.JsonOutput);
        Assert.Contains("SYNTHETIC_TEST_DATA", preview.HumanReadableReport);
    }
    [Fact]
    public async Task ExplicitMissingFile_MustNotFallBackToHistoricalSamples()
    {
        var reader = new SepidarCsvPilotReader(new SepidarCsvReaderOptions
        {
            FiscalYearsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv"),
            SearchDirectories = GetEmpiricalSampleDirectories()
        });
        var result = await reader.CaptureAsync();
        Assert.Null(result.Capture);
        Assert.Contains("fallback is prohibited", result.FailureReason);
    }

    [Fact]
    public async Task RunsRoot_MustNotDiscoverNestedHistoricalSamples()
    {
        var result = await new SepidarCsvPilotReader(RunsDir).CaptureAsync();
        Assert.Null(result.Capture);
        Assert.False(result.IsConsistent);
    }

    [Fact]
    public async Task AmbiguousFiscalYears_MustRequireExplicitPath()
    {
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(temp);
        try
        {
            File.WriteAllText(Path.Combine(temp, "01_fiscal_years.csv"), "ambiguous");
            var dirs = GetEmpiricalSampleDirectories().Append(temp).ToArray();
            var result = await new SepidarCsvPilotReader(dirs).CaptureAsync();
            Assert.Null(result.Capture);
            Assert.Contains("Ambiguous CSV input", result.FailureReason);
        }
        finally { Directory.Delete(temp, true); }
    }}
