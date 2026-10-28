using ParkPalace.Domain.Entities;

namespace ParkPalace.Application.Interfaces;

public record SepidarWarehouseRecord(
    string WarehouseRef,
    string Code,
    string Title,
    bool IsActive
);

public record SepidarItemRecord(
    string ItemRef,
    string Code,
    string Title,
    string UnitCode,
    string UnitTitle,
    bool IsActive
);

public record SepidarStockRecord(
    string ItemRef,
    string WarehouseRef,
    decimal OnHand,
    decimal? Reservable,
    decimal? Reserved,
    decimal Incoming,
    string UnitCode,
    DateTime SnapshotTimeUtc
);

public record SepidarMovementRecord(
    string VoucherRef,
    string VoucherNumber,
    string ItemRef,
    string WarehouseRef,
    InventoryMovementType MovementType,
    decimal Quantity,
    string UnitCode,
    DateTime TransactionDateUtc,
    int PostingStatus,
    bool IsCancelled,
    string? ReferenceDocument
);

public interface ISepidarInventoryAdapter
{
    bool IsLiveConnected { get; }
    string AdapterName { get; }
    Task<List<SepidarWarehouseRecord>> GetWarehousesAsync(CancellationToken cancellationToken = default);
    Task<List<SepidarItemRecord>> GetItemsAsync(CancellationToken cancellationToken = default);
    Task<List<SepidarStockRecord>> GetStockBalancesAsync(string warehouseRef, CancellationToken cancellationToken = default);
    Task<List<SepidarMovementRecord>> GetInventoryMovementsAsync(string warehouseRef, DateTime? sinceDateUtc = null, CancellationToken cancellationToken = default);
}
