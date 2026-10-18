using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
