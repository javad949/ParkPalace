using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class MaterialConsumptionSummary : BaseEntity
{
    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }

    public int PeriodDays { get; set; } // 7, 14, or 30
    public decimal TotalConsumption { get; set; }
    public decimal AverageDailyConsumption { get; set; }
    
    public int ActiveDaysCount { get; set; }
    public int ZeroDaysCount { get; set; }
    
    public DateTime CalculatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime FromDateUtc { get; set; }
    public DateTime ToDateUtc { get; set; }
}
