using System;
using System.Collections.Generic;
using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class TelephonyGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid? SupervisorUserId { get; set; }
    public AppUser? SupervisorUser { get; set; }
    public string? SupervisorExtension { get; set; }
    public string? SupervisorDisplayName { get; set; }

    public ICollection<TelephonyGroupMember> Members { get; set; } = new List<TelephonyGroupMember>();
}

public class TelephonyGroupMember : BaseEntity
{
    public Guid GroupId { get; set; }
    public TelephonyGroup? Group { get; set; }

    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }

    public string Extension { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool IsSupervisor { get; set; } = false;
}
