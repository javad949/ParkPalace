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
    public string? Brand { get; set; }                      // e.g. "دامداران", "کاله"
    public string? Packaging { get; set; }                  // e.g. "پاکت", "بطری", "دبه", "کیسه", "کارتن"
    public decimal? PackageSize { get; set; }               // e.g. 1.0, 250, 810
    public Guid? PackageSizeUnitId { get; set; }            // Unit of PackageSize: L, ML, KG, GR
    public UnitOfMeasure? PackageSizeUnit { get; set; }

    public Guid? PurchaseUnitId { get; set; }               // Unit of purchasing/ordering: e.g. COUNT (عدد)
    public UnitOfMeasure? PurchaseUnit { get; set; }

    public Guid? ConsumptionUnitId { get; set; }            // Suggested consumption unit: e.g. L (لیتر)
    public UnitOfMeasure? ConsumptionUnit { get; set; }

    public string? ProductFamily { get; set; }              // Product family grouping: e.g. "شیر پاکتی دامداران"
    public string CategorizationStatus { get; set; } = "NeedsReview"; // "AutoConfirmed", "NeedsReview", "ManualConfirmed"
    public bool IsCustomizedByUser { get; set; } = false;   // Protects manual edits from being overwritten by Sync
    public string? StandardizationNotes { get; set; }
    
    public decimal? MinStock { get; set; }
    public decimal? SafetyStock { get; set; }
    public int? LeadTimeDays { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<MaterialUnitConversion> UnitConversions { get; set; } = new List<MaterialUnitConversion>();
    public ICollection<SupplierMaterial> SupplierMaterials { get; set; } = new List<SupplierMaterial>();
    public ICollection<ExternalItemMapping> ExternalMappings { get; set; } = new List<ExternalItemMapping>();
}
