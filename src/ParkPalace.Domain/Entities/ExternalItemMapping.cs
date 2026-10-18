using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class ExternalItemMapping : BaseEntity
{
    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }

    public string SourceSystem { get; set; } = "Sepidar";
    public string SourceCompany { get; set; } = "Sepidar01";
    public string ExternalItemId { get; set; } = string.Empty;
    public string ExternalItemCode { get; set; } = string.Empty;
    public DateTime? LastSeenUtc { get; set; }
}
