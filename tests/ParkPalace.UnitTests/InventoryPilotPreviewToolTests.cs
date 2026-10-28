using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Services;
using Xunit;

namespace ParkPalace.UnitTests;

public class InventoryPilotPreviewToolTests
{
    private sealed class FakeSepidarPilotReader(SepidarPilotCaptureResult result) : ISepidarPilotReader
    {
        public Task<SepidarPilotCaptureResult> CaptureAsync(
            string warehouseRef = "1",
            string fiscalYearTitle = "1405",
            CancellationToken ct = default) => Task.FromResult(result);
    }

    private static (CrmDbContext Db, Guid WhId, Guid MatId, Guid KgId) CreateSeededContext()
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
        db.UnitsOfMeasure.Add(kg);

        var mat = new Material
        {
            Id = Guid.NewGuid(),
            InternalCode = "MAT-FLOUR",
            Name = "آرد ستاره",
            BaseUnitId = kg.Id,
            IsActive = true
        };
        db.Materials.Add(mat);

        db.InventoryItemMappings.Add(new InventoryItemMapping
        {
            MaterialId = mat.Id,
            SepidarItemId = "SEP-1001",
            SepidarBaseUnit = "KG",
            IsActive = true
        });

        db.InventorySnapshots.Add(new InventorySnapshot
        {
            WarehouseId = wh.Id,
            MaterialId = mat.Id,
            UnitId = kg.Id,
            OnHandQuantity = 500m,
            ReservedQuantity = 120m, // CRM owned
            IncomingQuantity = 80m,  // CRM owned
            ReservableQuantity = 380m,
            SnapshotTimeUtc = DateTime.UtcNow.AddHours(-2),
            SourceSnapshotKey = "initial-crm-key"
        });

