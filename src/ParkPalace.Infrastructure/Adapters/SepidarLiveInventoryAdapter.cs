using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Adapters;

public class SepidarLiveInventoryAdapter : ISepidarInventoryAdapter
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SepidarLiveInventoryAdapter> _logger;
    private readonly string? _connectionString;

    public SepidarLiveInventoryAdapter(IConfiguration configuration, ILogger<SepidarLiveInventoryAdapter> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _connectionString = _configuration.GetConnectionString("SepidarDatabase");
    }

    public bool IsLiveConnected => !string.IsNullOrWhiteSpace(_connectionString);
    public string AdapterName => "SepidarLiveInventoryAdapter (Production Direct SQL Reader)";

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("Sepidar connection string 'SepidarDatabase' is not configured. Live sync cannot proceed.");
        }
    }

    public async Task<List<SepidarWarehouseRecord>> GetWarehousesAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var list = new List<SepidarWarehouseRecord>();

        const string sql = @"
            SELECT CAST(StockID AS NVARCHAR(50)) AS WarehouseRef, 
                   CAST(Code AS NVARCHAR(50)) AS Code, 
                   Title, 
                   CAST(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END AS BIT) AS IsActive
            FROM INV.Stock
            WHERE IsActive = 1";

        try
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
            await conn.OpenAsync(cancellationToken);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(new SepidarWarehouseRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetBoolean(3)
                ));
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch warehouses from Sepidar live database: {Message}", ex.Message);
            throw new InvalidOperationException($"خطا در برقراری ارتباط با پایگاه داده سپیدار جهت دریافت لیست انبارها: {ex.Message}", ex);
        }
    }

    public async Task<List<SepidarItemRecord>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var list = new List<SepidarItemRecord>();

        const string sql = @"
            SELECT CAST(i.ItemID AS NVARCHAR(50)) AS ItemRef, 
                   i.Code, 
                   i.Title, 
                   CASE 
                       WHEN i.UnitRef = 1 OR u.Title LIKE N'%ک%لوگرم%' OR u.Title LIKE N'%ك%لوگرم%' THEN 'KG'
                       WHEN i.UnitRef = 2 OR u.Title = N'عدد' THEN 'COUNT'
                       ELSE ISNULL(u.Title, 'KG')
                   END AS UnitCode, 
                   CASE 
                       WHEN i.UnitRef = 1 OR u.Title LIKE N'%ک%لوگرم%' OR u.Title LIKE N'%ك%لوگرم%' THEN N'کیلوگرم'
                       WHEN i.UnitRef = 2 OR u.Title = N'عدد' THEN N'عدد'
                       ELSE ISNULL(u.Title, N'کیلوگرم')
                   END AS UnitTitle,
                   CAST(CASE WHEN i.IsActive = 1 THEN 1 ELSE 0 END AS BIT) AS IsActive
            FROM INV.Item i
            LEFT JOIN INV.Unit u ON i.UnitRef = u.UnitID";

        try
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
            await conn.OpenAsync(cancellationToken);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(new SepidarItemRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetBoolean(5)
                ));
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch items from Sepidar live database: {Message}", ex.Message);
            throw new InvalidOperationException($"خطا در برقراری ارتباط با پایگاه داده سپیدار جهت دریافت اقلام: {ex.Message}", ex);
        }
    }

    public async Task<int> ResolveFiscalYearIdAsync(SqlConnection conn, CancellationToken cancellationToken = default)
    {
        // 1. Check if explicitly configured in appsettings (by Title or Id)
        var configuredFy = _configuration["Sepidar:FiscalYear"];
        if (!string.IsNullOrWhiteSpace(configuredFy))
        {
            if (int.TryParse(configuredFy, out int fyId))
            {
                using var cmdId = new SqlCommand("SELECT COUNT(*) FROM FMK.FiscalYear WHERE FiscalYearId = @Id", conn);
                cmdId.Parameters.AddWithValue("@Id", fyId);
                int count = Convert.ToInt32(await cmdId.ExecuteScalarAsync(cancellationToken) ?? 0);
                if (count == 0)
                {
                    throw new InvalidOperationException($"دوره مالی مشخص‌شده با شناسه '{fyId}' در جدول دوره‌های مالی سپیدار (FMK.FiscalYear) یافت نشد.");
                }
                return fyId;
            }
            else
            {
                using var cmdTitle = new SqlCommand("SELECT FiscalYearId FROM FMK.FiscalYear WHERE Title = @Title", conn);
                cmdTitle.Parameters.AddWithValue("@Title", configuredFy);
                using var reader = await cmdTitle.ExecuteReaderAsync(cancellationToken);
                var ids = new List<int>();
                while (await reader.ReadAsync(cancellationToken))
                {
                    ids.Add(reader.GetInt32(0));
                }
                if (ids.Count == 0)
                {
                    throw new InvalidOperationException($"دوره مالی مشخص‌شده با عنوان '{configuredFy}' در جدول دوره‌های مالی سپیدار (FMK.FiscalYear) یافت نشد.");
                }
                if (ids.Count > 1)
                {
                    throw new InvalidOperationException($"چندین دوره مالی با عنوان '{configuredFy}' در جدول دوره‌های مالی سپیدار یافت شد (عدم یکتایی).");
                }
                return ids[0];
            }
        }

        // 2. Resolve dynamically based on current date bounds
        var now = DateTime.UtcNow.Date;
        using var cmdDate = new SqlCommand("SELECT FiscalYearId FROM FMK.FiscalYear WHERE @CurrentDate >= StartDate AND @CurrentDate <= EndDate", conn);
        cmdDate.Parameters.AddWithValue("@CurrentDate", now);
        using var dateReader = await cmdDate.ExecuteReaderAsync(cancellationToken);
        var matchedIds = new List<int>();
        while (await dateReader.ReadAsync(cancellationToken))
        {
            matchedIds.Add(dateReader.GetInt32(0));
        }

        if (matchedIds.Count == 0)
        {
            throw new InvalidOperationException($"هیچ دوره مالی منطبق با تاریخ جاری ({now:yyyy-MM-dd}) در سپیدار یافت نشد. انتخاب دوره باید در تنظیمات 'Sepidar:FiscalYear' صریحاً مشخص شود.");
        }
        if (matchedIds.Count > 1)
        {
            throw new InvalidOperationException($"چندین ({matchedIds.Count}) دوره مالی منطبق با تاریخ جاری ({now:yyyy-MM-dd}) در سپیدار یافت شد. جهت جلوگیری از ابهام، شناسه یا عنوان دوره مالی باید در تنظیمات 'Sepidar:FiscalYear' مشخص شود.");
        }

        return matchedIds[0];
    }

    public async Task<List<SepidarStockRecord>> GetStockBalancesAsync(string warehouseRef, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var list = new List<SepidarStockRecord>();

        // Query active inventory snapshot from INV.ItemStockSummary for target warehouse in resolved fiscal year.
        // Reservable and Reserved are kept strictly NULL (unknown) because SaleWithReserveQuantity in Sepidar
        // is exclusively for Sales Orders and is unpopulated for raw materials. Speculative replacement is forbidden.
        const string sql = @"
            SELECT CAST(sb.ItemRef AS NVARCHAR(50)) AS ItemRef,
                   CAST(sb.StockRef AS NVARCHAR(50)) AS WarehouseRef,
                   CAST(ISNULL(sb.Quantity, 0) AS DECIMAL(18,4)) AS OnHand,
                   CAST(NULL AS DECIMAL(18,4)) AS Reservable,
                   CAST(NULL AS DECIMAL(18,4)) AS Reserved,
                   CAST(0 AS DECIMAL(18,4)) AS Incoming,
                   CASE 
                       WHEN sb.UnitRef = 1 OR u.Title LIKE N'%ک%لوگرم%' OR u.Title LIKE N'%ك%لوگرم%' THEN 'KG'
                       WHEN sb.UnitRef = 2 OR u.Title = N'عدد' THEN 'COUNT'
                       ELSE ISNULL(u.Title, 'KG')
                   END AS UnitCode,
                   GETUTCDATE() AS SnapshotTimeUtc
            FROM INV.ItemStockSummary sb
            JOIN INV.Item i ON sb.ItemRef = i.ItemID
            LEFT JOIN INV.Unit u ON sb.UnitRef = u.UnitID
            WHERE CAST(sb.StockRef AS NVARCHAR(50)) = @WarehouseRef
              AND sb.FiscalYearRef = @FiscalYearRef";

        try
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            int fiscalYearId = await ResolveFiscalYearIdAsync(conn, cancellationToken);

            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("@WarehouseRef", warehouseRef);
            cmd.Parameters.AddWithValue("@FiscalYearRef", fiscalYearId);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(new SepidarStockRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetDecimal(2),
                    reader.IsDBNull(3) ? null : reader.GetDecimal(3),
                    reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                    reader.GetDecimal(5),
                    reader.GetString(6),
                    reader.GetDateTime(7)
                ));
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch stock balances from Sepidar live database: {Message}", ex.Message);
            throw new InvalidOperationException($"خطا در دریافت موجودی انبار از سپیدار: {ex.Message}", ex);
        }
    }

    public async Task<List<SepidarMovementRecord>> GetInventoryMovementsAsync(
        string warehouseRef, 
        DateTime? sinceDateUtc = null, 
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var list = new List<SepidarMovementRecord>();

        // Query both Receipts (Receipt, Return to Vendor, Transfer In) and Deliveries (Consumption, Return from Production, Waste, Transfer Out)
        var sql = @"
            SELECT 'REC-' + CAST(ri.InventoryReceiptItemID AS NVARCHAR(50)) AS VoucherRef,
                   CAST(r.Number AS NVARCHAR(50)) AS VoucherNumber,
                   CAST(ri.ItemRef AS NVARCHAR(50)) AS ItemRef,
                   CAST(r.StockRef AS NVARCHAR(50)) AS WarehouseRef,
                   CASE 
                       WHEN r.Type = 1 AND r.IsReturn = 0 THEN 2 -- Receipt (ورود از خرید)
                       WHEN r.Type = 1 AND r.IsReturn = 1 THEN 6 -- Return to vendor (برگشت از خرید)
                       WHEN r.Type = 4 THEN 3                     -- Transfer in (انتقال بین انبارها)
                       ELSE 2
                   END AS MovementType,
                   CAST(ri.Quantity AS DECIMAL(18,4)) AS Quantity,
                   CASE 
                       WHEN i.UnitRef = 1 OR u.Title LIKE N'%ک%لوگرم%' OR u.Title LIKE N'%ك%لوگرم%' THEN 'KG'
                       WHEN i.UnitRef = 2 OR u.Title = N'عدد' THEN 'COUNT'
                       ELSE ISNULL(u.Title, 'KG')
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
              AND r.FiscalYearRef = @FiscalYearRef"
            + (sinceDateUtc.HasValue ? " AND r.Date >= @SinceDate" : "") +
            @"
            UNION ALL
            SELECT 'DEL-' + CAST(di.InventoryDeliveryItemID AS NVARCHAR(50)) AS VoucherRef,
                   CAST(d.Number AS NVARCHAR(50)) AS VoucherNumber,
                   CAST(di.ItemRef AS NVARCHAR(50)) AS ItemRef,
                   CAST(d.StockRef AS NVARCHAR(50)) AS WarehouseRef,
                   CASE 
                       WHEN d.Type = 2 AND d.IsReturn = 0 THEN 1 -- Consumption (مصرف تولید)
                       WHEN d.Type = 2 AND d.IsReturn = 1 THEN 4 -- Return from production -> Adjustment (برگشت از تولید)
                       WHEN d.Type = 3 THEN 5                     -- Waste (ضایعات)
                       WHEN d.Type = 4 THEN 3                     -- Transfer out (انتقال بین انبارها)
                       ELSE 1
                   END AS MovementType,
                   CAST(di.Quantity AS DECIMAL(18,4)) AS Quantity,
                   CASE 
                       WHEN i.UnitRef = 1 OR u.Title LIKE N'%ک%لوگرم%' OR u.Title LIKE N'%ك%لوگرم%' THEN 'KG'
                       WHEN i.UnitRef = 2 OR u.Title = N'عدد' THEN 'COUNT'
                       ELSE ISNULL(u.Title, 'KG')
                   END AS UnitCode,
                   d.Date AS TransactionDateUtc,
                   CASE WHEN d.AccountingVoucherRef IS NOT NULL THEN 4 ELSE 2 END AS PostingStatus,
                   CAST(0 AS BIT) AS IsCancelled,
                   d.Description AS ReferenceDocument
            FROM INV.InventoryDeliveryItem di
            JOIN INV.InventoryDelivery d ON di.InventoryDeliveryRef = d.InventoryDeliveryID
            JOIN INV.Item i ON di.ItemRef = i.ItemID
            LEFT JOIN INV.Unit u ON i.UnitRef = u.UnitID
            WHERE CAST(d.StockRef AS NVARCHAR(50)) = @WarehouseRef
              AND d.FiscalYearRef = @FiscalYearRef"
            + (sinceDateUtc.HasValue ? " AND d.Date >= @SinceDate" : "");

        try
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            int fiscalYearId = await ResolveFiscalYearIdAsync(conn, cancellationToken);

            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("@WarehouseRef", warehouseRef);
            cmd.Parameters.AddWithValue("@FiscalYearRef", fiscalYearId);
            if (sinceDateUtc.HasValue)
            {
                cmd.Parameters.AddWithValue("@SinceDate", sinceDateUtc.Value);
            }

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                int rawType = reader.GetInt32(4);
                var movementType = (InventoryMovementType)rawType;

                list.Add(new SepidarMovementRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    movementType,
                    reader.GetDecimal(5),
                    reader.GetString(6),
                    reader.GetDateTime(7),
                    reader.GetInt32(8),
                    reader.GetBoolean(9),
                    reader.IsDBNull(10) ? null : reader.GetString(10)
                ));
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch inventory movements from Sepidar live database: {Message}", ex.Message);
            throw new InvalidOperationException($"خطا در دریافت گردش اسناد انبار از سپیدار: {ex.Message}", ex);
        }
    }
}
