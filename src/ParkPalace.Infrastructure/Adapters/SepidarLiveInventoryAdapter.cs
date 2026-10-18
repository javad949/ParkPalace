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
            SELECT CAST(WarehouseID AS NVARCHAR(50)) AS WarehouseRef, 
                   Code, 
                   Title, 
                   CAST(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END AS BIT) AS IsActive
            FROM INV.Warehouse
            WHERE IsActive = 1";

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

    public async Task<List<SepidarItemRecord>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var list = new List<SepidarItemRecord>();

        const string sql = @"
            SELECT CAST(i.ItemID AS NVARCHAR(50)) AS ItemRef, 
                   i.Code, 
                   i.Title, 
                   ISNULL(u.Code, 'KG') AS UnitCode, 
                   ISNULL(u.Title, N'کیلوگرم') AS UnitTitle,
                   CAST(CASE WHEN i.IsActive = 1 THEN 1 ELSE 0 END AS BIT) AS IsActive
            FROM INV.Item i
            LEFT JOIN GNR.Unit u ON i.UnitRef = u.UnitID
            WHERE i.IsActive = 1";

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

    public async Task<List<SepidarStockRecord>> GetStockBalancesAsync(string warehouseRef, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var list = new List<SepidarStockRecord>();

        const string sql = @"
            SELECT CAST(sb.ItemRef AS NVARCHAR(50)) AS ItemRef,
                   CAST(sb.WarehouseRef AS NVARCHAR(50)) AS WarehouseRef,
                   CAST(sb.OnHand AS DECIMAL(18,4)) AS OnHand,
                   CAST(ISNULL(sb.ReservableStock, sb.OnHand) AS DECIMAL(18,4)) AS Reservable,
                   CAST(ISNULL(sb.ReservedStock, 0) AS DECIMAL(18,4)) AS Reserved,
                   CAST(0 AS DECIMAL(18,4)) AS Incoming,
                   ISNULL(u.Code, 'KG') AS UnitCode,
                   GETUTCDATE() AS SnapshotTimeUtc
            FROM INV.StockBalance sb
            JOIN INV.Item i ON sb.ItemRef = i.ItemID
            LEFT JOIN GNR.Unit u ON i.UnitRef = u.UnitID
            WHERE sb.WarehouseRef = @WarehouseRef";

        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
        cmd.Parameters.AddWithValue("@WarehouseRef", warehouseRef);
        await conn.OpenAsync(cancellationToken);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new SepidarStockRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.GetDecimal(3),
                reader.GetDecimal(4),
                reader.GetDecimal(5),
                reader.GetString(6),
                reader.GetDateTime(7)
            ));
        }

        return list;
    }

    public async Task<List<SepidarMovementRecord>> GetInventoryMovementsAsync(
        string warehouseRef, 
        DateTime? sinceDateUtc = null, 
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var list = new List<SepidarMovementRecord>();

        var sql = @"
            SELECT CAST(vi.InventoryVoucherItemID AS NVARCHAR(50)) AS VoucherRef,
                   CAST(v.VoucherNumber AS NVARCHAR(50)) AS VoucherNumber,
                   CAST(vi.ItemRef AS NVARCHAR(50)) AS ItemRef,
                   CAST(v.SourceWarehouseRef AS NVARCHAR(50)) AS WarehouseRef,
                   v.Type AS MovementType,
                   CAST(vi.Quantity AS DECIMAL(18,4)) AS Quantity,
                   ISNULL(u.Code, 'KG') AS UnitCode,
                   v.Date AS TransactionDateUtc,
                   ISNULL(v.State, 4) AS PostingStatus,
                   CAST(CASE WHEN v.IsCancelled = 1 THEN 1 ELSE 0 END AS BIT) AS IsCancelled,
                   v.ReferenceDocument
            FROM INV.InventoryVoucherItem vi
            JOIN INV.InventoryVoucher v ON vi.InventoryVoucherRef = v.InventoryVoucherID
            JOIN INV.Item i ON vi.ItemRef = i.ItemID
            LEFT JOIN GNR.Unit u ON i.UnitRef = u.UnitID
            WHERE (v.SourceWarehouseRef = @WarehouseRef OR v.DestinationWarehouseRef = @WarehouseRef)";

        if (sinceDateUtc.HasValue)
        {
            sql += " AND v.Date >= @SinceDate";
        }

        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
        cmd.Parameters.AddWithValue("@WarehouseRef", warehouseRef);
        if (sinceDateUtc.HasValue)
        {
            cmd.Parameters.AddWithValue("@SinceDate", sinceDateUtc.Value);
        }

        await conn.OpenAsync(cancellationToken);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            int rawType = reader.GetInt32(4);
            var movementType = rawType switch
            {
                11 => InventoryMovementType.Receipt,
                21 => InventoryMovementType.Consumption,
                31 or 32 => InventoryMovementType.Transfer,
                41 => InventoryMovementType.Return,
                51 => InventoryMovementType.Waste,
                61 => InventoryMovementType.Adjustment,
                _ => InventoryMovementType.Consumption
            };

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
}
