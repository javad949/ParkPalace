using System;
using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class UserExtension : BaseEntity
{
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }

    public string Username { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty; // e.g. "101", "102"
    public string DeviceType { get; set; } = "SIP"; // SIP, PJSIP, WebRTC
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
}
