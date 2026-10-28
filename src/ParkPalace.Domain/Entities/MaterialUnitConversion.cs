using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class MaterialUnitConversion : BaseEntity
{
    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }

    public Guid FromUnitId { get; set; }
    public UnitOfMeasure? FromUnit { get; set; }

    public Guid ToUnitId { get; set; }
    public UnitOfMeasure? ToUnit { get; set; }

    // 1 FromUnit = Factor * ToUnit (e.g. 1 Carton = 25 KG)
    public decimal Factor { get; set; }

    public bool IsConfirmed { get; set; } = false;
    public string? Notes { get; set; }
}
