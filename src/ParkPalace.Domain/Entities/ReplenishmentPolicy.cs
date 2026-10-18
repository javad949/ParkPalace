using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class ReplenishmentPolicy : BaseEntity
{
    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }

    public int ReviewPeriodDays { get; set; } = 3;  // Frequency of reviewing stock (days)
    public int LeadTimeDays { get; set; } = 2;       // Supplier delivery lead time (days)
    public decimal SafetyStock { get; set; } = 0;   // Buffer stock
    public decimal MinOrderQuantity { get; set; } = 0; // MOQ
    public decimal? PackageMultiple { get; set; }   // e.g. 50 kg bag, 24 count box
    
    public Guid? PreferredSupplierId { get; set; }
    public Party? PreferredSupplier { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public string? UpdatedBy { get; set; }
}
