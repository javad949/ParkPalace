namespace ParkPalace.Application.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Username { get; }
    string? DisplayName { get; }
    string? AdSid { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<string> AvailableRoles { get; }
    string ActiveRole { get; }
    IReadOnlyList<string> Permissions { get; }
    bool IsAuthenticated { get; }
    bool HasPermission(string permission);
    bool IsInRole(string role);
}
