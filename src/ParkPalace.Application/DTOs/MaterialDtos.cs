using System.ComponentModel.DataAnnotations;

namespace ParkPalace.Application.DTOs;

public class MaterialDto
{
    public Guid Id { get; set; }
    public string InternalCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public Guid BaseUnitId { get; set; }
    public string BaseUnitName { get; set; } = string.Empty;
    public string BaseUnitCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Specifications { get; set; }
    public string? Brand { get; set; }
    public string? Packaging { get; set; }
    public decimal? PackageSize { get; set; }
    public Guid? PackageSizeUnitId { get; set; }
    public string? PackageSizeUnitName { get; set; }
    public string? PackageSizeUnitCode { get; set; }
    public Guid? PurchaseUnitId { get; set; }
    public string? PurchaseUnitName { get; set; }
    public string? PurchaseUnitCode { get; set; }
    public Guid? ConsumptionUnitId { get; set; }
    public string? ConsumptionUnitName { get; set; }
    public string? ConsumptionUnitCode { get; set; }
    public string? ProductFamily { get; set; }
    public string CategorizationStatus { get; set; } = "NeedsReview";
    public bool IsCustomizedByUser { get; set; }
    public string? StandardizationNotes { get; set; }
    public string? SepidarItemCode { get; set; }
    public string? SepidarItemTitle { get; set; }
    public string? SepidarBaseUnit { get; set; }
    public decimal? MinStock { get; set; }
    public decimal? SafetyStock { get; set; }
    public int? LeadTimeDays { get; set; }
    public bool IsActive { get; set; }
    public int SupplierCount { get; set; }
    public int ConversionCount { get; set; }
}

public class MaterialDetailDto : MaterialDto
{
    public List<MaterialUnitConversionDto> UnitConversions { get; set; } = new();
    public List<SupplierMaterialSummaryDto> Suppliers { get; set; } = new();
    public List<ExternalItemMappingDto> ExternalMappings { get; set; } = new();
}

public class CreateMaterialRequest
{
    [Required(ErrorMessage = "کد ماده اولیه الزامی است")]
    [MaxLength(50)]
    public string InternalCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام ماده اولیه الزامی است")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "انتخاب دسته‌بندی الزامی است")]
    public Guid CategoryId { get; set; }

    [Required(ErrorMessage = "انتخاب واحد مبنا الزامی است")]
    public Guid BaseUnitId { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(4000)]
    public string? Specifications { get; set; }

    [MaxLength(100)]
    public string? Brand { get; set; }

    [MaxLength(100)]
    public string? Packaging { get; set; }

    public decimal? PackageSize { get; set; }
    public Guid? PackageSizeUnitId { get; set; }
    public Guid? PurchaseUnitId { get; set; }
    public Guid? ConsumptionUnitId { get; set; }

    [MaxLength(150)]
    public string? ProductFamily { get; set; }

    public decimal? MinStock { get; set; }
    public decimal? SafetyStock { get; set; }
    public int? LeadTimeDays { get; set; }
}

public class UpdateMaterialRequest
{
    [Required(ErrorMessage = "نام ماده اولیه الزامی است")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "انتخاب دسته‌بندی الزامی است")]
    public Guid CategoryId { get; set; }

    [Required(ErrorMessage = "انتخاب واحد مبنا الزامی است")]
    public Guid BaseUnitId { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(4000)]
    public string? Specifications { get; set; }

    [MaxLength(100)]
    public string? Brand { get; set; }

    [MaxLength(100)]
    public string? Packaging { get; set; }

    public decimal? PackageSize { get; set; }
    public Guid? PackageSizeUnitId { get; set; }
    public Guid? PurchaseUnitId { get; set; }
    public Guid? ConsumptionUnitId { get; set; }

    [MaxLength(150)]
    public string? ProductFamily { get; set; }

    public string? StandardizationNotes { get; set; }

    public decimal? MinStock { get; set; }
    public decimal? SafetyStock { get; set; }
    public int? LeadTimeDays { get; set; }
    public bool IsActive { get; set; } = true;
}

public class MaterialUnitConversionDto
{
    public Guid Id { get; set; }
    public Guid MaterialId { get; set; }
    public Guid FromUnitId { get; set; }
    public string FromUnitName { get; set; } = string.Empty;
    public string FromUnitCode { get; set; } = string.Empty;
    public Guid ToUnitId { get; set; }
    public string ToUnitName { get; set; } = string.Empty;
    public string ToUnitCode { get; set; } = string.Empty;
    public decimal Factor { get; set; }
    public bool IsConfirmed { get; set; } = false;
    public string? Notes { get; set; }
}

