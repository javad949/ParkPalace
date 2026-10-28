using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public enum PartyType
{
    Individual = 1,
    LegalEntity = 2
}

public class Party : BaseEntity
{
    public PartyType PartyType { get; set; } = PartyType.LegalEntity;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    // Individual profile (شخص حقیقی)
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ShopName { get; set; }
    public string? NationalId { get; set; } // کدملی ۱۰ رقمی

    // Legal Entity profile (شخص حقوقی)
    public string? LegalName { get; set; }
    public string? NationalIdentifier { get; set; } // شناسه ملی ۱۱ رقمی
    public string? RegistrationNumber { get; set; }  // شماره ثبت
    public string? EconomicCode { get; set; }       // کد اقتصادی

    public ICollection<PartyContact> Contacts { get; set; } = new List<PartyContact>();
    public ICollection<PartyPhone> Phones { get; set; } = new List<PartyPhone>();
    public ICollection<PartyAddress> Addresses { get; set; } = new List<PartyAddress>();
    public ICollection<SupplierMaterial> SupplierMaterials { get; set; } = new List<SupplierMaterial>();
    public ICollection<PartyDocument> Documents { get; set; } = new List<PartyDocument>();
}
