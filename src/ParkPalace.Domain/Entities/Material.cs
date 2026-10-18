using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class Material : BaseEntity
{
    public string InternalCode { get; set; } = string.Empty; // e.g. "MAT-1001"
    public string Name { get; set; } = string.Empty;         // e.g. "آرد ستاره نانوایی ۲۱٪"
    
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    public Guid BaseUnitId { get; set; }
    public UnitOfMeasure? BaseUnit { get; set; }

    public string? Description { get; set; }
    public string? Specifications { get; set; }             // JSON or formatted specs (moisture, ash, protein, packaging)
    public string? Brand { get; set; }                      // Optional brand name (e.g. آرد زرین)
    
    public decimal? MinStock { get; set; }
    public decimal? SafetyStock { get; set; }
    public int? LeadTimeDays { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<MaterialUnitConversion> UnitConversions { get; set; } = new List<MaterialUnitConversion>();
    public ICollection<SupplierMaterial> SupplierMaterials { get; set; } = new List<SupplierMaterial>();
    public ICollection<ExternalItemMapping> ExternalMappings { get; set; } = new List<ExternalItemMapping>();
}
