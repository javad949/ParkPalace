using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class PartyPhone : BaseEntity
{
    public Guid PartyId { get; set; }
    public Party? Party { get; set; }

    public Guid? ContactId { get; set; }
    public PartyContact? Contact { get; set; }

    public string PhoneType { get; set; } = "Mobile"; // Mobile, Landline, Fax
    public string OriginalValue { get; set; } = string.Empty;
    public string NormalizedValue { get; set; } = string.Empty; // e.g. 09121234567 or 02188888888
    public bool IsPrimary { get; set; } = false;
}
