using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class PartyDocument : BaseEntity
{
    public Guid PartyId { get; set; }
    public Party? Party { get; set; }

    public string DocumentType { get; set; } = "OfficialGazette"; // OfficialGazette, TaxCertificate, Contract, Catalog
    public string FileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = "application/pdf";
    public string StorageReference { get; set; } = string.Empty;
    public Guid? UploadedByUserId { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
