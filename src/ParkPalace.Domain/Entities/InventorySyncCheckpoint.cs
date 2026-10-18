using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class InventorySyncCheckpoint : BaseEntity
{
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public string LastVoucherId { get; set; } = string.Empty;
    public DateTime? LastVoucherDateUtc { get; set; }
    public DateTime LastSyncTimeUtc { get; set; } = DateTime.UtcNow;
}
