using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class InventorySyncError : BaseEntity
{
    public Guid JobId { get; set; }
    public InventorySyncJob? Job { get; set; }

    public string? ItemKey { get; set; }
    public string? VoucherNumber { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? RawPayload { get; set; }
}
