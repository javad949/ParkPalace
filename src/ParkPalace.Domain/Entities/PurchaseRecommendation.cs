using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public enum RecommendationStatus
{
    Active = 1,
    Dismissed = 2,
    ConvertedToOrder = 3
}

public class PurchaseRecommendation : BaseEntity
{
    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }

    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public decimal CurrentOnHand { get; set; }
    public decimal ReservableOnHand { get; set; }
    public decimal IncomingOrders { get; set; }
    public decimal ReservedQuantity { get; set; }

    public decimal AverageDailyConsumption { get; set; }
    public int LeadTimeDays { get; set; }
    public int ReviewPeriodDays { get; set; }
    public decimal SafetyStock { get; set; }

    public decimal TargetStock { get; set; }
    public decimal InventoryPosition { get; set; }
    public decimal RawSuggestedQuantity { get; set; }
    public decimal RoundedSuggestedQuantity { get; set; }

    public Guid UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }

    public Guid? PreferredSupplierId { get; set; }
    public Party? PreferredSupplier { get; set; }

    public RecommendationStatus Status { get; set; } = RecommendationStatus.Active;
    public string? RecommendationExplanation { get; set; }
    public string? DataWarning { get; set; } // e.g. "Data stale by 3 days", "Incoming orders estimated"
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}
