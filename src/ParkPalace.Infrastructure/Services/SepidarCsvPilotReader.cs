using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services;

/// <summary>
/// Configuration options for the file-based CSV pilot reader.
/// </summary>
public sealed record SepidarCsvReaderOptions
{
    public string? FiscalYearsPath { get; init; }
    public string? StocksPath { get; init; }
    public string? UnitsPath { get; init; }
    public string? StockSummaryPath { get; init; }
    public string? MovementsPath { get; init; }
    public string? ReceiptsHeaderPath { get; init; }
    public string? DeliveriesHeaderPath { get; init; }
    public IReadOnlyList<string> SearchDirectories { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Strictly read-only offline CSV reader for the Sepidar inventory pilot.
/// 
/// PURPOSE & COMPLIANCE:
/// - Reads extracted empirical CSV samples without requiring live database connectivity, credentials, or SQL Server.
/// - Preserves all employer-accepted temporary assumptions.
/// - Transactional Consistency: Discrete CSV samples are NOT an ACID transaction snapshot. They are explicitly
///   marked as unverified (IsConsistent = false).
/// - Provenance: Records full paths, modification timestamps, row counts, and SHA256 hashes in an InputManifest.
/// - Cardex: Declares MissingMovementData when movement item rows are absent; does not treat it as zero movements or complete cardex.
/// </summary>
public sealed class SepidarCsvPilotReader : ISepidarPilotReader
{
    private readonly SepidarCsvReaderOptions _options;
    private readonly ILogger _logger;

    public SepidarCsvPilotReader(string baseDirectory, ILogger? logger = null)
        : this(new SepidarCsvReaderOptions { SearchDirectories = new[] { baseDirectory } }, logger)
    {
    }

    public SepidarCsvPilotReader(IReadOnlyList<string> searchDirectories, ILogger? logger = null)
        : this(new SepidarCsvReaderOptions { SearchDirectories = searchDirectories }, logger)
    {
    }

    public SepidarCsvPilotReader(SepidarCsvReaderOptions options, ILogger? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task<SepidarPilotCaptureResult> CaptureAsync(
        string warehouseRef = "1",
        string fiscalYearTitle = "1405",
        CancellationToken ct = default)
    {
        var inputManifest = new List<CsvFileEntry>();
        try
        {
            // 1. Locate required CSV files (strict TopDirectoryOnly to avoid silent multi-run blending)
            var fyPath = ResolveFilePath(_options.FiscalYearsPath, "*fiscal_year*.csv");
            if (fyPath == null || !File.Exists(fyPath))
            {
                return Inconsistent($"PILOT_CSV_FILE_MISSING: Fiscal years CSV file not found (searched: '{_options.FiscalYearsPath ?? "*fiscal_year*.csv"}').");
            }

            var stocksPath = ResolveFilePath(_options.StocksPath, "*stock1_verified*.csv", "*stocks*.csv", "*stock*.csv");
            if (stocksPath == null || !File.Exists(stocksPath))
            {
                return Inconsistent($"PILOT_CSV_FILE_MISSING: Stocks/Warehouse CSV file not found.");
            }

            var unitsPath = ResolveFilePath(_options.UnitsPath, "*units*.csv", "*unit*.csv");
            var summaryPath = ResolveFilePath(_options.StockSummaryPath, "*kg_stock_calculation*.csv", "*stock_summary*.csv");
            if (summaryPath == null || !File.Exists(summaryPath))
            {
                return Inconsistent($"PILOT_CSV_FILE_MISSING: Stock summary CSV file not found.");
            }

            // 2. Parse Fiscal Year
            SepidarFiscalYearInfo? matchedFy = null;
            var fyRows = await ReadCsvFileAsync(fyPath, ct);
            if (fyRows.Count < 2)
            {
                return Inconsistent($"PILOT_CSV_INVALID_FORMAT: Fiscal year file '{fyPath}' contains no data rows.");
            }
            inputManifest.Add(CreateManifestEntry("FiscalYears", fyPath, Math.Max(0, fyRows.Count - 1)));

            var fyHeaders = fyRows[0];
            int idxFyId = FindColumn(fyHeaders, "FiscalYearId");
            int idxFyTitle = FindColumn(fyHeaders, "Title");
            int idxFyStart = FindColumn(fyHeaders, "StartDate");
            int idxFyEnd = FindColumn(fyHeaders, "EndDate");

            if (idxFyId < 0 || idxFyTitle < 0 || idxFyStart < 0 || idxFyEnd < 0)
            {
                return Inconsistent($"PILOT_CSV_MISSING_COLUMNS: Fiscal year file is missing required columns (FiscalYearId, Title, StartDate, EndDate).");
            }

            var candidates = new List<SepidarFiscalYearInfo>();
            for (int r = 1; r < fyRows.Count; r++)
            {
                var row = fyRows[r];
                if (row.Count <= Math.Max(idxFyId, Math.Max(idxFyTitle, Math.Max(idxFyStart, idxFyEnd)))) continue;

                if (string.Equals(row[idxFyTitle].Trim(), fiscalYearTitle, StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(row[idxFyId].Trim(), out int fyId)) continue;
                    if (!DateTime.TryParse(row[idxFyStart].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var start))
                    {
                        if (!DateTime.TryParse(row[idxFyStart].Trim(), out start)) continue;
                    }
                    if (!DateTime.TryParse(row[idxFyEnd].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var end))
                    {
                        if (!DateTime.TryParse(row[idxFyEnd].Trim(), out end)) continue;
                    }

                    start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
                    end = DateTime.SpecifyKind(end, DateTimeKind.Utc);
                    var periodEnd = end.TimeOfDay == TimeSpan.Zero ? end.AddDays(1) : end;

                    candidates.Add(new SepidarFiscalYearInfo(fyId, fiscalYearTitle, start, end, periodEnd));
                }
            }

            if (candidates.Count == 0)
            {
                return Inconsistent($"PILOT_FISCAL_YEAR_NOT_FOUND: Fiscal year '{fiscalYearTitle}' was not found in CSV '{fyPath}'.");
            }
            if (candidates.Count > 1)
            {
                return Inconsistent($"PILOT_FISCAL_YEAR_AMBIGUOUS: Multiple fiscal year rows found for '{fiscalYearTitle}'.");
            }

            matchedFy = candidates[0];
            if (matchedFy.StartDate >= matchedFy.PeriodEndExclusive)
            {
                return Inconsistent($"PILOT_FISCAL_YEAR_INVALID_RANGE: StartDate ({matchedFy.StartDate:u}) is not before EndDate ({matchedFy.PeriodEndExclusive:u}).");
            }

            // 3. Verify Warehouse
            var stockRows = await ReadCsvFileAsync(stocksPath, ct);
            inputManifest.Add(CreateManifestEntry("WarehouseStatus", stocksPath, Math.Max(0, stockRows.Count - 1)));

            var stockHeaders = stockRows.Count > 0 ? stockRows[0] : new List<string>();
            int idxStockId = FindColumn(stockHeaders, "StockID", "StockRef", "WarehouseRef", "Id");
            int idxStockActive = FindColumn(stockHeaders, "IsActive", "IsActiveInt");

            bool whFound = false;
            bool whActive = false;
            for (int r = 1; r < stockRows.Count; r++)
            {
                var row = stockRows[r];
                if (idxStockId >= 0 && row.Count > idxStockId && string.Equals(row[idxStockId].Trim(), warehouseRef, StringComparison.OrdinalIgnoreCase))
                {
                    whFound = true;
                    if (idxStockActive >= 0 && row.Count > idxStockActive)
                    {
                        var val = row[idxStockActive].Trim();
                        whActive = string.Equals(val, "True", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(val, "1", StringComparison.OrdinalIgnoreCase) ||
                                   string.IsNullOrEmpty(val); // default true if unpopulated in sample
                    }
                    else
                    {
                        whActive = true;
                    }
                    break;
                }
            }

            if (!whFound || !whActive)
            {
                return Inconsistent($"PILOT_SOURCE_WAREHOUSE_INVALID: WarehouseRef '{warehouseRef}' was not found or is inactive in stocks CSV.");
            }

            // 4. Load Units Map
            var unitsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (unitsPath != null && File.Exists(unitsPath))
            {
                var unitRows = await ReadCsvFileAsync(unitsPath, ct);
                inputManifest.Add(CreateManifestEntry("Units", unitsPath, Math.Max(0, unitRows.Count - 1)));
                if (unitRows.Count > 1)
                {
                    var uHeaders = unitRows[0];
                    int idxUId = FindColumn(uHeaders, "UnitID", "Id");
                    int idxUTitle = FindColumn(uHeaders, "Title", "UnitTitle");
                    if (idxUId >= 0 && idxUTitle >= 0)
                    {
                        for (int r = 1; r < unitRows.Count; r++)
                        {
                            var row = unitRows[r];
                            if (row.Count > Math.Max(idxUId, idxUTitle))
                            {
                                var uId = row[idxUId].Trim();
                                var uTitle = row[idxUTitle].Trim();
                                string unitCode = NormalizeUnitTitle(uTitle);
                                unitsMap[uId] = unitCode;
                            }
                        }
                    }
                }
            }

            // 5. Read Stock Balances (OnHand)
            var capturedAtUtc = DateTime.UtcNow;
            var summaryRows = await ReadCsvFileAsync(summaryPath, ct);
            if (summaryRows.Count < 2)
            {
                return Inconsistent($"PILOT_CSV_INVALID_FORMAT: Stock summary file '{summaryPath}' contains no data rows.");
            }
            inputManifest.Add(CreateManifestEntry("StockBalancesOnHand", summaryPath, Math.Max(0, summaryRows.Count - 1)));

            var sumHeaders = summaryRows[0];
            int idxItemRef = FindColumn(sumHeaders, "ItemRef", "ItemID", "SourceItemId");
            int idxSumStockRef = FindColumn(sumHeaders, "StockRef", "StockID", "WarehouseRef");
            int idxSumUnitRef = FindColumn(sumHeaders, "UnitRef", "UnitID");
            int idxSumQty = FindColumn(sumHeaders, "StoredQuantity", "Quantity", "OnHand", "NetDifference");
            int idxSumUnitTitle = FindColumn(sumHeaders, "UnitTitle", "UnitCode");

            if (idxItemRef < 0 || idxSumQty < 0)
            {
                return Inconsistent($"PILOT_CSV_MISSING_COLUMNS: Stock summary file is missing required columns (ItemRef, Quantity/StoredQuantity).");
            }

            var stocks = new List<SepidarStockRecord>();
            for (int r = 1; r < summaryRows.Count; r++)
            {
                var row = summaryRows[r];
                if (row.Count <= Math.Max(idxItemRef, idxSumQty)) continue;

                // Check warehouse ref if present
                if (idxSumStockRef >= 0 && row.Count > idxSumStockRef)
                {
                    var rowWh = row[idxSumStockRef].Trim();
                    if (!string.IsNullOrEmpty(rowWh) && !string.Equals(rowWh, warehouseRef, StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Skip items from other warehouses
                    }
                }

                var itemRef = row[idxItemRef].Trim();
                if (string.IsNullOrEmpty(itemRef)) continue;

                if (!decimal.TryParse(row[idxSumQty].Trim(), CultureInfo.InvariantCulture, out decimal onHand))
                {
                    if (!decimal.TryParse(row[idxSumQty].Trim(), out onHand)) continue;
                }

                // Resolve unit code
                string unitCode = "UNKNOWN_UNIT";
                if (idxSumUnitTitle >= 0 && row.Count > idxSumUnitTitle && !string.IsNullOrWhiteSpace(row[idxSumUnitTitle]))
                {
                    unitCode = NormalizeUnitTitle(row[idxSumUnitTitle].Trim());
                }
                else if (idxSumUnitRef >= 0 && row.Count > idxSumUnitRef)
                {
                    var uRef = row[idxSumUnitRef].Trim();
                    if (unitsMap.TryGetValue(uRef, out var mappedUnit))
                    {
                        unitCode = mappedUnit;
                    }
                }

                stocks.Add(new SepidarStockRecord(
                    ItemRef: itemRef,
                    WarehouseRef: warehouseRef,
                    OnHand: onHand,
                    Reservable: null,
                    Reserved: null,
                    Incoming: 0m,
                    UnitCode: unitCode,
                    SnapshotTimeUtc: capturedAtUtc));
            }

            // 6. Read Movements
            var movements = new List<SepidarMovementRecord>();
            bool missingMovementData = true;

            var movementsPath = ResolveFilePath(_options.MovementsPath, "*movement*.csv");
            if (movementsPath != null && File.Exists(movementsPath))
            {
                var movRows = await ReadCsvFileAsync(movementsPath, ct);
                if (movRows.Count > 1)
                {
                    var mHeaders = movRows[0];
                    int idxVoucherRef = FindColumn(mHeaders, "VoucherRef", "VoucherId", "Id");
                    int idxVoucherNum = FindColumn(mHeaders, "VoucherNumber", "DocumentNumber", "Number");
                    int idxMItemRef = FindColumn(mHeaders, "ItemRef", "ItemID");
                    int idxMWhRef = FindColumn(mHeaders, "WarehouseRef", "StockRef");
                    int idxMType = FindColumn(mHeaders, "MovementType", "Type");
                    int idxMQty = FindColumn(mHeaders, "Quantity");
                    int idxMUnit = FindColumn(mHeaders, "UnitCode", "UnitTitle");
                    int idxMDate = FindColumn(mHeaders, "TransactionDateUtc", "DocumentDate", "Date");
                    int idxMPosting = FindColumn(mHeaders, "PostingStatus");
                    int idxMCancel = FindColumn(mHeaders, "IsCancelled");
                    int idxMRef = FindColumn(mHeaders, "ReferenceDocument", "Description");
                    int idxMReturn = FindColumn(mHeaders, "IsReturn");

                    // Check if file actually contains item lines and quantities
                    if (idxMItemRef >= 0 && idxMQty >= 0)
                    {
                        missingMovementData = false;
                        inputManifest.Add(CreateManifestEntry("MovementsItems", movementsPath, movRows.Count - 1));

                        for (int r = 1; r < movRows.Count; r++)
                        {
                            var row = movRows[r];
                            if (row.Count <= Math.Max(idxVoucherRef, Math.Max(idxMItemRef, idxMQty))) continue;

                            if (idxMWhRef >= 0 && row.Count > idxMWhRef)
                            {
                                var rWh = row[idxMWhRef].Trim();
                                if (!string.IsNullOrEmpty(rWh) && !string.Equals(rWh, warehouseRef, StringComparison.OrdinalIgnoreCase))
                                    continue;
                            }

                            var vRef = idxVoucherRef >= 0 ? row[idxVoucherRef].Trim() : $"MOV-{r}";
                            var vNum = idxVoucherNum >= 0 && row.Count > idxVoucherNum ? row[idxVoucherNum].Trim() : vRef;
                            var mItem = row[idxMItemRef].Trim();
                            if (!decimal.TryParse(row[idxMQty].Trim(), CultureInfo.InvariantCulture, out decimal qty))
                            {
                                if (!decimal.TryParse(row[idxMQty].Trim(), out qty)) continue;
                            }

                            if (qty < 0)
                            {
                                return Inconsistent($"PILOT_NEGATIVE_QUANTITY: Movement {vRef} has negative quantity {qty}.");
                            }

                            DateTime txDate = capturedAtUtc;
                            if (idxMDate >= 0 && row.Count > idxMDate)
                            {
                                if (DateTime.TryParse(row[idxMDate].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var d) ||
                                    DateTime.TryParse(row[idxMDate].Trim(), out d))
                                {
                                    txDate = DateTime.SpecifyKind(d, DateTimeKind.Utc);
                                }
                            }

                            if (txDate < matchedFy.StartDate || txDate >= matchedFy.PeriodEndExclusive)
                            {
                                return Inconsistent($"PILOT_MOVEMENT_DATE_OUT_OF_BOUNDS: Movement date {txDate:u} is outside fiscal year [{matchedFy.StartDate:u}, {matchedFy.PeriodEndExclusive:u}).");
                            }

                            // Determine movement type
                            InventoryMovementType mType = InventoryMovementType.Unknown;
                            if (idxMType >= 0 && row.Count > idxMType)
                            {
                                var rawTypeStr = row[idxMType].Trim();
                                bool isReturn = false;
                                if (idxMReturn >= 0 && row.Count > idxMReturn)
                                {
                                    bool.TryParse(row[idxMReturn].Trim(), out isReturn);
                                }

                                if (int.TryParse(rawTypeStr, out int rawType))
                                {
                                    mType = MapMovementType(rawType, isReturn, vRef);
                                }
                                else if (Enum.TryParse<InventoryMovementType>(rawTypeStr, true, out var parsedType))
                                {
                                    mType = parsedType;
                                }
                            }

                            string uCode = "KG";
                            if (idxMUnit >= 0 && row.Count > idxMUnit && !string.IsNullOrWhiteSpace(row[idxMUnit]))
                            {
                                uCode = NormalizeUnitTitle(row[idxMUnit].Trim());
                            }

                            int postingStatus = 4;
                            if (idxMPosting >= 0 && row.Count > idxMPosting && int.TryParse(row[idxMPosting].Trim(), out int ps))
                            {
                                postingStatus = ps;
                            }

                            bool isCancelled = false;
                            if (idxMCancel >= 0 && row.Count > idxMCancel)
                            {
                                bool.TryParse(row[idxMCancel].Trim(), out isCancelled);
                            }

                            string? refDoc = idxMRef >= 0 && row.Count > idxMRef ? row[idxMRef].Trim() : null;

                            movements.Add(new SepidarMovementRecord(
                                VoucherRef: vRef,
                                VoucherNumber: vNum,
                                ItemRef: mItem,
                                WarehouseRef: warehouseRef,
                                MovementType: mType,
                                Quantity: qty,
                                UnitCode: uCode,
                                TransactionDateUtc: txDate,
                                PostingStatus: postingStatus,
                                IsCancelled: isCancelled,
                                ReferenceDocument: refDoc));
                        }
                    }
                }
            }

            // Also manifest document number samples if present, and document that item lines cannot be deduced from them
            var receiptsHeaderPath = ResolveFilePath(_options.ReceiptsHeaderPath, "*receipt*document_number*.csv", "*receipt*.csv");
            if (receiptsHeaderPath != null && File.Exists(receiptsHeaderPath) && !string.Equals(receiptsHeaderPath, movementsPath, StringComparison.OrdinalIgnoreCase))
            {
                var rRows = await ReadCsvFileAsync(receiptsHeaderPath, ct);
                inputManifest.Add(CreateManifestEntry("ReceiptsDocumentHeaders", receiptsHeaderPath, Math.Max(0, rRows.Count - 1)));
            }

            var deliveriesHeaderPath = ResolveFilePath(_options.DeliveriesHeaderPath, "*delivery*document_number*.csv", "*delivery*.csv");
            if (deliveriesHeaderPath != null && File.Exists(deliveriesHeaderPath) && !string.Equals(deliveriesHeaderPath, movementsPath, StringComparison.OrdinalIgnoreCase))
            {
                var dRows = await ReadCsvFileAsync(deliveriesHeaderPath, ct);
                inputManifest.Add(CreateManifestEntry("DeliveriesDocumentHeaders", deliveriesHeaderPath, Math.Max(0, dRows.Count - 1)));
            }

            var capture = new PilotSourceCapture(
                FiscalYearId: matchedFy.FiscalYearId,
                CapturedAtUtc: capturedAtUtc,
                Stocks: stocks,
                Movements: movements);

            // COMPLIANCE REQUIREMENT:
            // For discrete sample CSV files, IsConsistent = true and "Snapshot تأییدشده" are FALSE.
            // Point-in-time transactional consistency MUST be marked UNVERIFIED (IsConsistent = false).
            // A local diagnostic report is still produced, but remains strictly unexecutable / cannot be applied.
            return new SepidarPilotCaptureResult(
                IsConsistent: false,
                FailureReason: "PILOT_TRANSACTIONAL_SNAPSHOT_UNVERIFIED: نمونه‌های مجزای CSV حاصل برداشت هم‌زمان در یک تراکنش Snapshot مشترک نیستند؛ سازگاری تراکنشی تأییدنشده است.",
                FiscalYear: matchedFy,
                Capture: capture,
                CapturedAtUtc: capturedAtUtc,
                StockCount: stocks.Count,
                MovementCount: movements.Count,
                MissingMovementData: missingMovementData,
                InputManifest: inputManifest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing CSV pilot capture: {Message}", ex.Message);
            return Inconsistent($"PILOT_CSV_READ_ERROR: {ex.Message}");
        }
    }

    private static InventoryMovementType MapMovementType(int rawType, bool isReturn, string voucherRef)
    {
        // Follows the identical employer-accepted temporary assumptions:
        // Receipt (REC-): Type=1, IsReturn=0 -> Receipt (2, Purchase receipt)
        // Receipt (REC-): Type=1, IsReturn=1 -> ReturnToVendor (6, Purchase return)
        // Delivery (DEL-): Type=2, IsReturn=0 -> Consumption (1, Production issue)
        // Delivery (DEL-): Type=2, IsReturn=1 -> Adjustment (4, Return from production)
        // All others -> Unknown (0) (Unsupported)
        if (voucherRef.StartsWith("REC", StringComparison.OrdinalIgnoreCase))
        {
            if (rawType == 1 && !isReturn) return InventoryMovementType.Receipt;
            if (rawType == 1 && isReturn) return InventoryMovementType.Return;
            return InventoryMovementType.Unknown;
        }

        if (voucherRef.StartsWith("DEL", StringComparison.OrdinalIgnoreCase))
        {
            if (rawType == 2 && !isReturn) return InventoryMovementType.Consumption;
            if (rawType == 2 && isReturn) return InventoryMovementType.Adjustment;
            return InventoryMovementType.Unknown;
        }

        // Direct standard code
        if (Enum.IsDefined(typeof(InventoryMovementType), rawType))
        {
            return (InventoryMovementType)rawType;
        }

        return InventoryMovementType.Unknown;
    }

    private static string NormalizeUnitTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "UNKNOWN_UNIT";
        var t = title.Trim();
        if (t.Contains("كیلوگرم", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("كيلوگرم", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("کیلوگرم", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t, "KG", StringComparison.OrdinalIgnoreCase))
        {
            return "KG";
        }
        if (string.Equals(t, "عدد", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t, "COUNT", StringComparison.OrdinalIgnoreCase))
        {
            return "COUNT";
        }
        return t;
    }

    private static CsvFileEntry CreateManifestEntry(string role, string path, int rowCount)
    {
        var fullPath = Path.GetFullPath(path);
        var fi = new FileInfo(fullPath);
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(fullPath);
        var hash = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        return new CsvFileEntry(role, fullPath, fi.LastWriteTimeUtc, hash, rowCount);
    }

    private string? ResolveFilePath(string? explicitPath, params string[] patterns)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            if (!File.Exists(explicitPath))
                throw new FileNotFoundException("Explicit CSV input does not exist; fallback is prohibited.", explicitPath);
            return Path.GetFullPath(explicitPath);
        }

        foreach (var pattern in patterns)
        {
            var matches = _options.SearchDirectories
                .Where(Directory.Exists)
                .SelectMany(dir => Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly))
                .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (matches.Length > 1)
                throw new InvalidOperationException("Ambiguous CSV input: specify an exact file path.");
            if (matches.Length == 1) return matches[0];
        }
        return null;
    }
    private static int FindColumn(List<string> headers, params string[] candidateNames)
    {
        for (int i = 0; i < headers.Count; i++)
        {
            var h = headers[i].Trim().Trim('"');
            foreach (var name in candidateNames)
            {
                if (string.Equals(h, name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }
        return -1;
    }

    private static async Task<List<List<string>>> ReadCsvFileAsync(string filePath, CancellationToken ct)
    {
        var result = new List<List<string>>();
        using var stream = File.OpenRead(filePath);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            result.Add(ParseCsvLine(line));
        }

        return result;
    }

    public static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(line)) return result;

        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString().Trim('"'));
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        result.Add(sb.ToString().Trim('"'));
        return result;
    }

    private static SepidarPilotCaptureResult Inconsistent(string reason) =>
        new(IsConsistent: false,
            FailureReason: reason,
            FiscalYear: null,
            Capture: null,
            CapturedAtUtc: null,
            StockCount: 0,
            MovementCount: 0);
}
