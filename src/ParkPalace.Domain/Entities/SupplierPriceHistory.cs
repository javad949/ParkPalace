using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class SupplierPriceHistory : BaseEntity
{
    public Guid SupplierMaterialId { get; set; }
    public SupplierMaterial? SupplierMaterial { get; set; }

    public decimal Price { get; set; }
    public string Currency { get; set; } = "TOMAN"; // TOMAN or IRR

    public Guid UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }

    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public string? Source { get; set; } // e.g. "پیش‌فاکتور شماره ۱۲۳۴", "استعلام شفاهی"
    public string? Notes { get; set; }
}
