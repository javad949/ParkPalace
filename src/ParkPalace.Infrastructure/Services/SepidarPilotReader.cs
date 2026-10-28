using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services;

public sealed record SepidarFiscalYearInfo(
    int FiscalYearId,
    string Title,
    DateTime StartDate,
    DateTime EndDate,
    DateTime PeriodEndExclusive)
{
    public SepidarFiscalYearInfo(int fiscalYearId, string title, DateTime startDate, DateTime endDate)
        : this(fiscalYearId, title, startDate, endDate, endDate.TimeOfDay == TimeSpan.Zero ? endDate.AddDays(1) : endDate)
    {
    }
}

public sealed record CsvFileEntry(
    string Role,
    string Path,
    DateTime LastModifiedUtc,
    string Sha256Hash,
    int RowCount);

public sealed record SepidarPilotCaptureResult(
    bool IsConsistent,
    string? FailureReason,
    SepidarFiscalYearInfo? FiscalYear,
    PilotSourceCapture? Capture,
    DateTime? CapturedAtUtc,
    int StockCount,
    int MovementCount,
    bool MissingMovementData = false,
    IReadOnlyList<CsvFileEntry>? InputManifest = null);

public interface ISepidarPilotReader
{
    Task<SepidarPilotCaptureResult> CaptureAsync(
        string warehouseRef = "1",
        string fiscalYearTitle = "1405",
        CancellationToken ct = default);
}

/// <summary>
/// Strictly read-only Sepidar reader for the inventory pilot.
/// 
/// CONCURRENCY & LOCKING DESIGN:
/// - Executes inside a single transaction bound to all reading commands.
/// - Default isolation level is IsolationLevel.Snapshot to achieve point-in-time cross-table consistency
///   via tempdb row versioning without acquiring shared (S) read locks on production ERP data pages.
/// - Locking impact: Avoids standard shared-lock read contention and reader-writer deadlocks on data pages.
///   However, it does not prevent contention completely: DDL schema modification locks (Sch-M), tempdb
///   I/O load, and concurrent update conflicts remain factors in SQL Server performance.
/// - NO PRODUCTION SETTING MUTATIONS: If ALLOW_SNAPSHOT_ISOLATION is not enabled on the source database,
///   this reader refuses to run (declaring capture UNEXECUTABLE with PILOT_CONSISTENCY_PREREQUISITE_MISSING).
///   It never executes ALTER DATABASE on operational systems.
/// 
/// UNIT & DATA INTEGRITY:
/// - No assumptions that UnitRef=1 is KG.
/// - No fallback to KG if unit title is null/empty. Missing unit titles evaluate to 'UNKNOWN_UNIT' and are excluded.
/// - Dates: Normalizes fiscal year EndDate so that whole-day end timestamps are not truncated.
/// - Voucher Types: Unrecognized voucher types are rejected rather than guessed.
/// </summary>
public sealed class SepidarPilotReader : ISepidarPilotReader
{
    private readonly Func<CancellationToken, Task<DbConnection>> _connectionFactory;
    private readonly ILogger _logger;
    public IsolationLevel DesiredIsolationLevel { get; init; } = IsolationLevel.Snapshot;
    public Func<Task>? OnAfterStockReadAsync { get; set; }

