using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class InventoryItemMapping : BaseEntity
{
    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }

    public string SepidarItemId { get; set; } = string.Empty;    // ItemID in Sepidar
    public string SepidarItemCode { get; set; } = string.Empty;  // Code in Sepidar
    public string SepidarItemTitle { get; set; } = string.Empty; // Title in Sepidar
    public string? SepidarBaseUnit { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncUtc { get; set; }
}