public class StandardizedCatalogItemSuggestion
{
    public string RawTitle { get; set; } = string.Empty;
    public string StandardName { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Packaging { get; set; }
    public decimal? PackageSize { get; set; }
    public string? PackageSizeUnitCode { get; set; }
    public string? ParentCategoryName { get; set; }
    public string? ParentCategoryCode { get; set; }
    public string? SubcategoryName { get; set; }
    public string? SubcategoryCode { get; set; }
    public string? ProductFamily { get; set; }
    public string BaseUnitCode { get; set; } = "COUNT";
    public string PurchaseUnitCode { get; set; } = "COUNT";
    public string? ConsumptionUnitCode { get; set; }
    public decimal? SuggestedConversionFactor { get; set; }
    public string? SuggestedConversionToUnit { get; set; }
    public string CategoryStatus { get; set; } = "Ambiguous"; // "Clear" or "Ambiguous"
    public string BrandStatus { get; set; } = "Unknown"; // "Clear" or "Unknown"
    public string PackagingStatus { get; set; } = "Unknown"; // "Clear" or "Unknown"
    public string PackageSizeStatus { get; set; } = "Unknown"; // "Clear" or "Unknown"
    public string Status { get; set; } = "NeedsReview"; // "AutoConfirmed" or "NeedsReview"
    public string Explanation { get; set; } = string.Empty;
}

public class SepidarCorrectionProposalDto
{
    public string SepidarItemId { get; set; } = string.Empty;
    public string SepidarItemCode { get; set; } = string.Empty;
    public string CurrentSepidarTitle { get; set; } = string.Empty;
    public string ProposedStandardName { get; set; } = string.Empty;
    public string CurrentSepidarUnit { get; set; } = string.Empty;
    public string ProposedPurchaseUnit { get; set; } = string.Empty;
    public string ProposedBaseUnit { get; set; } = string.Empty;
    public string CurrentCategoryName { get; set; } = string.Empty;
    public string ProposedCategoryName { get; set; } = string.Empty;
    public decimal? PackageSize { get; set; }
    public string? PackageSizeUnit { get; set; }
    public string Justification { get; set; } = string.Empty;
}

public class CreateUnitConversionRequest
{
    [Required(ErrorMessage = "واحد مبدا الزامی است")]
    public Guid FromUnitId { get; set; }

    [Required(ErrorMessage = "واحد مقصد الزامی است")]
    public Guid ToUnitId { get; set; }

    [Range(0.000001, 100000000, ErrorMessage = "ضریب تبدیل باید اکیداً مثبت و معتبر باشد")]
    public decimal Factor { get; set; }

    public bool? IsConfirmed { get; set; }
    public string? Notes { get; set; }
}

public class ExternalItemMappingDto
{
    public Guid Id { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string SourceCompany { get; set; } = string.Empty;
    public string ExternalItemId { get; set; } = string.Empty;
    public string ExternalItemCode { get; set; } = string.Empty;
    public DateTime? LastSeenUtc { get; set; }
}

public class SupplierMaterialSummaryDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public bool IsPreferred { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? LatestPrice { get; set; }
    public string? Currency { get; set; }
    public string? PriceUnitName { get; set; }
    public DateTime? PriceEffectiveFrom { get; set; }
}

public class CategoryChangePlanItemDto
{
    public Guid MaterialId { get; set; }
    public string InternalCode { get; set; } = string.Empty;
    public string CurrentName { get; set; } = string.Empty;
    public Guid CurrentCategoryId { get; set; }
    public string CurrentCategoryName { get; set; } = string.Empty;
    public string ProposedParentCategoryCode { get; set; } = string.Empty;
    public string ProposedParentCategoryName { get; set; } = string.Empty;
    public string? ProposedSubcategoryCode { get; set; }
    public string? ProposedSubcategoryName { get; set; }
    public string ProposedCategoryDisplayName => !string.IsNullOrEmpty(ProposedSubcategoryName)
        ? $"{ProposedParentCategoryName} / {ProposedSubcategoryName}"
        : ProposedParentCategoryName;
    public bool IsChanged { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

public class CategoryUpdateResultDto
{
    public bool Success { get; set; }
    public Guid MaterialId { get; set; }
    public bool IsUnchanged { get; set; }
    public Guid? OldCategoryId { get; set; }
    public Guid? NewCategoryId { get; set; }
    public string? OldCategoryName { get; set; }
    public string? NewCategoryName { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
}

