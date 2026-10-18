using System.ComponentModel.DataAnnotations;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Application.DTOs;

public class PartyDto
{
    public Guid Id { get; set; }
    public PartyType PartyType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? Notes { get; set; }

    // Individual
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ShopName { get; set; }
    public string? NationalId { get; set; }

    // Legal
    public string? LegalName { get; set; }
    public string? NationalIdentifier { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? EconomicCode { get; set; }

    public string? PrimaryPhone { get; set; }
    public string? PrimaryContactName { get; set; }
    public string? PrimaryAddressCity { get; set; }
    public int SuppliedMaterialsCount { get; set; }
}

public class PartyDetailDto : PartyDto
{
    public List<PartyContactDto> Contacts { get; set; } = new();
    public List<PartyPhoneDto> Phones { get; set; } = new();
    public List<PartyAddressDto> Addresses { get; set; } = new();
    public List<SupplierMaterialDto> SuppliedMaterials { get; set; } = new();
    public List<PartyDocumentDto> Documents { get; set; } = new();
}

public class CreatePartyRequest
{
    [Required]
    public PartyType PartyType { get; set; } = PartyType.LegalEntity;

    [Required(ErrorMessage = "نام نمایشی تأمین‌کننده الزامی است")]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Individual profile
    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    [MaxLength(200)]
    public string? ShopName { get; set; }

    [MaxLength(20)]
    public string? NationalId { get; set; }

    // Legal Entity profile
    [MaxLength(200)]
    public string? LegalName { get; set; }

    [MaxLength(30)]
    public string? NationalIdentifier { get; set; }

    [MaxLength(50)]
    public string? RegistrationNumber { get; set; }

    [MaxLength(30)]
    public string? EconomicCode { get; set; }

    // Initial Primary Contact & Phone (optional upon creation)
    public string? PrimaryContactName { get; set; }
    public string? PrimaryContactPosition { get; set; }
    public string? PrimaryPhone { get; set; }
    public string? PrimaryPhoneType { get; set; } = "Mobile";
    public string? PrimaryAddressCity { get; set; }
    public string? PrimaryAddressText { get; set; }
}

public class UpdatePartyRequest
{
    [Required(ErrorMessage = "نام نمایشی تأمین‌کننده الزامی است")]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Individual
    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    [MaxLength(200)]
    public string? ShopName { get; set; }

    [MaxLength(20)]
    public string? NationalId { get; set; }

    // Legal
    [MaxLength(200)]
    public string? LegalName { get; set; }

    [MaxLength(30)]
    public string? NationalIdentifier { get; set; }

    [MaxLength(50)]
    public string? RegistrationNumber { get; set; }

    [MaxLength(30)]
    public string? EconomicCode { get; set; }

    public bool IsActive { get; set; } = true;
}

public class PartyContactDto
{
    public Guid Id { get; set; }
    public Guid PartyId { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public string? Position { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }
    public List<PartyPhoneDto> Phones { get; set; } = new();
}

public class CreatePartyContactRequest
{
    [Required(ErrorMessage = "نام شخص الزامی است")]
    [MaxLength(150)]
    public string PersonName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Position { get; set; }

    public bool IsPrimary { get; set; } = false;
}

public class PartyPhoneDto
{
    public Guid Id { get; set; }
    public Guid PartyId { get; set; }
    public Guid? ContactId { get; set; }
    public string? ContactName { get; set; }
    public string PhoneType { get; set; } = "Mobile";
    public string OriginalValue { get; set; } = string.Empty;
    public string NormalizedValue { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

public class CreatePartyPhoneRequest
{
    public Guid? ContactId { get; set; }

    [Required(ErrorMessage = "نوع شماره الزامی است")]
    public string PhoneType { get; set; } = "Mobile"; // Mobile, Landline, Fax

    [Required(ErrorMessage = "شماره تلفن الزامی است")]
    [MaxLength(50)]
    public string PhoneNumber { get; set; } = string.Empty;

    public bool IsPrimary { get; set; } = false;
}

public class PartyAddressDto
{
    public Guid Id { get; set; }
    public Guid PartyId { get; set; }
    public string AddressType { get; set; } = "Factory";
    public string Province { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string AddressText { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public bool IsPrimary { get; set; }
}

public class CreatePartyAddressRequest
{
    public string AddressType { get; set; } = "Factory";

    [Required(ErrorMessage = "نام استان الزامی است")]
    [MaxLength(100)]
    public string Province { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام شهر الزامی است")]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "متن آدرس الزامی است")]
    [MaxLength(500)]
    public string AddressText { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    public bool IsPrimary { get; set; } = false;
}

public class SupplierMaterialDto
{
    public Guid Id { get; set; }
    public Guid PartyId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialCode { get; set; } = string.Empty;
    public bool IsPreferred { get; set; }
    public string Status { get; set; } = "Active";
    public int? DefaultLeadTimeDays { get; set; }
    public decimal? MinimumOrderQty { get; set; }
    public Guid? PurchaseUnitId { get; set; }
    public string? PurchaseUnitName { get; set; }
    public string? Notes { get; set; }
    public List<SupplierPriceHistoryDto> PriceHistories { get; set; } = new();
}

public class AddSupplierMaterialRequest
{
    [Required(ErrorMessage = "انتخاب ماده اولیه الزامی است")]
    public Guid MaterialId { get; set; }

    public bool IsPreferred { get; set; } = false;
    public string Status { get; set; } = "Active";
    public int? DefaultLeadTimeDays { get; set; }
    public decimal? MinimumOrderQty { get; set; }
    public Guid? PurchaseUnitId { get; set; }
    public string? Notes { get; set; }

    // Optional initial price quote
    public decimal? InitialPrice { get; set; }
    public string? Currency { get; set; } = "TOMAN";
    public Guid? PriceUnitId { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public string? PriceSource { get; set; }
}

public class SupplierPriceHistoryDto
{
    public Guid Id { get; set; }
    public Guid SupplierMaterialId { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "TOMAN";
    public Guid UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public DateTime RecordedAt { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
}

public class AddPriceHistoryRequest
{
    [Required(ErrorMessage = "مبلغ قیمت الزامی است")]
    [Range(0, 1000000000000, ErrorMessage = "مبلغ باید نامنفی باشد")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "واحد پول الزامی است")]
    public string Currency { get; set; } = "TOMAN"; // TOMAN or IRR

    [Required(ErrorMessage = "واحد سنجش قیمت الزامی است")]
    public Guid UnitId { get; set; }

    [Required(ErrorMessage = "تاریخ آغاز اعتبار الزامی است")]
    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    [MaxLength(200)]
    public string? Source { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class PartyDocumentDto
{
    public Guid Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public DateTime UploadedAtUtc { get; set; }
}
