using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public enum InventoryMovementType
{
    Unknown = 0,     // نوع پشتیبانی‌نشده یا فاقد نگاشت تأییدشده در سپیدار
    Consumption = 1, // حواله خروج به خط تولید نان و قنادی
    Receipt = 2,     // رسید ورود کالا از خرید
    Transfer = 3,    // انتقال بین انبارها
    Adjustment = 4,  // تعدیل انبارگردانی
    Waste = 5,       // ضایعات تولید
    Return = 6       // مرجوعی به فروشنده
}

public class InventoryMovement : BaseEntity
{
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }

    public string VoucherNumber { get; set; } = string.Empty;
    public InventoryMovementType MovementType { get; set; } = InventoryMovementType.Consumption;
    
    public decimal Quantity { get; set; } // Always positive; direction defined by MovementType
    public Guid UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }

    public DateTime TransactionDateUtc { get; set; }
    public int PostingStatus { get; set; } = 4; // 1: Draft, 2: Temporary, 3: Approved, 4: Posted
    public bool IsCancelled { get; set; } = false;

    public string? ReferenceDocument { get; set; } // External invoice or production batch
    public string? SourceMovementKey { get; set; }  // Unique key in source to ensure idempotent imports
    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;
}
