using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class InventorySnapshot : BaseEntity
{
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }

    public decimal OnHandQuantity { get; set; }        // Physical current stock
    public decimal ReservableQuantity { get; set; }    // Usable stock = OnHand - Reserved
    public decimal IncomingQuantity { get; set; }      // Eligible POs in progress
    public decimal ReservedQuantity { get; set; }      // Reserved for production orders

    public Guid UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }

    public DateTime SnapshotTimeUtc { get; set; } = DateTime.UtcNow;
    public bool IsStale { get; set; }                  // True if sync is older than threshold
    public string? SourceSnapshotKey { get; set; }     // e.g. "WH1-ITEM101-20261017"
}
