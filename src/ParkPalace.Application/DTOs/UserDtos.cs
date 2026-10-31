using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Application.DTOs;

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? AdObjectSid { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastLoginAtUtc { get; set; }
    public List<string> Roles { get; set; } = new();
    public string? Extension { get; set; }
    public string? DeviceType { get; set; }
    public string? AdDepartment { get; set; }
    public string? AdTitle { get; set; }
}

public class AdUserDirectoryDto
{
    public string SamAccountName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Title { get; set; }
    public string? Department { get; set; }
    public string? TelephoneNumber { get; set; }
    public string? IpPhone { get; set; }
    public string? Mobile { get; set; }
    public string Sid { get; set; } = string.Empty;
    public bool IsDisabled { get; set; }
    public bool IsImportedInCrm { get; set; }
    public Guid? ExistingUserId { get; set; }
    public List<string> ExistingRoles { get; set; } = new();
    public string? ExistingExtension { get; set; }
}

public class UpdateUserRolesRequest
{
    [Required]
    public List<string> Roles { get; set; } = new();
}

public class UpdateUserExtensionRequest
{
    [MaxLength(20)]
    public string? Extension { get; set; }
    public string DeviceType { get; set; } = "SIP";
    public string? Description { get; set; }
}

public class UpdateUserStatusRequest
{
    public bool IsEnabled { get; set; }
}

public class SyncAdUsersRequest
{
    public List<string>? SamAccountNames { get; set; }
    public string? DefaultRole { get; set; }
    public bool OverwriteExistingExtensions { get; set; } = false;
}

public class AvailableExtensionDto
{
    public string Extension { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DeviceType { get; set; } = "SIP";
    public bool IsAssigned { get; set; }
    public Guid? AssignedUserId { get; set; }
    public string? AssignedUsername { get; set; }
    public string? AssignedDisplayName { get; set; }
    public string Status { get; set; } = "Available"; // Available, Busy, Offline
}
