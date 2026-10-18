using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class UnitOfMeasure : BaseEntity
{
    public string Code { get; set; } = string.Empty; // e.g. "KG", "GR", "L", "PACK", "COUNT"
    public string Name { get; set; } = string.Empty; // e.g. "کیلوگرم", "گرم", "لیتر", "بسته/کارتن", "عدد"
    public int Precision { get; set; } = 2;          // Decimal places for quantities
    public string Dimension { get; set; } = "Mass";  // Mass, Volume, Count, Packaging
    public bool IsActive { get; set; } = true;
}
