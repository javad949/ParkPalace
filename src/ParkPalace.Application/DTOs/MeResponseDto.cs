namespace ParkPalace.Application.DTOs;

public class MeResponseDto
{
    public Guid? Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? AdSid { get; set; }
    public bool IsAuthenticated { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
}

public class RoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
}

public class HealthResponseDto
{
    public string Status { get; set; } = "Healthy";
    public string Service { get; set; } = "ParkPalace Procurement CRM API";
    public string Version { get; set; } = "1.0.0";
    public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;
    public bool DatabaseConnected { get; set; }
    public string SepidarIntegrationStatus { get; set; } = "Not configured (Disabled)";
}
