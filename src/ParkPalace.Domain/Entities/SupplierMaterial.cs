using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class SupplierMaterial : BaseEntity
{
    public Guid PartyId { get; set; }
    public Party? Party { get; set; }

    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }

    public bool IsPreferred { get; set; } = false;
    public string Status { get; set; } = "Active"; // Active, Suspended, UnderReview
    public int? DefaultLeadTimeDays { get; set; }
    public decimal? MinimumOrderQty { get; set; }

    public Guid? PurchaseUnitId { get; set; }
    public UnitOfMeasure? PurchaseUnit { get; set; }

    public string? Notes { get; set; }

    public ICollection<SupplierPriceHistory> PriceHistories { get; set; } = new List<SupplierPriceHistory>();
}
