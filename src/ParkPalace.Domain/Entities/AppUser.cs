using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class AppUser : BaseEntity
{
    /// <summary>
    /// Immutable Active Directory Object SID or GUID for reliable enterprise identity correlation.
    /// </summary>
    public string AdObjectSid { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime? LastLoginAtUtc { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
