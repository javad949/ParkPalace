using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;

namespace ParkPalace.Infrastructure.Services;

public static class InventoryPilotCliRunner
{
    public static async Task<int> RunAsync(string[] args, CancellationToken ct = default)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("==========================================================");
        Console.WriteLine(" ParkPalace CRM - Inventory Pilot Preview CLI Runner");
        Console.WriteLine(" Mode: Strictly Read-Only Preview (Zero Writes)");
        Console.WriteLine("==========================================================");

        string runId = GetArg(args, "--run-id", $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}");
        string warehouseCode = GetArg(args, "--warehouse", "WH-MAIN");
        string sourceWhId = GetArg(args, "--source-wh", "1");
        string fiscalYearTitle = GetArg(args, "--fiscal-year", "1405");
        
        string defaultRunDir = Path.Combine("docs", "runs", runId);
        string outputJson = GetArg(args, "--output-json", Path.Combine(defaultRunDir, "pilot_preview_output.json"));
        string outputReport = GetArg(args, "--output-report", Path.Combine(defaultRunDir, "pilot_preview_report.md"));
        
        bool simulatedTest = args.Contains("--simulated-test") || args.Contains("-SimulatedTest");
        string? csvDir = GetArg(args, "--csv-dir", null);
        bool verifySemanticContract = args.Contains("--verify-semantic-contract");
        string? configFile = GetArg(args, "--config-file", null);

