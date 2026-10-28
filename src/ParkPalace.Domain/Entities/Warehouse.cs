using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class Warehouse : BaseEntity
{
    public string Code { get; set; } = string.Empty; // e.g. "WH-MAIN"
    public string Name { get; set; } = string.Empty; // e.g. "انبار مرکزی کارخانه نان"
    public string? Description { get; set; }
    public string? SepidarWarehouseRef { get; set; } // External key in Sepidar (e.g. "1")
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncUtc { get; set; }

    public ICollection<InventorySnapshot> Snapshots { get; set; } = new List<InventorySnapshot>();
    public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
}
