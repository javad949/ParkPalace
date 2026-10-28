using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class PartyContact : BaseEntity
{
    public Guid PartyId { get; set; }
    public Party? Party { get; set; }

    public string PersonName { get; set; } = string.Empty;
    public string? Position { get; set; } // e.g. "ویزیتور آرد", "مدیر فروش", "مدیر عامل"
    public bool IsPrimary { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public ICollection<PartyPhone> Phones { get; set; } = new List<PartyPhone>();
}