        // Pre-validate output paths: reject existing files (prevent overwriting prior reports) and sensitive targets
        try
        {
            InventoryPilotPreviewTool.ValidateOutputPath(outputJson, configFile);
            InventoryPilotPreviewTool.ValidateOutputPath(outputReport, configFile);
        }
        catch (InvalidOperationException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] اعتبارسنجی مسیر خروجی ناموفق بود: {ex.Message}");
            Console.ResetColor();
            return 1;
        }

        // Compute binary hash to record exact executed artifact provenance
        string binaryHash = ComputeExecutingBinaryHash();

        Console.WriteLine($"* Run ID: {runId}");
        Console.WriteLine($"* Executed Binary SHA256: {binaryHash}");
        Console.WriteLine($"* Warehouse Target: {warehouseCode} (Source: {sourceWhId})");
        Console.WriteLine($"* Fiscal Year: {fiscalYearTitle}");
        Console.WriteLine($"* Output JSON: {outputJson}");
        Console.WriteLine($"* Output Report: {outputReport}");

        string? crmConn = Environment.GetEnvironmentVariable("PARKPALACE_CRM_CONN");
        string? sepidarConn = Environment.GetEnvironmentVariable("PARKPALACE_SEPIDAR_CONN");

        // Optionally load secure config file from outside repository if specified
        if (!string.IsNullOrWhiteSpace(configFile) && File.Exists(configFile))
        {
            try
            {
                var jsonContent = await File.ReadAllTextAsync(configFile, ct);
                using var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;
                if (root.TryGetProperty("CrmConnectionString", out var crmProp)) crmConn = crmProp.GetString();
                if (root.TryGetProperty("SepidarConnectionString", out var sepProp)) sepidarConn = sepProp.GetString();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ERROR] خواندن فایل تنظیمات امن ({configFile}) ناموفق بود: {ex.Message}");
                Console.ResetColor();
                return 1;
            }
        }

        bool isOfflineSource = simulatedTest || !string.IsNullOrWhiteSpace(csvDir);

        if (!isOfflineSource)
        {
            if (string.IsNullOrWhiteSpace(crmConn))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[ERROR] مقصد پایگاه داده CRM صریحاً مشخص نشده است.");
                Console.WriteLine("اتصال پیش‌فرض به CRM محلی حذف شده است. لطفاً متغیر محیطی PARKPALACE_CRM_CONN یا فایل تنظیمات امن را مشخص کنید.");
                Console.ResetColor();
                return 1;
            }

            if (string.IsNullOrWhiteSpace(sepidarConn))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[ERROR] مقصد پایگاه داده سپیدار صریحاً مشخص نشده است.");
                Console.WriteLine("لطفاً متغیر محیطی PARKPALACE_SEPIDAR_CONN یا فایل تنظیمات امن را مشخص کنید.");
                Console.WriteLine("برای اجرای نمونه آزمایشی محلی بدون نیاز به اتصال، سوئیچ --simulated-test یا --csv-dir را ارسال فرمایید.");
                Console.ResetColor();
                return 1;
            }

            Console.WriteLine($"* CRM Database Target: {SanitizeTarget(crmConn)}");
            Console.WriteLine($"* Sepidar Database Target: {SanitizeTarget(sepidarConn)}");
        }
        CrmDbContext crmDb;
        ISepidarPilotReader sepidarReader;

        if (!string.IsNullOrWhiteSpace(csvDir))
        {
            Console.WriteLine($"* CSV Source Directory: {csvDir}");
            // Offline CSV diagnostics never use ambient production connection strings.
            var options = new DbContextOptionsBuilder<CrmDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
            crmDb = new CrmDbContext(options);
            await SeedCrmBaselineAsync(crmDb, warehouseCode, sourceWhId, ct);
            string? fyFile = GetArg(args, "--fiscal-years-file", null);
            string? stFile = GetArg(args, "--stocks-file", null);
            string? uFile = GetArg(args, "--units-file", null);
            string? sumFile = GetArg(args, "--summary-file", null);
            string? movFile = GetArg(args, "--movements-file", null);
            string? recFile = GetArg(args, "--receipts-header-file", null);
            string? delFile = GetArg(args, "--deliveries-header-file", null);

            var fullCsvDir = Path.GetFullPath(csvDir);
            var resolvedSearchDirs = new List<string> { fullCsvDir };
            
            var csvOptions = new SepidarCsvReaderOptions
            {
                FiscalYearsPath = fyFile,
                StocksPath = stFile,
                UnitsPath = uFile,
                StockSummaryPath = sumFile,
                MovementsPath = movFile,
                ReceiptsHeaderPath = recFile,
                DeliveriesHeaderPath = delFile,
                SearchDirectories = resolvedSearchDirs
            };

            sepidarReader = new SepidarCsvPilotReader(csvOptions);
        }
        else if (simulatedTest)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[MODE] در حال اجرای پیش‌نمایش روی داده‌های آزمایشی شبیه‌سازی‌شده (SIMULATED_TEST)...");
            Console.WriteLine("توجه: این خروجی منشأ آزمایشی دارد و وضعیت زنده سپیدار نیست.");
            Console.ResetColor();

            var options = new DbContextOptionsBuilder<CrmDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
            crmDb = new CrmDbContext(options);

            await SeedCrmBaselineAsync(crmDb, warehouseCode, sourceWhId, ct);

            // Source reader using exact empirical values from CSV sample:
            // Year 1405: FiscalYearId = 10, 2026-03-21 to 2027-03-20
            var capturedAt = DateTime.UtcNow;
            var fy = new SepidarFiscalYearInfo(10, fiscalYearTitle,
                new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2027, 3, 20, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2027, 3, 21, 0, 0, 0, DateTimeKind.Utc));

            // Empirical CSV OnHand stocks:
            // Item 369 = -1590.0000 KG, Item 89 = 107.1500 KG, Item 360 = 1510.4100 KG
            var simStocks = new[]
            {
                new SepidarStockRecord("369", sourceWhId, -1590.0000m, null, 999m, 999m, "KG", capturedAt),
                new SepidarStockRecord("89", sourceWhId, 107.1500m, null, 999m, 999m, "KG", capturedAt),
                new SepidarStockRecord("360", sourceWhId, 1510.4100m, null, 999m, 999m, "KG", capturedAt)
            };

            // Movements clearly separated:
            // 1. Assumed Receipt for Item 89 (Labeled SYNTHETIC_TEST_MOVEMENT)
            // 2. Assumed Delivery for Item 369 (Labeled SYNTHETIC_TEST_MOVEMENT)
            // 3. UNSUPPORTED movement for Item 360 -> Item 360 will be excluded with ITEM_HAS_UNSUPPORTED_MOVEMENT
            // 4. UNSUPPORTED movement for an OUTSIDE item (Item 7777, not in CRM) -> Must NOT abort the pilot!
            var simMovements = new[]
            {
                new SepidarMovementRecord("REC-90001", "TEST-REC-01", "89", sourceWhId,
                    InventoryMovementType.Receipt, 100m, "KG",
                    new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc), 4, false, "SYNTHETIC_TEST_MOVEMENT"),
                new SepidarMovementRecord("DEL-90002", "TEST-DEL-01", "369", sourceWhId,
                    InventoryMovementType.Consumption, 50m, "KG",
                    new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc), 4, false, "SYNTHETIC_TEST_MOVEMENT"),
                new SepidarMovementRecord("REC-90003", "TEST-DOC-1469", "360", sourceWhId,
                    InventoryMovementType.Unknown, 25m, "KG",
                    new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc), 4, false, "SYNTHETIC_UNSUPPORTED_IN_SCOPE"),
                new SepidarMovementRecord("REC-90004", "TEST-DOC-644", "7777", sourceWhId,
                    InventoryMovementType.Unknown, 10m, "KG",
                    new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc), 2, false, "SYNTHETIC_UNSUPPORTED_OUTSIDE_MANIFEST")
            };

            var simCapture = new PilotSourceCapture(10, capturedAt, simStocks, simMovements);

            sepidarReader = new SimulatedTestPilotReader(new SepidarPilotCaptureResult(
                IsConsistent: true,
                FailureReason: null,
                FiscalYear: fy,
                Capture: simCapture,
                CapturedAtUtc: capturedAt,
                StockCount: simStocks.Length,
                MovementCount: simMovements.Length));
        }
        else
        {
            Console.WriteLine("\n[MODE] در حال اتصال به پایگاه داده با سطح ایزولاسیون Snapshot...");
            var dbOptions = new DbContextOptionsBuilder<CrmDbContext>()
                .UseSqlServer(crmConn!).Options;
            crmDb = new CrmDbContext(dbOptions);
            sepidarReader = new SepidarPilotReader(sepidarConn!, NullLogger.Instance, IsolationLevel.Snapshot);
        }

        var previewOptions = new PilotPreviewOptions(
            WarehouseCode: warehouseCode,
            SourceWarehouseId: sourceWhId,
            FiscalYearTitle: fiscalYearTitle,
            OutputJsonPath: outputJson,
            OutputReportPath: outputReport,
            DataProvenance: GetArg(args, "--provenance", !string.IsNullOrWhiteSpace(csvDir) ? "CSV_EMPIRICAL_SAMPLES" : (simulatedTest ? "ASSUMED_LOCAL_SCENARIO" : "LIVE_READONLY_CONNECTION")),
            SemanticContractVerified: verifySemanticContract,
            RunId: runId,
            BinaryHash: binaryHash,
            ConfigFilePath: configFile);

        PilotPreviewExecutionResult result;
        try
        {
            result = await InventoryPilotPreviewTool.GeneratePreviewAsync(crmDb, sepidarReader, previewOptions, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("OUTPUT_FILE_ALREADY_EXISTS") || ex.Message.Contains("SENSITIVE_OUTPUT_TARGET_REJECTED"))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] {ex.Message}");
            Console.ResetColor();
            return 1;
        }

        if (result.IsExecutable)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[SUCCESS] پیش‌نمایش با موفقیت تولید شد!");
            Console.WriteLine($"* شناسه اجرا: {runId}");
            Console.WriteLine($"* هش مانیفست: {result.ManifestHash}");
            Console.WriteLine($"* هش پلن: {result.PlanHash}");
            Console.WriteLine($"* تعداد اقلام مانیفست: {result.ManifestItemCount}");
            Console.WriteLine($"* اسنپ‌شات‌های تغییریافته: {result.SnapshotModifiedCount}");
            Console.WriteLine($"* گردش‌های انبار جدید: {result.MovementInsertedCount}");
            Console.WriteLine($"* فایل گزارش: {outputReport}");
            Console.WriteLine($"* فایل ساختاریافته: {outputJson}");
            Console.ResetColor();
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[UNEXECUTABLE] پیش‌نمایش به دلایل عدم اعتبارسنجی قرارداد معنایی یا دسترسی مبدأ غیرقابل اجراست:");
            Console.WriteLine($"علت: {result.FailureReason}");
            Console.WriteLine($"گزارش وضعیت در {outputReport} ذخیره شد.");
            Console.ResetColor();
            return 2;
        }
    }

    private static string GetArg(string[] args, string name, string? defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return defaultValue ?? "";
    }

    private static async Task SeedCrmBaselineAsync(CrmDbContext crmDb, string warehouseCode, string sourceWhId, CancellationToken ct)
    {
        var wh = new Warehouse { Code = warehouseCode, Name = "انبار مواد اولیه [ساختگی آزمایشی - SYNTHETIC_TEST_DATA]", SepidarWarehouseRef = sourceWhId, IsActive = true };
        var kg = new UnitOfMeasure { Code = "KG", Name = "كيلوگرم", IsActive = true };
        var countUnit = new UnitOfMeasure { Code = "COUNT", Name = "عدد", IsActive = true };
        
        var mat369 = new Material { InternalCode = "MAT-RAW-369", Name = "ماده اولیه ۳۶۹ [ساختگی آزمایشی - SYNTHETIC_TEST_DATA]", BaseUnitId = kg.Id, IsActive = true };
        var mat89 = new Material { InternalCode = "MAT-RAW-89", Name = "ماده اولیه ۸۹ [ساختگی آزمایشی - SYNTHETIC_TEST_DATA]", BaseUnitId = kg.Id, IsActive = true };
        var mat360 = new Material { InternalCode = "MAT-RAW-360", Name = "ماده اولیه ۳۶۰ [ساختگی آزمایشی - SYNTHETIC_TEST_DATA]", BaseUnitId = kg.Id, IsActive = true };
        var matCount = new Material { InternalCode = "MAT-COUNT-01", Name = "قلم متفرقه با واحد عدد [ساختگی آزمایشی - SYNTHETIC_TEST_DATA]", BaseUnitId = countUnit.Id, IsActive = true };

        crmDb.Warehouses.Add(wh);
        crmDb.UnitsOfMeasure.AddRange(kg, countUnit);
        crmDb.Materials.AddRange(mat369, mat89, mat360, matCount);

        crmDb.InventoryItemMappings.AddRange(
            new InventoryItemMapping
            {
                MaterialId = mat369.Id,
                SepidarItemId = "369",
                SepidarItemCode = "369",
                SepidarItemTitle = "ماده ۳۶۹ در سپیدار",
                SepidarBaseUnit = "KG",
                IsActive = true
            },
            new InventoryItemMapping
            {
                MaterialId = mat89.Id,
                SepidarItemId = "89",
                SepidarItemCode = "89",
                SepidarItemTitle = "ماده ۸۹ در سپیدار",
                SepidarBaseUnit = "KG",
                IsActive = true
            },
            new InventoryItemMapping
            {
                MaterialId = mat360.Id,
                SepidarItemId = "360",
                SepidarItemCode = "360",
                SepidarItemTitle = "ماده ۳۶۰ در سپیدار",
                SepidarBaseUnit = "KG",
                IsActive = true
            },
            new InventoryItemMapping
            {
                MaterialId = matCount.Id,
                SepidarItemId = "9999",
                SepidarItemCode = "9999",
                SepidarItemTitle = "قلم غیر کیلوگرم در سپیدار",
                SepidarBaseUnit = "عدد",
                IsActive = true
            }
        );

        crmDb.InventorySnapshots.AddRange(
            new InventorySnapshot
            {
                WarehouseId = wh.Id,
                MaterialId = mat369.Id,
                UnitId = kg.Id,
                OnHandQuantity = 200m,
                ReservedQuantity = 45m,   // Synthetic CRM baseline - must be preserved!
                IncomingQuantity = 70m,   // Synthetic CRM baseline - must be preserved!
                ReservableQuantity = 155m,
                SnapshotTimeUtc = DateTime.UtcNow.AddDays(-1),
                SourceSnapshotKey = "SYNTHETIC_CRM_BASELINE_369"
            },
            new InventorySnapshot
            {
                WarehouseId = wh.Id,
                MaterialId = mat89.Id,
                UnitId = kg.Id,
                OnHandQuantity = 50m,
                ReservedQuantity = 10m,   // Synthetic CRM baseline - must be preserved!
                IncomingQuantity = 20m,   // Synthetic CRM baseline - must be preserved!
                ReservableQuantity = 40m,
                SnapshotTimeUtc = DateTime.UtcNow.AddDays(-1),
                SourceSnapshotKey = "SYNTHETIC_CRM_BASELINE_89"
            },
            new InventorySnapshot
            {
                WarehouseId = wh.Id,
                MaterialId = mat360.Id,
                UnitId = kg.Id,
                OnHandQuantity = 1200m,
                ReservedQuantity = 100m,
                IncomingQuantity = 0m,
                ReservableQuantity = 1100m,
                SnapshotTimeUtc = DateTime.UtcNow.AddDays(-1),
                SourceSnapshotKey = "SYNTHETIC_CRM_BASELINE_360"
            }
        );
        await crmDb.SaveChangesAsync(ct);
    }

    private static string ComputeExecutingBinaryHash()
    {
        try
        {
            var loc = typeof(InventoryPilotCliRunner).Assembly.Location;
            if (File.Exists(loc))
            {
                using var sha = SHA256.Create();
                using var stream = File.OpenRead(loc);
                return Convert.ToHexString(sha.ComputeHash(stream));
            }
        }
        catch { /* best effort */ }
        return "UNKNOWN";
    }

    private static string SanitizeTarget(string? connStr)
    {
        if (string.IsNullOrWhiteSpace(connStr)) return "Not Configured";
        try
        {
            var b = new SqlConnectionStringBuilder(connStr);
            return $"Server={b.DataSource};Database={b.InitialCatalog};IntegratedSecurity={b.IntegratedSecurity}";
        }
        catch
        {
            return "[Protected Connection Target]";
        }
    }

    private sealed class SimulatedTestPilotReader(SepidarPilotCaptureResult result) : ISepidarPilotReader
    {
        public Task<SepidarPilotCaptureResult> CaptureAsync(string warehouseRef = "1", string fiscalYearTitle = "1405", CancellationToken ct = default) =>
            Task.FromResult(result);
    }
}