        db.SaveChanges();
        return (db, wh.Id, mat.Id, kg.Id);
    }

    [Fact]
    public async Task GeneratePreview_WhenReaderIsInconsistent_ReturnsUnexecutable()
    {
        var (db, _, _, _) = CreateSeededContext();
        var fakeReader = new FakeSepidarPilotReader(new SepidarPilotCaptureResult(
            IsConsistent: false,
            FailureReason: "PILOT_FISCAL_YEAR_NOT_FOUND: دوره مالی ۱۴۰۵ در جدول FMK.FiscalYear یافت نشد.",
            FiscalYear: null,
            Capture: null,
            CapturedAtUtc: null,
            StockCount: 0,
            MovementCount: 0));

        var result = await InventoryPilotPreviewTool.GeneratePreviewAsync(db, fakeReader);

        Assert.False(result.IsExecutable);
        Assert.Contains("PILOT_FISCAL_YEAR_NOT_FOUND", result.FailureReason);
        Assert.Contains("غیرقابل اجرا", result.HumanReadableReport);
        Assert.Empty(db.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged));
    }

    [Fact]
    public async Task GeneratePreview_WhenCaptureHasValidDataButSnapshotIsUnverified_GeneratesDiagnosticReportWithUnexecutableStatus()
    {
        var (db, _, _, _) = CreateSeededContext();
        var capturedAt = DateTime.UtcNow;
        var fy = new SepidarFiscalYearInfo(10, "1405", new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 3, 20, 0, 0, 0, DateTimeKind.Utc));

        var stocks = new[]
        {
            new SepidarStockRecord("SEP-1001", "1", 850m, null, 999m, 999m, "KG", capturedAt)
        };
        var capture = new PilotSourceCapture(10, capturedAt, stocks, Array.Empty<SepidarMovementRecord>());

        var fakeReader = new FakeSepidarPilotReader(new SepidarPilotCaptureResult(
            IsConsistent: false,
            FailureReason: "PILOT_TRANSACTIONAL_SNAPSHOT_UNVERIFIED: نمونه‌های مجزای CSV حاصل برداشت هم‌زمان نیستند.",
            FiscalYear: fy,
            Capture: capture,
            CapturedAtUtc: capturedAt,
            StockCount: 1,
            MovementCount: 0,
            MissingMovementData: true));

        var result = await InventoryPilotPreviewTool.GeneratePreviewAsync(db, fakeReader);

        // Crucial behavior: Diagnostic report is produced, but is strictly unexecutable
        Assert.False(result.IsExecutable);
        Assert.Contains("PILOT_TRANSACTIONAL_SNAPSHOT_UNVERIFIED", result.FailureReason);
        Assert.Equal(1, result.ManifestItemCount);
        Assert.Equal(1, result.SnapshotModifiedCount);
        Assert.Contains("سازگاری تراکنشی (Snapshot)** | ❌ تأییدنشده", result.HumanReadableReport);
        Assert.Contains("MissingMovementData", result.HumanReadableReport);
    }

    [Fact]
    public async Task GeneratePreview_WithConsistentSource_ComputesPlanAndPreservesCrmFields()
    {
        var (db, _, _, _) = CreateSeededContext();
        var capturedAt = DateTime.UtcNow;
        var fy = new SepidarFiscalYearInfo(10, "1405", new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 3, 21, 0, 0, 0, DateTimeKind.Utc));

        var stocks = new[]
        {
            new SepidarStockRecord("SEP-1001", "1", 850m, null, 999m, 999m, "KG", capturedAt)
        };
        var movements = new[]
        {
            new SepidarMovementRecord("REC-501", "501", "SEP-1001", "1", InventoryMovementType.Receipt, 350m, "KG", capturedAt.AddDays(-1), 4, false, null)
        };
        var capture = new PilotSourceCapture(10, capturedAt, stocks, movements);

        var fakeReader = new FakeSepidarPilotReader(new SepidarPilotCaptureResult(
            IsConsistent: true,
            FailureReason: null,
            FiscalYear: fy,
            Capture: capture,
            CapturedAtUtc: capturedAt,
            StockCount: 1,
            MovementCount: 1));

        var result = await InventoryPilotPreviewTool.GeneratePreviewAsync(db, fakeReader);

        Assert.True(result.IsExecutable);
        Assert.NotNull(result.ManifestHash);
        Assert.NotNull(result.PlanHash);
        Assert.Equal(1, result.ManifestItemCount);
        Assert.Equal(1, result.SnapshotModifiedCount);
        Assert.Equal(1, result.MovementInsertedCount);

        // Check snapshot change preserves CRM Reserved and Incoming
        var change = Assert.Single(result.SnapshotChanges);
        Assert.Equal(500m, change.Before.OnHand);
        Assert.Equal(850m, change.After.OnHand);
        Assert.Equal(120m, change.After.Reserved); // Preserved!
        Assert.Equal(80m, change.After.Incoming);   // Preserved!

        // Read-only guarantee: change tracker has no added, modified or deleted entries
        Assert.Empty(db.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged));
    }

    [Fact]
    public async Task GeneratePreview_ExcludesItemsAndMovementsOutsideManifest()
    {
        var (db, _, _, _) = CreateSeededContext();
        var capturedAt = DateTime.UtcNow;
        var fy = new SepidarFiscalYearInfo(10, "1405", new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 3, 21, 0, 0, 0, DateTimeKind.Utc));

        var stocks = new[]
        {
            new SepidarStockRecord("SEP-1001", "1", 850m, null, 999m, 999m, "KG", capturedAt),
            new SepidarStockRecord("SEP-OUTSIDE", "1", 200m, null, 999m, 999m, "KG", capturedAt) // Outside manifest
        };
        var movements = new[]
        {
            new SepidarMovementRecord("REC-501", "501", "SEP-1001", "1", InventoryMovementType.Receipt, 350m, "KG", capturedAt.AddDays(-1), 4, false, null),
            new SepidarMovementRecord("REC-999", "999", "SEP-OUTSIDE", "1", InventoryMovementType.Receipt, 50m, "KG", capturedAt.AddDays(-2), 4, false, null) // Outside manifest
        };
        var capture = new PilotSourceCapture(10, capturedAt, stocks, movements);

        var fakeReader = new FakeSepidarPilotReader(new SepidarPilotCaptureResult(
            IsConsistent: true,
            FailureReason: null,
            FiscalYear: fy,
            Capture: capture,
            CapturedAtUtc: capturedAt,
            StockCount: 2,
            MovementCount: 2));

        var result = await InventoryPilotPreviewTool.GeneratePreviewAsync(db, fakeReader);

        Assert.True(result.IsExecutable);
        Assert.Equal(1, result.ExcludedStockRows);
        Assert.Equal(1, result.ExcludedMovementRows);
    }

    [Fact]
    public async Task GeneratePreview_WhenLiveConnectionWithoutSemanticVerification_ReturnsUnexecutable()
    {
        var (db, _, _, _) = CreateSeededContext();
        var capturedAt = DateTime.UtcNow;
        var fy = new SepidarFiscalYearInfo(10, "1405", new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 3, 21, 0, 0, 0, DateTimeKind.Utc));

        var stocks = new[] { new SepidarStockRecord("SEP-1001", "1", 850m, null, 999m, 999m, "KG", capturedAt) };
        var movements = new[] { new SepidarMovementRecord("REC-501", "501", "SEP-1001", "1", InventoryMovementType.Receipt, 350m, "KG", capturedAt.AddDays(-1), 4, false, null) };
        var capture = new PilotSourceCapture(10, capturedAt, stocks, movements);

        var fakeReader = new FakeSepidarPilotReader(new SepidarPilotCaptureResult(
            IsConsistent: true,
            FailureReason: null,
            FiscalYear: fy,
            Capture: capture,
            CapturedAtUtc: capturedAt,
            StockCount: 1,
            MovementCount: 1));

        var options = new PilotPreviewOptions(
            DataProvenance: "LIVE_READONLY_CONNECTION",
            SemanticContractVerified: false);

        var result = await InventoryPilotPreviewTool.GeneratePreviewAsync(db, fakeReader, options);

        Assert.False(result.IsExecutable);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("PILOT_SEMANTIC_CONTRACT_UNVERIFIED", result.FailureReason);
        Assert.Contains("UNEXECUTABLE", result.HumanReadableReport);
        Assert.Contains("UNEXECUTABLE_SEMANTIC_CONTRACT_UNVERIFIED", result.JsonOutput);
    }

    [Fact]
    public async Task GeneratePreview_WhenLiveProvenanceEvenWithSemanticVerification_ReturnsUnexecutable()
    {
        var (db, _, _, _) = CreateSeededContext();
        var capturedAt = DateTime.UtcNow;
        var fy = new SepidarFiscalYearInfo(10, "1405", new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 3, 21, 0, 0, 0, DateTimeKind.Utc));

        var stocks = new[] { new SepidarStockRecord("SEP-1001", "1", 850m, null, 999m, 999m, "KG", capturedAt) };
        var movements = new[] { new SepidarMovementRecord("REC-501", "501", "SEP-1001", "1", InventoryMovementType.Receipt, 350m, "KG", capturedAt.AddDays(-1), 4, false, null) };
        var capture = new PilotSourceCapture(10, capturedAt, stocks, movements);

        var fakeReader = new FakeSepidarPilotReader(new SepidarPilotCaptureResult(
            IsConsistent: true,
            FailureReason: null,
            FiscalYear: fy,
            Capture: capture,
            CapturedAtUtc: capturedAt,
            StockCount: 1,
            MovementCount: 1));

        // Even with SemanticContractVerified = true, Live data cannot be executable until real Sepidar is implemented!
        var options = new PilotPreviewOptions(
            DataProvenance: "LIVE_READONLY_CONNECTION",
            SemanticContractVerified: true);

        var result = await InventoryPilotPreviewTool.GeneratePreviewAsync(db, fakeReader, options);

        Assert.False(result.IsExecutable);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("PILOT_SEMANTIC_CONTRACT_UNVERIFIED", result.FailureReason);
        Assert.Contains("ارسال سوئیچ --verify-semantic-contract نمی‌تواند این محدودیت را دور بزند", result.FailureReason);
    }

    [Fact]
    public void ValidateOutputPath_WhenFileAlreadyExists_ThrowsInvalidOperationException()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                InventoryPilotPreviewTool.ValidateOutputPath(tempFile));
            Assert.Contains("OUTPUT_FILE_ALREADY_EXISTS", ex.Message);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Production.json")]
    [InlineData("web.config")]
    [InlineData(".env")]
    [InlineData("secrets.json")]
    [InlineData("crm-web.cer")]
    public void ValidateOutputPath_WhenTargetIsSensitiveFile_ThrowsInvalidOperationException(string sensitiveFileName)
    {
        var target = Path.Combine(Path.GetTempPath(), "test_" + Guid.NewGuid().ToString("N"), sensitiveFileName);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            InventoryPilotPreviewTool.ValidateOutputPath(target));
        Assert.Contains("SENSITIVE_OUTPUT_TARGET_REJECTED", ex.Message);
    }

    [Fact]
    public void ValidateOutputPath_WhenTargetMatchesSensitiveConfigFile_ThrowsInvalidOperationException()
    {
        var configPath = Path.Combine(Path.GetTempPath(), "my_external_secrets.json");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            InventoryPilotPreviewTool.ValidateOutputPath(configPath, sensitiveConfigPath: configPath));
        Assert.Contains("SENSITIVE_OUTPUT_TARGET_REJECTED", ex.Message);
    }
}