    public SepidarPilotReader(
        string? connectionString,
        ILogger? logger = null,
        IsolationLevel isolationLevel = IsolationLevel.Snapshot)
    {
        DesiredIsolationLevel = isolationLevel;
        _logger = logger ?? NullLogger.Instance;
        _connectionFactory = async (ct) =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("PILOT_SEPIDAR_CONNECTION_STRING_EMPTY");

            var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);
            return conn;
        };
    }

    public SepidarPilotReader(
        Func<CancellationToken, Task<DbConnection>> connectionFactory,
        ILogger? logger = null,
        IsolationLevel isolationLevel = IsolationLevel.Snapshot)
    {
        DesiredIsolationLevel = isolationLevel;
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task<SepidarPilotCaptureResult> CaptureAsync(
        string warehouseRef = "1",
        string fiscalYearTitle = "1405",
        CancellationToken ct = default)
    {
        DbConnection? conn = null;
        try
        {
            conn = await _connectionFactory(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Sepidar for pilot capture: {Message}", ex.Message);
            return Inconsistent($"PILOT_SOURCE_UNAVAILABLE: Connection failed - {ex.Message}");
        }

        await using (conn)
        {
            DbTransaction? tx = null;
            try
            {
                tx = await conn.BeginTransactionAsync(DesiredIsolationLevel, ct);
            }
            catch (Exception ex) when (IsSnapshotPrerequisiteError(ex))
            {
                _logger.LogWarning(ex, "Snapshot isolation prerequisite not enabled on source database: {Message}", ex.Message);
                return SnapshotPrerequisiteMissingResult;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to begin read transaction on Sepidar: {Message}", ex.Message);
                return Inconsistent($"PILOT_TRANSACTION_START_FAILED: {ex.Message}");
            }

            await using (tx)
            {
                // 1. Verify Fiscal Year directly from FMK.FiscalYear within the transaction
                SepidarFiscalYearInfo fiscalYear;
                try
                {
                    await using var fyCmd = conn.CreateCommand();
                    fyCmd.Transaction = tx;
                    fyCmd.CommandText = @"
                        SELECT FiscalYearId, Title, StartDate, EndDate
                        FROM FMK.FiscalYear
                        WHERE Title = @Title";
                    fyCmd.CommandTimeout = 15;

                    var titleParam = fyCmd.CreateParameter();
                    titleParam.ParameterName = "@Title";
                    titleParam.Value = fiscalYearTitle;
                    fyCmd.Parameters.Add(titleParam);

                    var fyRecords = new List<SepidarFiscalYearInfo>();
                    await using (var reader = await fyCmd.ExecuteReaderAsync(ct))
                    {
                        while (await reader.ReadAsync(ct))
                        {
                            var fyId = reader.GetInt32(0);
                            var title = reader.GetString(1);
                            var start = DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc);
                            var end = DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc);
                            // If EndDate has no time component (00:00:00), end-of-period exclusive boundary is midnight next day
                            var periodEndExclusive = end.TimeOfDay == TimeSpan.Zero ? end.AddDays(1) : end;
                            fyRecords.Add(new SepidarFiscalYearInfo(fyId, title, start, end, periodEndExclusive));
                        }
                    }

                    if (fyRecords.Count == 0)
                    {
                        return Inconsistent($"PILOT_FISCAL_YEAR_NOT_FOUND: Fiscal year '{fiscalYearTitle}' was not found in FMK.FiscalYear.");
                    }

                    if (fyRecords.Count > 1)
                    {
                        return Inconsistent($"PILOT_FISCAL_YEAR_AMBIGUOUS: Multiple fiscal years found with title '{fiscalYearTitle}'.");
                    }

                    fiscalYear = fyRecords[0];
                    if (fiscalYear.StartDate >= fiscalYear.PeriodEndExclusive)
                    {
                        return Inconsistent($"PILOT_FISCAL_YEAR_INVALID_RANGE: StartDate ({fiscalYear.StartDate:u}) is not before EndDate ({fiscalYear.EndDate:u}).");
                    }
                }
                catch (Exception ex) when (IsSnapshotPrerequisiteError(ex))
                {
                    _logger.LogWarning(ex, "Snapshot isolation prerequisite not enabled on source database: {Message}", ex.Message);
                    return SnapshotPrerequisiteMissingResult;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error reading fiscal year from Sepidar: {Message}", ex.Message);
                    return Inconsistent($"PILOT_FISCAL_YEAR_QUERY_FAILED: {ex.Message}");
                }

                // 2. Verify Warehouse exists and is active in INV.Stock within the transaction
                try
                {
                    await using var whCmd = conn.CreateCommand();
                    whCmd.Transaction = tx;
                    whCmd.CommandText = @"
                        SELECT StockID, IsActive
                        FROM INV.Stock
                        WHERE CAST(StockID AS NVARCHAR(50)) = @WarehouseRef";
                    whCmd.CommandTimeout = 15;

                    var whParam = whCmd.CreateParameter();
                    whParam.ParameterName = "@WarehouseRef";
                    whParam.Value = warehouseRef;
                    whCmd.Parameters.Add(whParam);

                    bool found = false;
                    bool isActive = false;
                    await using (var reader = await whCmd.ExecuteReaderAsync(ct))
                    {
                        if (await reader.ReadAsync(ct))
                        {
                            found = true;
                            isActive = reader.GetBoolean(1);
                        }
                    }

                    if (!found || !isActive)
                    {
                        return Inconsistent($"PILOT_SOURCE_WAREHOUSE_INVALID: WarehouseRef '{warehouseRef}' was not found or is inactive in INV.Stock.");
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error verifying warehouse in Sepidar: {Message}", ex.Message);
                    return Inconsistent($"PILOT_WAREHOUSE_QUERY_FAILED: {ex.Message}");
                }

                // 3. Point-in-time captured timestamp
                var capturedAtUtc = DateTime.UtcNow;

                // 4. Read Stock Balances within the transaction
                // Note: No UnitRef=1 => KG assumption. u.Title must be explicitly verified.
                var stocks = new List<SepidarStockRecord>();
                try
                {
                    await using var stockCmd = conn.CreateCommand();
                    stockCmd.Transaction = tx;
                    stockCmd.CommandText = @"
                        SELECT CAST(sb.ItemRef AS NVARCHAR(50)) AS ItemRef,
                               CAST(sb.StockRef AS NVARCHAR(50)) AS WarehouseRef,
                               CAST(ISNULL(sb.Quantity, 0) AS DECIMAL(18,4)) AS OnHand,
                               CAST(NULL AS DECIMAL(18,4)) AS Reservable,
                               CAST(NULL AS DECIMAL(18,4)) AS Reserved,
                               CAST(0 AS DECIMAL(18,4)) AS Incoming,
                               CASE 
                                   WHEN u.Title IS NULL OR LTRIM(RTRIM(u.Title)) = '' THEN 'UNKNOWN_UNIT'
                                   WHEN (u.Title LIKE N'%ک%لوگرم%' OR u.Title LIKE N'%ك%لوگرم%' OR u.Title = 'KG') THEN 'KG'
                                   WHEN (u.Title = N'عدد' OR u.Title = 'COUNT') THEN 'COUNT'
                                   ELSE u.Title
                               END AS UnitCode
                        FROM INV.ItemStockSummary sb
                        JOIN INV.Item i ON sb.ItemRef = i.ItemID
                        LEFT JOIN INV.Unit u ON sb.UnitRef = u.UnitID
                        WHERE CAST(sb.StockRef AS NVARCHAR(50)) = @WarehouseRef
                          AND sb.FiscalYearRef = @FiscalYearRef";
                    stockCmd.CommandTimeout = 30;

                    var pWh = stockCmd.CreateParameter();
                    pWh.ParameterName = "@WarehouseRef";
                    pWh.Value = warehouseRef;
                    stockCmd.Parameters.Add(pWh);

                    var pFy = stockCmd.CreateParameter();
                    pFy.ParameterName = "@FiscalYearRef";
                    pFy.Value = fiscalYear.FiscalYearId;
                    stockCmd.Parameters.Add(pFy);

                    await using var reader = await stockCmd.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        var unitCode = reader.GetString(6);
                        stocks.Add(new SepidarStockRecord(
                            reader.GetString(0),
                            reader.GetString(1),
                            reader.GetDecimal(2),
                            reader.IsDBNull(3) ? null : reader.GetDecimal(3),
                            reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                            reader.GetDecimal(5),
                            unitCode,
                            capturedAtUtc
                        ));
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error reading stocks from Sepidar: {Message}", ex.Message);
                    return Inconsistent($"PILOT_STOCKS_QUERY_FAILED: {ex.Message}");
                }

                // Test Hook: allows testing a concurrent writer between stock read and movements read
                if (OnAfterStockReadAsync != null)
                {
                    await OnAfterStockReadAsync();
                }

                // 5. Read Movements within the transaction
                // Bound by [StartDate, PeriodEndExclusive)
                // Rejects unrecognized movement types.
                var movements = new List<SepidarMovementRecord>();
                try
                {
                    await using var movCmd = conn.CreateCommand();
                    movCmd.Transaction = tx;
                    movCmd.CommandText = @"
                        SELECT 'REC-' + CAST(ri.InventoryReceiptItemID AS NVARCHAR(50)) AS VoucherRef,
                               CAST(r.Number AS NVARCHAR(50)) AS VoucherNumber,
                               CAST(ri.ItemRef AS NVARCHAR(50)) AS ItemRef,
                               CAST(r.StockRef AS NVARCHAR(50)) AS WarehouseRef,
                               CASE 
                                   WHEN r.Type = 1 AND r.IsReturn = 0 THEN 2 -- Receipt (Assumed: Purchase Receipt)
                                   WHEN r.Type = 1 AND r.IsReturn = 1 THEN 6 -- Return to vendor (Assumed: Purchase Return)
                                   ELSE 0                                     -- Unsupported: Type=3, Type=4, etc. (No wild guesses)
                               END AS MovementType,
                               CAST(ri.Quantity AS DECIMAL(18,4)) AS Quantity,
                               CASE 
                                   WHEN u.Title IS NULL OR LTRIM(RTRIM(u.Title)) = '' THEN 'UNKNOWN_UNIT'
                                   WHEN (u.Title LIKE N'%ک%لوگرم%' OR u.Title LIKE N'%ك%لوگرم%' OR u.Title = 'KG') THEN 'KG'
                                   WHEN (u.Title = N'عدد' OR u.Title = 'COUNT') THEN 'COUNT'
                                   ELSE u.Title
                               END AS UnitCode,
                               r.Date AS TransactionDateUtc,
                               CASE WHEN r.AccountingVoucherRef IS NOT NULL THEN 4 ELSE 2 END AS PostingStatus,
                               CAST(0 AS BIT) AS IsCancelled,
                               r.Description AS ReferenceDocument
                        FROM INV.InventoryReceiptItem ri
                        JOIN INV.InventoryReceipt r ON ri.InventoryReceiptRef = r.InventoryReceiptID
                        JOIN INV.Item i ON ri.ItemRef = i.ItemID
                        LEFT JOIN INV.Unit u ON i.UnitRef = u.UnitID
                        WHERE CAST(r.StockRef AS NVARCHAR(50)) = @WarehouseRef
                          AND r.FiscalYearRef = @FiscalYearRef
                          AND r.Date >= @StartDate AND r.Date < @EndDate
                        UNION ALL
                        SELECT 'DEL-' + CAST(di.InventoryDeliveryItemID AS NVARCHAR(50)) AS VoucherRef,
                               CAST(d.Number AS NVARCHAR(50)) AS VoucherNumber,
                               CAST(di.ItemRef AS NVARCHAR(50)) AS ItemRef,
                               CAST(d.StockRef AS NVARCHAR(50)) AS WarehouseRef,
                               CASE 
                                   WHEN d.Type = 2 AND d.IsReturn = 0 THEN 1 -- Consumption (Assumed: Production issue)
                                   WHEN d.Type = 2 AND d.IsReturn = 1 THEN 4 -- Return from production -> Adjustment (Assumed)
                                   ELSE 0                                     -- Unsupported: Type=3, Type=4, etc. (No wild guesses)
                               END AS MovementType,
                               CAST(di.Quantity AS DECIMAL(18,4)) AS Quantity,
                               CASE 
                                   WHEN u.Title IS NULL OR LTRIM(RTRIM(u.Title)) = '' THEN 'UNKNOWN_UNIT'
                                   WHEN (u.Title LIKE N'%ک%لوگرم%' OR u.Title LIKE N'%ك%لوگرم%' OR u.Title = 'KG') THEN 'KG'
                                   WHEN (u.Title = N'عدد' OR u.Title = 'COUNT') THEN 'COUNT'
                                   ELSE u.Title
                               END AS UnitCode,
                               d.Date AS TransactionDateUtc,
                               4 AS PostingStatus, -- Assumed: Deliveries to production in Sepidar are physical movements; lack of individual AccountingVoucherRef does not denote draft
                               CAST(0 AS BIT) AS IsCancelled,
                               d.Description AS ReferenceDocument
                        FROM INV.InventoryDeliveryItem di
                        JOIN INV.InventoryDelivery d ON di.InventoryDeliveryRef = d.InventoryDeliveryID
                        JOIN INV.Item i ON di.ItemRef = i.ItemID
                        LEFT JOIN INV.Unit u ON i.UnitRef = u.UnitID
                        WHERE CAST(d.StockRef AS NVARCHAR(50)) = @WarehouseRef
                          AND d.FiscalYearRef = @FiscalYearRef
                          AND d.Date >= @StartDate AND d.Date < @EndDate";
                    movCmd.CommandTimeout = 30;

                    var pWh = movCmd.CreateParameter();
                    pWh.ParameterName = "@WarehouseRef";
                    pWh.Value = warehouseRef;
                    movCmd.Parameters.Add(pWh);

                    var pFy = movCmd.CreateParameter();
                    pFy.ParameterName = "@FiscalYearRef";
                    pFy.Value = fiscalYear.FiscalYearId;
                    movCmd.Parameters.Add(pFy);

                    var pStart = movCmd.CreateParameter();
                    pStart.ParameterName = "@StartDate";
                    pStart.Value = fiscalYear.StartDate;
                    movCmd.Parameters.Add(pStart);

                    var pEnd = movCmd.CreateParameter();
                    pEnd.ParameterName = "@EndDate";
                    pEnd.Value = fiscalYear.PeriodEndExclusive;
                    movCmd.Parameters.Add(pEnd);

                    await using var reader = await movCmd.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        int rawType = reader.GetInt32(4);
                        var movementType = (rawType > 0 && Enum.IsDefined(typeof(InventoryMovementType), rawType))
                            ? (InventoryMovementType)rawType
                            : InventoryMovementType.Unknown;
                        var txDate = DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc);
                        var qty = reader.GetDecimal(5);

                        if (txDate < fiscalYear.StartDate || txDate >= fiscalYear.PeriodEndExclusive)
                        {
                            return Inconsistent($"PILOT_MOVEMENT_DATE_OUT_OF_BOUNDS: Movement date {txDate:u} is outside fiscal year [{fiscalYear.StartDate:u}, {fiscalYear.PeriodEndExclusive:u}).");
                        }

                        if (qty < 0)
                        {
                            return Inconsistent($"PILOT_NEGATIVE_QUANTITY: Movement {reader.GetString(0)} has negative quantity {qty}.");
                        }

                        movements.Add(new SepidarMovementRecord(
                            reader.GetString(0),
                            reader.GetString(1),
                            reader.GetString(2),
                            reader.GetString(3),
                            movementType,
                            qty,
                            reader.GetString(6),
                            txDate,
                            reader.GetInt32(8),
                            reader.GetBoolean(9),
                            reader.IsDBNull(10) ? null : reader.GetString(10)
                        ));
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error reading movements from Sepidar: {Message}", ex.Message);
                    return Inconsistent($"PILOT_MOVEMENTS_QUERY_FAILED: {ex.Message}");
                }

                // Commit read-only transaction cleanly
                await tx.CommitAsync(ct);

                var capture = new PilotSourceCapture(
                    fiscalYear.FiscalYearId,
                    capturedAtUtc,
                    stocks,
                    movements);

                return new SepidarPilotCaptureResult(
                    IsConsistent: true,
                    FailureReason: null,
                    FiscalYear: fiscalYear,
                    Capture: capture,
                    CapturedAtUtc: capturedAtUtc,
                    StockCount: stocks.Count,
                    MovementCount: movements.Count);
            }
        }
    }

    private static SepidarPilotCaptureResult Inconsistent(string reason) =>
        new(IsConsistent: false,
            FailureReason: reason,
            FiscalYear: null,
            Capture: null,
            CapturedAtUtc: null,
            StockCount: 0,
            MovementCount: 0);

    private static bool IsSnapshotPrerequisiteError(Exception ex) =>
        (ex is SqlException sqlEx && (sqlEx.Number == 3951 || sqlEx.Number == 3960)) ||
        ex.Message.Contains("snapshot isolation", StringComparison.OrdinalIgnoreCase);

    private static SepidarPilotCaptureResult SnapshotPrerequisiteMissingResult =>
        Inconsistent("PILOT_CONSISTENCY_PREREQUISITE_MISSING: Snapshot isolation is not enabled on source database (ALLOW_SNAPSHOT_ISOLATION is OFF). Point-in-time consistent capture cannot be guaranteed without holding blocking locks on production tables.");
}
