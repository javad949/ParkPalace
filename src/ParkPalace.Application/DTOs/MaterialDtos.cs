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
}

public class CreateUnitConversionRequest
{
    [Required(ErrorMessage = "واحد مبدا الزامی است")]
    public Guid FromUnitId { get; set; }

    [Required(ErrorMessage = "واحد مقصد الزامی است")]
    public Guid ToUnitId { get; set; }

    [Range(0.000001, 100000000, ErrorMessage = "ضریب تبدیل باید اکیداً مثبت و معتبر باشد")]
    public decimal Factor { get; set; }
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
