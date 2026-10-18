using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class PartyAddress : BaseEntity
{
    public Guid PartyId { get; set; }
    public Party? Party { get; set; }

    public string AddressType { get; set; } = "Factory"; // Factory, Office, Warehouse, Store
    public string Province { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string AddressText { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public bool IsPrimary { get; set; } = false;
}
