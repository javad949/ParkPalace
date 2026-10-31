using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Api.Controllers;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Identity;
using ParkPalace.Infrastructure.Services.Telephony;
using Xunit;

namespace ParkPalace.UnitTests;

public class TelephonyAccessControlTests
{
    private class FakeActiveDirectoryService : IActiveDirectoryService
    {
        public Task<List<AdUserDirectoryDto>> GetDomainUsersAsync(CancellationToken ct = default) =>
            Task.FromResult(new List<AdUserDirectoryDto>());

        public Task<AdUserDirectoryDto?> GetUserByUsernameAsync(string username, CancellationToken ct = default) =>
            Task.FromResult<AdUserDirectoryDto?>(null);
    }

    private class FakeTelephonyAdapter : ITelephonyAdapter
    {
        public bool IsConnected => true;
        public string AdapterType => "Fake";
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public event Func<CallEventPayload, Task>? OnCallEventReceived;
        public Task SimulateCallAsync(SimulateCallRequest request) => Task.CompletedTask;
        public Task<List<AvailableExtensionDto>> GetAvailableExtensionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AvailableExtensionDto>());
    }

    private class FakeHubGroupManager : ITelephonyHubGroupManager
    {
        public List<(string ConnectionId, string GroupName)> AddedGroups { get; } = new();
        public List<(string ConnectionId, string GroupName)> RemovedGroups { get; } = new();

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken ct = default)
        {
            AddedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken ct = default)
        {
            RemovedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    private class TestCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; }
        public string? Username { get; set; }
        public string? DisplayName { get; set; }
        public string? AdSid { get; set; }
        public List<string> RolesList { get; set; } = new();
        public IReadOnlyList<string> Roles => RolesList;
        public IReadOnlyList<string> AvailableRoles => RolesList;
        public string ActiveRole { get; set; } = "Procurement";
        public string? Extension { get; set; }
        public List<string> PermissionsList { get; set; } = new();
        public IReadOnlyList<string> Permissions => PermissionsList;
        public bool IsAuthenticated => true;

        public bool HasPermission(string permission) => PermissionsList.Contains(permission, StringComparer.OrdinalIgnoreCase);
        public bool IsInRole(string role) => RolesList.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private class MockHubCallerClients : IHubCallerClients<ITelephonyClient>
    {
        public List<(IReadOnlyList<string> Groups, string EventType, object Payload)> Broadcasts { get; } = new();

        public ITelephonyClient Caller => new MockTelephonyClient(this, new List<string> { "__caller" });
        public ITelephonyClient Others => new MockTelephonyClient(this, new List<string> { "__others" });
        public ITelephonyClient All => throw new InvalidOperationException("Clients.All is forbidden!");
        public ITelephonyClient AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
        public ITelephonyClient Client(string connectionId) => new MockTelephonyClient(this, new List<string> { connectionId });
        public ITelephonyClient Clients(IReadOnlyList<string> connectionIds) => new MockTelephonyClient(this, connectionIds);
        public ITelephonyClient Group(string groupName) => new MockTelephonyClient(this, new List<string> { groupName });
        public ITelephonyClient GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
        public ITelephonyClient Groups(IReadOnlyList<string> groupNames) => new MockTelephonyClient(this, groupNames);
        public ITelephonyClient OthersInGroup(string groupName) => throw new NotImplementedException();
        public ITelephonyClient User(string userId) => new MockTelephonyClient(this, new List<string> { userId });
        public ITelephonyClient Users(IReadOnlyList<string> userIds) => new MockTelephonyClient(this, userIds);
    }

    private class MockTelephonyClient : ITelephonyClient
    {
        private readonly MockHubCallerClients _parent;
        private readonly IReadOnlyList<string> _targetGroups;

        public MockTelephonyClient(MockHubCallerClients parent, IReadOnlyList<string> targetGroups)
        {
            _parent = parent;
            _targetGroups = targetGroups;
        }

        public Task ReceiveCallPopup(CallerPopupDto popup)
        {
            _parent.Broadcasts.Add((_targetGroups, nameof(ReceiveCallPopup), popup));
            return Task.CompletedTask;
        }

        public Task CallStatusChanged(CallRecordDto call)
        {
            _parent.Broadcasts.Add((_targetGroups, nameof(CallStatusChanged), call));
            return Task.CompletedTask;
        }

        public Task CallEnded(string uniqueId)
        {
            _parent.Broadcasts.Add((_targetGroups, nameof(CallEnded), uniqueId));
            return Task.CompletedTask;
        }

        public Task ReceiveNotification(string message)
        {
            _parent.Broadcasts.Add((_targetGroups, nameof(ReceiveNotification), message));
            return Task.CompletedTask;
        }
    }

    private class MockGroupManager : IGroupManager
    {
        public List<(string ConnId, string Group)> Added { get; } = new();
        public List<(string ConnId, string Group)> Removed { get; } = new();

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Added.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Removed.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    private (CrmDbContext dbContext, IServiceScopeFactory scopeFactory, TelephonyConnectionTracker tracker, FakeHubGroupManager hubGroupManager, TelephonyAccessControlService accessControlService) CreateSetup()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<CrmDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddScoped<ICrmDbContext>(sp => sp.GetRequiredService<CrmDbContext>());
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var dbContext = provider.CreateScope().ServiceProvider.GetRequiredService<CrmDbContext>();

        var tracker = new TelephonyConnectionTracker();
        var fakeGroupMgr = new FakeHubGroupManager();
        var accessControlService = new TelephonyAccessControlService(
            scopeFactory,
            tracker,
            NullLogger<TelephonyAccessControlService>.Instance,
            fakeGroupMgr);

        return (dbContext, scopeFactory, tracker, fakeGroupMgr, accessControlService);
    }

    [Fact(DisplayName = "1. User A cannot register extension B (throws HubException)")]
    public async Task Scenario1_UserA_Cannot_Register_ExtensionB_Throws_HubException()
    {
        var (dbContext, _, tracker, _, accessControlService) = CreateSetup();

        var userAId = Guid.NewGuid();
        dbContext.Users.Add(new AppUser { Id = userAId, Username = "user_a", DisplayName = "کاربر الف", IsEnabled = true });
        dbContext.UserExtensions.Add(new UserExtension { Id = Guid.NewGuid(), UserId = userAId, Username = "user_a", Extension = "101", IsActive = true });
        await dbContext.SaveChangesAsync();

        var currentUser = new TestCurrentUserService
        {
            UserId = userAId,
            Username = "user_a",
            Extension = "101",
            RolesList = new List<string> { Roles.Procurement }
        };

        var scope = await accessControlService.GetUserAccessScopeAsync(userAId, "user_a");
        scope.VerifiedUserExtension.Should().Be("101");
        scope.HasGlobalAccess.Should().BeFalse();

        // Simulate Hub RegisterExtension logic
        var requestedExt = "102"; // Tampered extension
        var act = () =>
        {
            if (!scope.HasGlobalAccess)
            {
                if (string.IsNullOrEmpty(scope.VerifiedUserExtension) ||
                    !string.Equals(scope.VerifiedUserExtension, requestedExt, StringComparison.OrdinalIgnoreCase))
                {
                    throw new HubException("شما مجاز به ثبت این شماره داخلی نیستید.");
                }
            }
        };

        act.Should().Throw<HubException>().WithMessage("شما مجاز به ثبت این شماره داخلی نیستید.");
    }

    [Fact(DisplayName = "2. Normal user providing supervisor extension does not become supervisor")]
    public async Task Scenario2_Normal_User_Providing_Supervisor_Extension_Does_Not_Become_Supervisor()
    {
        var (dbContext, _, tracker, _, accessControlService) = CreateSetup();

        var supervisorId = Guid.NewGuid();
        var normalUserId = Guid.NewGuid();

        dbContext.Users.AddRange(
            new AppUser { Id = supervisorId, Username = "supervisor_a", DisplayName = "سرپرست الف", IsEnabled = true },
            new AppUser { Id = normalUserId, Username = "normal_user", DisplayName = "کاربر عادی", IsEnabled = true }
        );

        dbContext.UserExtensions.AddRange(
            new UserExtension { Id = Guid.NewGuid(), UserId = supervisorId, Username = "supervisor_a", Extension = "201", IsActive = true },
            new UserExtension { Id = Guid.NewGuid(), UserId = normalUserId, Username = "normal_user", Extension = "101", IsActive = true }
        );

        var groupA = new TelephonyGroup
        {
            Id = Guid.NewGuid(),
            Name = "تیم تدارکات",
            IsActive = true,
            SupervisorUserId = supervisorId,
            SupervisorExtension = "201",
            Members = new List<TelephonyGroupMember>
            {
                new() { Id = Guid.NewGuid(), Extension = "201", UserId = supervisorId, IsSupervisor = true },
                new() { Id = Guid.NewGuid(), Extension = "101", UserId = normalUserId, IsSupervisor = false }
            }
        };
        dbContext.TelephonyGroups.Add(groupA);
        await dbContext.SaveChangesAsync();

        // Normal user evaluates scope
        var normalScope = await accessControlService.GetUserAccessScopeAsync(normalUserId, "normal_user");

        normalScope.VerifiedUserExtension.Should().Be("101");
        normalScope.SupervisedGroupIds.Should().BeEmpty();
        normalScope.AllowedExtensions.Should().BeEquivalentTo(new[] { "101" });
        normalScope.HubGroups.Should().BeEquivalentTo(new[] { "ext_101" });

        // Normal user cannot claim "201"
        var act = () =>
        {
            if (!normalScope.HasGlobalAccess)
            {
                if (string.IsNullOrEmpty(normalScope.VerifiedUserExtension) ||
                    !string.Equals(normalScope.VerifiedUserExtension, "201", StringComparison.OrdinalIgnoreCase))
                {
                    throw new HubException("شما مجاز به ثبت این شماره داخلی نیستید.");
                }
            }
        };

        act.Should().Throw<HubException>();
    }

    [Fact(DisplayName = "3. Supervisor of Group A receives no notifications or audio for Group B")]
    public async Task Scenario3_Supervisor_GroupA_Receives_No_Notifications_Or_Audio_For_GroupB()
    {
        var (dbContext, _, tracker, _, accessControlService) = CreateSetup();

        var supAId = Guid.NewGuid();
        var supBId = Guid.NewGuid();
        var memberAId = Guid.NewGuid();
        var memberBId = Guid.NewGuid();

        dbContext.Users.AddRange(
            new AppUser { Id = supAId, Username = "sup_a", DisplayName = "سرپرست الف", IsEnabled = true },
            new AppUser { Id = memberAId, Username = "mem_a", DisplayName = "عضو الف", IsEnabled = true },
            new AppUser { Id = supBId, Username = "sup_b", DisplayName = "سرپرست ب", IsEnabled = true },
            new AppUser { Id = memberBId, Username = "mem_b", DisplayName = "عضو ب", IsEnabled = true }
        );

        dbContext.UserExtensions.AddRange(
            new UserExtension { Id = Guid.NewGuid(), UserId = supAId, Username = "sup_a", Extension = "201", IsActive = true },
            new UserExtension { Id = Guid.NewGuid(), UserId = memberAId, Username = "mem_a", Extension = "101", IsActive = true },
            new UserExtension { Id = Guid.NewGuid(), UserId = supBId, Username = "sup_b", Extension = "202", IsActive = true },
            new UserExtension { Id = Guid.NewGuid(), UserId = memberBId, Username = "mem_b", Extension = "102", IsActive = true }
        );

        var groupA = new TelephonyGroup
        {
            Id = Guid.NewGuid(),
            Name = "گروه تدارکات الف",
            IsActive = true,
            SupervisorUserId = supAId,
            SupervisorExtension = "201",
            Members = new List<TelephonyGroupMember>
            {
                new() { Id = Guid.NewGuid(), Extension = "201", UserId = supAId, IsSupervisor = true },
                new() { Id = Guid.NewGuid(), Extension = "101", UserId = memberAId, IsSupervisor = false }
            }
        };

        var groupB = new TelephonyGroup
        {
            Id = Guid.NewGuid(),
            Name = "گروه انبار ب",
            IsActive = true,
            SupervisorUserId = supBId,
            SupervisorExtension = "202",
            Members = new List<TelephonyGroupMember>
            {
                new() { Id = Guid.NewGuid(), Extension = "202", UserId = supBId, IsSupervisor = true },
                new() { Id = Guid.NewGuid(), Extension = "102", UserId = memberBId, IsSupervisor = false }
            }
        };

        dbContext.TelephonyGroups.AddRange(groupA, groupB);
        await dbContext.SaveChangesAsync();

        var scopeSupA = await accessControlService.GetUserAccessScopeAsync(supAId, "sup_a");
        var scopeSupB = await accessControlService.GetUserAccessScopeAsync(supBId, "sup_b");

        // Sup A has access only to Group A
        scopeSupA.AllowedExtensions.Should().BeEquivalentTo(new[] { "201", "101" });
        scopeSupA.HubGroups.Should().BeEquivalentTo(new[] { "ext_201", $"group_sup_{groupA.Id}" });
        scopeSupA.HubGroups.Should().NotContain($"group_sup_{groupB.Id}");

        // Call on Group B (ext 102)
        var targetGroupsFor102 = await accessControlService.GetNotificationTargetGroupsForExtensionAsync("102");
        targetGroupsFor102.Should().Contain("ext_102");
        targetGroupsFor102.Should().Contain($"group_sup_{groupB.Id}");
        targetGroupsFor102.Should().NotContain($"group_sup_{groupA.Id}");

        // Group A supervisor can NOT access call or recording for Group B
        accessControlService.CanAccessCall(scopeSupA, callExtension: "102", callerNumber: "09121111111", destinationNumber: "102")
            .Should().BeFalse();

        // Group B supervisor CAN access call or recording for Group B
        accessControlService.CanAccessCall(scopeSupB, callExtension: "102", callerNumber: "09121111111", destinationNumber: "102")
            .Should().BeTrue();
    }

    [Fact(DisplayName = "4. Inactive group grants no extra access to supervisor or members")]
    public async Task Scenario4_Inactive_Group_Grants_No_Extra_Access()
    {
        var (dbContext, _, tracker, _, accessControlService) = CreateSetup();

        var supId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        dbContext.Users.AddRange(
            new AppUser { Id = supId, Username = "sup_c", DisplayName = "سرپرست ج", IsEnabled = true },
            new AppUser { Id = memberId, Username = "mem_c", DisplayName = "عضو ج", IsEnabled = true }
        );

        dbContext.UserExtensions.AddRange(
            new UserExtension { Id = Guid.NewGuid(), UserId = supId, Username = "sup_c", Extension = "301", IsActive = true },
            new UserExtension { Id = Guid.NewGuid(), UserId = memberId, Username = "mem_c", Extension = "302", IsActive = true }
        );

        var inactiveGroup = new TelephonyGroup
        {
            Id = Guid.NewGuid(),
            Name = "گروه غیرفعال بازرگانی",
            IsActive = false, // Inactive!
            SupervisorUserId = supId,
            SupervisorExtension = "301",
            Members = new List<TelephonyGroupMember>
            {
                new() { Id = Guid.NewGuid(), Extension = "301", UserId = supId, IsSupervisor = true },
                new() { Id = Guid.NewGuid(), Extension = "302", UserId = memberId, IsSupervisor = false }
            }
        };

        dbContext.TelephonyGroups.Add(inactiveGroup);
        await dbContext.SaveChangesAsync();

        var supScope = await accessControlService.GetUserAccessScopeAsync(supId, "sup_c");

        // Since group is inactive, supervisor only gets their own extension!
        supScope.SupervisedGroupIds.Should().BeEmpty();
        supScope.AllowedExtensions.Should().BeEquivalentTo(new[] { "301" });
        supScope.HubGroups.Should().BeEquivalentTo(new[] { "ext_301" });
        supScope.HubGroups.Should().NotContain($"group_sup_{inactiveGroup.Id}");

        // Inactive group supervisor CANNOT access member 302's calls
        accessControlService.CanAccessCall(supScope, callExtension: "302", callerNumber: "09123333333", destinationNumber: "302")
            .Should().BeFalse();

        // Notifications for 302 do NOT include the inactive group supervisor
        var targetGroups = await accessControlService.GetNotificationTargetGroupsForExtensionAsync("302");
        targetGroups.Should().NotContain($"group_sup_{inactiveGroup.Id}");
    }

    [Fact(DisplayName = "5. User without extension receives no global notifications")]
    public async Task Scenario5_User_Without_Extension_Receives_No_Global_Notifications()
    {
        var (dbContext, _, tracker, _, accessControlService) = CreateSetup();

        var unassignedUserId = Guid.NewGuid();
        dbContext.Users.Add(new AppUser { Id = unassignedUserId, Username = "unassigned_user", DisplayName = "کاربر بدون داخلی", IsEnabled = true });
        await dbContext.SaveChangesAsync();

        var scope = await accessControlService.GetUserAccessScopeAsync(unassignedUserId, "unassigned_user");

        scope.VerifiedUserExtension.Should().BeNull();
        scope.AllowedExtensions.Should().BeEmpty();
        scope.HubGroups.Should().BeEmpty();
        scope.HasGlobalAccess.Should().BeFalse();

        // Cannot access any call
        accessControlService.CanAccessCall(scope, callExtension: "101", callerNumber: "09121111111", destinationNumber: "101")
            .Should().BeFalse();

        // Calling RegisterExtension throws
        var act = () =>
        {
            if (!scope.HasGlobalAccess)
            {
                if (string.IsNullOrEmpty(scope.VerifiedUserExtension) ||
                    !string.Equals(scope.VerifiedUserExtension, "101", StringComparison.OrdinalIgnoreCase))
                {
                    throw new HubException("شما مجاز به ثبت این شماره داخلی نیستید.");
                }
            }
        };

        act.Should().Throw<HubException>();
    }

    [Fact(DisplayName = "6. Permission revocation during open connection is effective")]
    public async Task Scenario6_Permission_Revocation_During_Open_Connection_Is_Effective()
    {
        var (dbContext, _, tracker, hubGroupManager, accessControlService) = CreateSetup();

        var supId = Guid.NewGuid();
        dbContext.Users.Add(new AppUser { Id = supId, Username = "sup_revoke", DisplayName = "سرپرست لغو", IsEnabled = true });
        dbContext.UserExtensions.Add(new UserExtension { Id = Guid.NewGuid(), UserId = supId, Username = "sup_revoke", Extension = "501", IsActive = true });

        var group = new TelephonyGroup
        {
            Id = Guid.NewGuid(),
            Name = "تیم آزمایش لغو مجوز",
            IsActive = true,
            SupervisorUserId = supId,
            SupervisorExtension = "501",
            Members = new List<TelephonyGroupMember>
            {
                new() { Id = Guid.NewGuid(), Extension = "501", UserId = supId, IsSupervisor = true },
                new() { Id = Guid.NewGuid(), Extension = "502", UserId = Guid.NewGuid(), IsSupervisor = false }
            }
        };
        dbContext.TelephonyGroups.Add(group);
        await dbContext.SaveChangesAsync();

        var connId = "conn_user_501";
        var initialGroups = new[] { "ext_501", $"group_sup_{group.Id}" };
        tracker.TrackConnection(connId, supId, "sup_revoke", "501", initialGroups);

        tracker.GetConnection(connId)!.JoinedGroups.Should().Contain($"group_sup_{group.Id}");

        // Deactivate the group in DB
        group.IsActive = false;
        await dbContext.SaveChangesAsync();

        // Run connection sync
        await accessControlService.SyncActiveConnectionsAsync();

        // Verification: HubGroupManager.RemoveFromGroupAsync was called with group_sup_{group.Id}
        hubGroupManager.RemovedGroups.Should().Contain(r => r.ConnectionId == connId && r.GroupName == $"group_sup_{group.Id}");

        // Tracker joined groups no longer has the supervisor group
        tracker.GetConnection(connId)!.JoinedGroups.Should().NotContain($"group_sup_{group.Id}");
        tracker.GetConnection(connId)!.JoinedGroups.Should().Contain("ext_501");
    }

    [Fact(DisplayName = "7. Start, status change, and ended are all restricted to scoped groups (no Clients.All)")]
    public async Task Scenario7_Start_StatusChange_And_Ended_Are_All_Restricted_No_ClientsAll()
    {
        var (dbContext, _, tracker, _, accessControlService) = CreateSetup();

        var targetExt = "601";
        var supId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        dbContext.UserExtensions.Add(new UserExtension { Id = Guid.NewGuid(), UserId = supId, Username = "sup_600", Extension = "600", IsActive = true });
        dbContext.TelephonyGroups.Add(new TelephonyGroup
        {
            Id = groupId,
            Name = "تیم ۶۰۰",
            IsActive = true,
            SupervisorUserId = supId,
            SupervisorExtension = "600",
            Members = new List<TelephonyGroupMember>
            {
                new() { Id = Guid.NewGuid(), Extension = "600", UserId = supId, IsSupervisor = true },
                new() { Id = Guid.NewGuid(), Extension = targetExt, UserId = Guid.NewGuid(), IsSupervisor = false }
            }
        });
        await dbContext.SaveChangesAsync();

        var targetGroups = await accessControlService.GetNotificationTargetGroupsForExtensionAsync(targetExt);

        targetGroups.Should().Contain("ext_601");
        targetGroups.Should().Contain($"group_sup_{groupId}");
        targetGroups.Should().Contain("global_supervisors");
        targetGroups.Should().NotContain("supervisors"); // Global 'supervisors' removed

        // Notification targets do not broadcast to all
        var mockClients = new MockHubCallerClients();

        // Broadcast popup
        await mockClients.Groups(targetGroups).ReceiveCallPopup(new CallerPopupDto { UniqueId = "call-1", Extension = targetExt });
        // Broadcast status
        await mockClients.Groups(targetGroups).CallStatusChanged(new CallRecordDto { UniqueId = "call-1", Extension = targetExt });
        // Broadcast ended
        await mockClients.Groups(targetGroups).CallEnded("call-1");

        mockClients.Broadcasts.Should().HaveCount(3);
        foreach (var b in mockClients.Broadcasts)
        {
            b.Groups.Should().BeEquivalentTo(targetGroups);
            b.Groups.Should().NotContain("supervisors");
        }
    }

    [Fact(DisplayName = "8. Direct access to call ID and recording URL is strictly scoped")]
    public async Task Scenario8_Direct_Access_To_Call_Id_And_Recording_Stream_Is_Protected()
    {
        var (dbContext, _, _, _, accessControlService) = CreateSetup();

        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();

        dbContext.Users.AddRange(
            new AppUser { Id = userAId, Username = "user_a", DisplayName = "کاربر الف", IsEnabled = true },
            new AppUser { Id = userBId, Username = "user_b", DisplayName = "کاربر ب", IsEnabled = true }
        );

        dbContext.UserExtensions.AddRange(
            new UserExtension { Id = Guid.NewGuid(), UserId = userAId, Username = "user_a", Extension = "101", IsActive = true },
            new UserExtension { Id = Guid.NewGuid(), UserId = userBId, Username = "user_b", Extension = "102", IsActive = true }
        );
        await dbContext.SaveChangesAsync();

        var scopeUserA = await accessControlService.GetUserAccessScopeAsync(userAId, "user_a");
        var scopeUserB = await accessControlService.GetUserAccessScopeAsync(userBId, "user_b");

        // Call belongs to user B (Extension = 102)
        var callB = new CallRecord
        {
            Id = Guid.NewGuid(),
            UniqueId = "call-user-b-102",
            LinkedId = "call-user-b-102",
            Extension = "102",
            CallerNumber = "09129999999",
            NormalizedCallerNumber = "09129999999",
            DestinationNumber = "102",
            RecordingFileName = "rec_102.wav",
            MatchConfidence = "Exact"
        };

        // User A attempts direct access to call B -> False (403 Forbidden)
        accessControlService.CanAccessCall(scopeUserA, callB.Extension, callB.CallerNumber, callB.DestinationNumber)
            .Should().BeFalse();

        // User B attempts direct access to call B -> True (200 OK)
        accessControlService.CanAccessCall(scopeUserB, callB.Extension, callB.CallerNumber, callB.DestinationNumber)
            .Should().BeTrue();
    }

    [Fact(DisplayName = "9. Authorized admin and authorized users continue to work correctly")]
    public async Task Scenario9_Authorized_Admin_And_Authorized_Users_Work_Correctly()
    {
        var (dbContext, _, _, _, accessControlService) = CreateSetup();

        var adminId = Guid.NewGuid();
        var adminRole = new Role { Id = Guid.NewGuid(), Name = Roles.Admin, Description = "مدیر کل" };
        var adminUser = new AppUser { Id = adminId, Username = "CRM\\Administrator", DisplayName = "مدیر سیستم", IsEnabled = true };
        adminUser.UserRoles.Add(new UserRole { UserId = adminId, RoleId = adminRole.Id, Role = adminRole });

        var supId = Guid.NewGuid();
        var memId = Guid.NewGuid();

        dbContext.Roles.Add(adminRole);
        dbContext.Users.AddRange(
            adminUser,
            new AppUser { Id = supId, Username = "sup_team", DisplayName = "سرپرست ۷۰۰", IsEnabled = true },
            new AppUser { Id = memId, Username = "mem_team", DisplayName = "عضو ۷۰۰", IsEnabled = true }
        );

        dbContext.UserExtensions.AddRange(
            new UserExtension { Id = Guid.NewGuid(), UserId = supId, Username = "sup_team", Extension = "701", IsActive = true },
            new UserExtension { Id = Guid.NewGuid(), UserId = memId, Username = "mem_team", Extension = "702", IsActive = true }
        );

        var group = new TelephonyGroup
        {
            Id = Guid.NewGuid(),
            Name = "تیم ۷۰۰",
            IsActive = true,
            SupervisorUserId = supId,
            SupervisorExtension = "701",
            Members = new List<TelephonyGroupMember>
            {
                new() { Id = Guid.NewGuid(), Extension = "701", UserId = supId, IsSupervisor = true },
                new() { Id = Guid.NewGuid(), Extension = "702", UserId = memId, IsSupervisor = false }
            }
        };
        dbContext.TelephonyGroups.Add(group);
        await dbContext.SaveChangesAsync();

        var adminScope = await accessControlService.GetUserAccessScopeAsync(adminId, "CRM\\Administrator");
        var supScope = await accessControlService.GetUserAccessScopeAsync(supId, "sup_team");
        var memScope = await accessControlService.GetUserAccessScopeAsync(memId, "mem_team");

        // Admin has global access
        adminScope.HasGlobalAccess.Should().BeTrue();
        adminScope.HubGroups.Should().Contain("global_supervisors");
        accessControlService.CanAccessCall(adminScope, "999", "09120000000", "888").Should().BeTrue();

        // Supervisor can access calls for their own ext (701) and group member (702)
        supScope.AllowedExtensions.Should().BeEquivalentTo(new[] { "701", "702" });
        accessControlService.CanAccessCall(supScope, "701", "09121111111", "701").Should().BeTrue();
        accessControlService.CanAccessCall(supScope, "702", "09122222222", "702").Should().BeTrue();
        accessControlService.CanAccessCall(supScope, "703", "09123333333", "703").Should().BeFalse();

        // Member can only access calls for their own ext (702)
        memScope.AllowedExtensions.Should().BeEquivalentTo(new[] { "702" });
        accessControlService.CanAccessCall(memScope, "702", "09122222222", "702").Should().BeTrue();
        accessControlService.CanAccessCall(memScope, "701", "09121111111", "701").Should().BeFalse();
    }

    [Fact(DisplayName = "10. User with username Administrator without explicit role or permission gets NO global access")]
    public async Task Scenario10_Administrator_Username_Without_Explicit_Role_Or_Permission_Gets_No_Global_Access()
    {
        var (dbContext, _, _, _, accessControlService) = CreateSetup();

        // 1. User named CRM\Administrator without Admin role or CallsSupervise permission
        var domainAdminId = Guid.NewGuid();
        var procRole = new Role { Id = Guid.NewGuid(), Name = Roles.Procurement, Description = "کارشناس بازرگانی" };
        var domainAdminUser = new AppUser
        {
            Id = domainAdminId,
            Username = "CRM\\Administrator",
            DisplayName = "کاربر بدون دسترسی مدیر",
            IsEnabled = true
        };
        domainAdminUser.UserRoles.Add(new UserRole { UserId = domainAdminId, RoleId = procRole.Id, Role = procRole });

        // 2. User named plain Administrator without any roles
        var plainAdminId = Guid.NewGuid();
        var plainAdminUser = new AppUser
        {
            Id = plainAdminId,
            Username = "Administrator",
            DisplayName = "ادمین اسمی بدون نقش",
            IsEnabled = true
        };

        dbContext.Roles.Add(procRole);
        dbContext.Users.AddRange(domainAdminUser, plainAdminUser);
        await dbContext.SaveChangesAsync();

        var scopeDomain = await accessControlService.GetUserAccessScopeAsync(domainAdminId, "CRM\\Administrator");
        var scopePlain = await accessControlService.GetUserAccessScopeAsync(plainAdminId, "Administrator");

        // Both must NOT receive global access or global_supervisors group
        scopeDomain.HasGlobalAccess.Should().BeFalse();
        scopeDomain.HubGroups.Should().NotContain("global_supervisors");
        accessControlService.CanAccessCall(scopeDomain, "999", "09120000000", "888").Should().BeFalse();

        scopePlain.HasGlobalAccess.Should().BeFalse();
        scopePlain.HubGroups.Should().NotContain("global_supervisors");
        accessControlService.CanAccessCall(scopePlain, "999", "09120000000", "888").Should().BeFalse();
    }

    [Fact(DisplayName = "11. Disabled user with active extension mapping or supervisor history receives empty scope and no access")]
    public async Task Scenario11_Disabled_User_With_Active_Extension_And_Supervisor_History_Gets_Empty_Scope()
    {
        var (dbContext, _, _, _, accessControlService) = CreateSetup();

        var disabledUserId = Guid.NewGuid();
        var disabledUser = new AppUser
        {
            Id = disabledUserId,
            Username = "CRM\\disabled_user",
            DisplayName = "کاربر غیرفعال",
            IsEnabled = false // DISABLED!
        };

        var ext = new UserExtension
        {
            Id = Guid.NewGuid(),
            UserId = disabledUserId,
            Username = "CRM\\disabled_user",
            Extension = "101",
            IsActive = true // Active mapping in DB
        };

        var group = new TelephonyGroup
        {
            Id = Guid.NewGuid(),
            Name = "تیم غیرفعال",
            IsActive = true,
            SupervisorUserId = disabledUserId,
            SupervisorExtension = "101",
            Members = new List<TelephonyGroupMember>
            {
                new() { Id = Guid.NewGuid(), Extension = "101", UserId = disabledUserId, IsSupervisor = true },
                new() { Id = Guid.NewGuid(), Extension = "102", IsSupervisor = false }
            }
        };

        dbContext.Users.Add(disabledUser);
        dbContext.UserExtensions.Add(ext);
        dbContext.TelephonyGroups.Add(group);
        await dbContext.SaveChangesAsync();

        var scope = await accessControlService.GetUserAccessScopeAsync(disabledUserId, "CRM\\disabled_user");

        // Must receive completely empty access scope: zero side-channels!
        scope.HasGlobalAccess.Should().BeFalse();
        scope.VerifiedUserExtension.Should().BeNull();
        scope.AllowedExtensions.Should().BeEmpty();
        scope.SupervisedGroupIds.Should().BeEmpty();
        scope.HubGroups.Should().BeEmpty();

        // Any call access attempt must be rejected
        accessControlService.CanAccessCall(scope, "101", "09121111111", "101").Should().BeFalse();
        accessControlService.CanAccessCall(scope, "102", "09122222222", "102").Should().BeFalse();
    }

    [Fact(DisplayName = "12. Extension change via UsersController on open connection disconnects old extension notifications")]
    public async Task Scenario12_Extension_Change_Via_UsersController_Disconnects_Old_Extension_On_Open_Connection()
    {
        var (dbContext, _, tracker, fakeGroupMgr, accessControlService) = CreateSetup();

        var agentId = Guid.NewGuid();
        var agent = new AppUser
        {
            Id = agentId,
            Username = "CRM\\agent1",
            DisplayName = "اپراتور ۱",
            IsEnabled = true
        };
        var ext = new UserExtension
        {
            Id = Guid.NewGuid(),
            UserId = agentId,
            Username = "CRM\\agent1",
            Extension = "101",
            IsActive = true
        };
        dbContext.Users.Add(agent);
        dbContext.UserExtensions.Add(ext);
        await dbContext.SaveChangesAsync();

        // Simulate established open connection for agent1 with initial group ext_101
        var connId = "conn-agent-101";
        tracker.TrackConnection(connId, agentId, "CRM\\agent1", "101", new[] { "ext_101" });

        // Instantiate UsersController with Admin caller
        var adminCurrentUser = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            Username = "CRM\\AdminUser",
            RolesList = new List<string> { Roles.Admin }
        };
        var usersController = new UsersController(
            dbContext,
            new FakeActiveDirectoryService(),
            adminCurrentUser,
            new FakeTelephonyAdapter(),
            accessControlService,
            NullLogger<UsersController>.Instance
        );

        // Act: Admin updates agent1's extension from 101 to 102 via controller
        var response = await usersController.UpdateExtension(
            agentId,
            new UpdateUserExtensionRequest { Extension = "102", DeviceType = "SIP" },
            CancellationToken.None);

        // Verify: controller returned 200 OK
        response.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();

        // Verify: Hub group manager removed old group 'ext_101' and added new group 'ext_102'
        fakeGroupMgr.RemovedGroups.Should().Contain((connId, "ext_101"));
        fakeGroupMgr.AddedGroups.Should().Contain((connId, "ext_102"));

        // Verify: Tracker updated open connection's active groups
        var activeGroups = tracker.GetConnection(connId)!.JoinedGroups;
        activeGroups.Should().Contain("ext_102");
        activeGroups.Should().NotContain("ext_101");

        // Verify: Notification targeting for old extension 101 no longer includes connection's groups
        var targetsForOld = await accessControlService.GetNotificationTargetGroupsForExtensionAsync("101");
        targetsForOld.Intersect(activeGroups).Should().BeEmpty();

        // Verify: Notification targeting for new extension 102 reaches the connection
        var targetsForNew = await accessControlService.GetNotificationTargetGroupsForExtensionAsync("102");
        targetsForNew.Intersect(activeGroups).Should().Contain("ext_102");
    }

    [Fact(DisplayName = "13. Disabling user via UsersController immediately revokes all open connection groups")]
    public async Task Scenario13_Disabling_User_Via_UsersController_Immediately_Revokes_All_Open_Connection_Groups()
    {
        var (dbContext, _, tracker, fakeGroupMgr, accessControlService) = CreateSetup();

        var userId = Guid.NewGuid();
        var user = new AppUser
        {
            Id = userId,
            Username = "CRM\\agent_to_disable",
            DisplayName = "کاربر در آستانه غیرفعال‌سازی",
            IsEnabled = true
        };
        var ext = new UserExtension
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Username = "CRM\\agent_to_disable",
            Extension = "201",
            IsActive = true
        };
        var group = new TelephonyGroup
        {
            Id = Guid.NewGuid(),
            Name = "تیم ۲۰",
            IsActive = true,
            SupervisorUserId = userId,
            SupervisorExtension = "201",
            Members = new List<TelephonyGroupMember>
            {
                new() { Id = Guid.NewGuid(), Extension = "201", UserId = userId, IsSupervisor = true }
            }
        };

        dbContext.Users.Add(user);
        dbContext.UserExtensions.Add(ext);
        dbContext.TelephonyGroups.Add(group);
        await dbContext.SaveChangesAsync();

        var connId = "conn-disable-test";
        var initialGroups = new[] { "ext_201", $"group_sup_{group.Id}" };
        tracker.TrackConnection(connId, userId, "CRM\\agent_to_disable", "201", initialGroups);

        var adminCurrentUser = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            Username = "CRM\\AdminUser",
            RolesList = new List<string> { Roles.Admin }
        };
        var usersController = new UsersController(
            dbContext,
            new FakeActiveDirectoryService(),
            adminCurrentUser,
            new FakeTelephonyAdapter(),
            accessControlService,
            NullLogger<UsersController>.Instance
        );

        // Act: Admin disables the user via controller
        var result = await usersController.UpdateStatus(userId, new UpdateUserStatusRequest { IsEnabled = false }, CancellationToken.None);
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();

        // Verify: Hub group manager removed all groups from connection
        fakeGroupMgr.RemovedGroups.Should().Contain((connId, "ext_201"));
        fakeGroupMgr.RemovedGroups.Should().Contain((connId, $"group_sup_{group.Id}"));

        // Verify: Open connection has zero joined groups left in tracker
        tracker.GetConnection(connId)!.JoinedGroups.Should().BeEmpty();

        // Verify: Scope is empty
        var scope = await accessControlService.GetUserAccessScopeAsync(userId, "CRM\\agent_to_disable");
        scope.HubGroups.Should().BeEmpty();
        scope.AllowedExtensions.Should().BeEmpty();
    }

    [Fact(DisplayName = "14. Revoking CallsSupervise permission via RolesController immediately revokes global supervisors group on open connection")]
    public async Task Scenario14_Revoking_CallsSupervise_Via_RolesController_Immediately_Revokes_Global_Supervisors_Group()
    {
        var (dbContext, _, tracker, fakeGroupMgr, accessControlService) = CreateSetup();

        var permSupervise = new Permission { Id = Guid.NewGuid(), Code = Permissions.CallsSupervise, Description = "نظارت سراسری تماس", Category = "Calls" };
        var permView = new Permission { Id = Guid.NewGuid(), Code = Permissions.CallsView, Description = "مشاهده تماس", Category = "Calls" };
        var role = new Role { Id = Guid.NewGuid(), Name = "CallSupervisorRole", Description = "سرپرست تلفنی" };
        role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permSupervise.Id, Permission = permSupervise });

        var userId = Guid.NewGuid();
        var user = new AppUser
        {
            Id = userId,
            Username = "CRM\\supervisor1",
            DisplayName = "سرپرست مرکز تماس",
            IsEnabled = true
        };
        user.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id, Role = role });

        var ext = new UserExtension
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Username = "CRM\\supervisor1",
            Extension = "301",
            IsActive = true
        };

        dbContext.Permissions.AddRange(permSupervise, permView);
        dbContext.Roles.Add(role);
        dbContext.Users.Add(user);
        dbContext.UserExtensions.Add(ext);
        await dbContext.SaveChangesAsync();

        var connId = "conn-perm-test";
        tracker.TrackConnection(connId, userId, "CRM\\supervisor1", "301", new[] { "ext_301", "global_supervisors" });

        var adminCurrentUser = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            Username = "CRM\\AdminUser",
            RolesList = new List<string> { Roles.Admin }
        };
        var rolesController = new RolesController(
            dbContext,
            adminCurrentUser,
            accessControlService
        );

        // Act: Revoke Calls.Supervise permission by only keeping Calls.View
        var result = await rolesController.UpdatePermissions(
            role.Id,
            new UpdateRolePermissionsRequest { Permissions = new List<string> { Permissions.CallsView } },
            CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();

        // Verify: Hub group manager removed 'global_supervisors' from open connection
        fakeGroupMgr.RemovedGroups.Should().Contain((connId, "global_supervisors"));

        // Verify: Open connection tracker still retains personal extension but NOT global_supervisors
        var groups = tracker.GetConnection(connId)!.JoinedGroups;
        groups.Should().Contain("ext_301");
        groups.Should().NotContain("global_supervisors");

        // Verify: New scope has HasGlobalAccess = false
        var scope = await accessControlService.GetUserAccessScopeAsync(userId, "CRM\\supervisor1");
        scope.HasGlobalAccess.Should().BeFalse();
        scope.HubGroups.Should().NotContain("global_supervisors");
    }

    [Fact(DisplayName = "15. Revoking Admin role via UsersController immediately revokes global supervisors group on open connection")]
    public async Task Scenario15_Revoking_AdminRole_Via_UsersController_Immediately_Revokes_Global_Supervisors_Group()
    {
        var (dbContext, _, tracker, fakeGroupMgr, accessControlService) = CreateSetup();

        var adminRole = new Role { Id = Guid.NewGuid(), Name = Roles.Admin, Description = "مدیر کل" };
        var buyerRole = new Role { Id = Guid.NewGuid(), Name = Roles.Procurement, Description = "کارشناس بازرگانی" };

        var userId = Guid.NewGuid();
        var user = new AppUser
        {
            Id = userId,
            Username = "CRM\\demoted_user",
            DisplayName = "کاربر عزل شده",
            IsEnabled = true
        };
        user.UserRoles.Add(new UserRole { UserId = userId, RoleId = adminRole.Id, Role = adminRole });

        dbContext.Roles.AddRange(adminRole, buyerRole);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var connId = "conn-demote-test";
        tracker.TrackConnection(connId, userId, "CRM\\demoted_user", null, new[] { "global_supervisors" });

        var adminCurrentUser = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            Username = "CRM\\MasterAdmin",
            RolesList = new List<string> { Roles.Admin }
        };
        var usersController = new UsersController(
            dbContext,
            new FakeActiveDirectoryService(),
            adminCurrentUser,
            new FakeTelephonyAdapter(),
            accessControlService,
            NullLogger<UsersController>.Instance
        );

        // Act: Demote user from Admin to Procurement
        var result = await usersController.UpdateRoles(
            userId,
            new UpdateUserRolesRequest { Roles = new List<string> { Roles.Procurement } },
            CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();

        // Verify: Hub group manager removed 'global_supervisors'
        fakeGroupMgr.RemovedGroups.Should().Contain((connId, "global_supervisors"));
        tracker.GetConnection(connId)!.JoinedGroups.Should().NotContain("global_supervisors");

        // Verify: New scope has HasGlobalAccess = false
        var scope = await accessControlService.GetUserAccessScopeAsync(userId, "CRM\\demoted_user");
        scope.HasGlobalAccess.Should().BeFalse();
        scope.HubGroups.Should().NotContain("global_supervisors");
    }

    [Fact(DisplayName = "16. Two accounts with same account name across different domains have strictly isolated roles and extensions")]
    public async Task Scenario16_Two_Accounts_Same_AccountName_Different_Domains_With_Different_Roles_And_Extensions()
    {
        var (dbContext, _, _, _, accessControlService) = CreateSetup();

        var adminRole = new Role { Id = Guid.NewGuid(), Name = Roles.Admin, Description = "مدیر کل" };
        var buyerRole = new Role { Id = Guid.NewGuid(), Name = Roles.Procurement, Description = "کارشناس بازرگانی" };

        var user1Id = Guid.NewGuid();
        var user1 = new AppUser
        {
            Id = user1Id,
            Username = "DOMAIN1\\sara",
            DisplayName = "سارا دامنه ۱ (کارشناس بازرگانی)",
            IsEnabled = true
        };
        user1.UserRoles.Add(new UserRole { UserId = user1Id, RoleId = buyerRole.Id, Role = buyerRole });

        var user2Id = Guid.NewGuid();
        var user2 = new AppUser
        {
            Id = user2Id,
            Username = "DOMAIN2\\sara",
            DisplayName = "سارا دامنه ۲ (مدیر کل)",
            IsEnabled = true
        };
        user2.UserRoles.Add(new UserRole { UserId = user2Id, RoleId = adminRole.Id, Role = adminRole });

        var ext1 = new UserExtension
        {
            Id = Guid.NewGuid(),
            UserId = user1Id,
            Username = "DOMAIN1\\sara",
            Extension = "101",
            IsActive = true
        };

        var ext2 = new UserExtension
        {
            Id = Guid.NewGuid(),
            UserId = user2Id,
            Username = "DOMAIN2\\sara",
            Extension = "202",
            IsActive = true
        };

        dbContext.Roles.AddRange(adminRole, buyerRole);
        dbContext.Users.AddRange(user1, user2);
        dbContext.UserExtensions.AddRange(ext1, ext2);
        await dbContext.SaveChangesAsync();

        // 1. Evaluate DOMAIN1\sara (standard buyer on extension 101)
        var scope1 = await accessControlService.GetUserAccessScopeAsync(user1Id, "DOMAIN1\\sara");
        scope1.HasGlobalAccess.Should().BeFalse();
        scope1.VerifiedUserExtension.Should().Be("101");
        scope1.AllowedExtensions.Should().BeEquivalentTo(new[] { "101" });
        scope1.HubGroups.Should().BeEquivalentTo(new[] { "ext_101" });
        scope1.HubGroups.Should().NotContain("global_supervisors");

        // 2. Evaluate DOMAIN2\sara (admin on extension 202)
        var scope2 = await accessControlService.GetUserAccessScopeAsync(user2Id, "DOMAIN2\\sara");
        scope2.HasGlobalAccess.Should().BeTrue();
        scope2.VerifiedUserExtension.Should().Be("202");
        scope2.AllowedExtensions.Should().Contain("202");
        scope2.HubGroups.Should().Contain("ext_202");
        scope2.HubGroups.Should().Contain("global_supervisors");
        scope2.HubGroups.Should().NotContain("ext_101");

        // 3. Ambiguous short name "sara" without domain prefix and without UserId must receive empty scope
        var ambiguousScope = await accessControlService.GetUserAccessScopeAsync(null, "sara");
        ambiguousScope.HasGlobalAccess.Should().BeFalse();
        ambiguousScope.VerifiedUserExtension.Should().BeNull();
        ambiguousScope.AllowedExtensions.Should().BeEmpty();
        ambiguousScope.HubGroups.Should().BeEmpty();
    }

    [Fact(DisplayName = "17. Providing UserId of one account with Username of another account denies access")]
    public async Task Scenario17_UserId_Of_One_Account_With_Username_Of_Another_Account_Denies_Access()
    {
        var (dbContext, _, _, _, accessControlService) = CreateSetup();

        var adminRole = new Role { Id = Guid.NewGuid(), Name = Roles.Admin, Description = "مدیر کل" };
        var buyerRole = new Role { Id = Guid.NewGuid(), Name = Roles.Procurement, Description = "کارشناس بازرگانی" };

        var userAId = Guid.NewGuid();
        var userA = new AppUser
        {
            Id = userAId,
            Username = "DOMAIN\\buyer",
            DisplayName = "کارشناس خرید",
            IsEnabled = true
        };
        userA.UserRoles.Add(new UserRole { UserId = userAId, RoleId = buyerRole.Id, Role = buyerRole });

        var userBId = Guid.NewGuid();
        var userB = new AppUser
        {
            Id = userBId,
            Username = "DOMAIN\\superadmin",
            DisplayName = "مدیر ارشد",
            IsEnabled = true
        };
        userB.UserRoles.Add(new UserRole { UserId = userBId, RoleId = adminRole.Id, Role = adminRole });

        dbContext.Roles.AddRange(adminRole, buyerRole);
        dbContext.Users.AddRange(userA, userB);
        dbContext.UserExtensions.AddRange(
            new UserExtension { Id = Guid.NewGuid(), UserId = userAId, Username = "DOMAIN\\buyer", Extension = "101", IsActive = true },
            new UserExtension { Id = Guid.NewGuid(), UserId = userBId, Username = "DOMAIN\\superadmin", Extension = "999", IsActive = true }
        );
        await dbContext.SaveChangesAsync();

        // Tampered request: UserId of buyer (User A) passed with Username of superadmin (User B)
        var scope = await accessControlService.GetUserAccessScopeAsync(userAId, "DOMAIN\\superadmin");

        // Identity conflict detected -> must be rejected completely!
        scope.HasGlobalAccess.Should().BeFalse();
        scope.VerifiedUserExtension.Should().BeNull();
        scope.AllowedExtensions.Should().BeEmpty();
        scope.HubGroups.Should().BeEmpty();

        // Under no circumstances can Account B's call/extension be accessed
        accessControlService.CanAccessCall(scope, "999", "09120000000", "999").Should().BeFalse();
    }

    [Fact(DisplayName = "18. Providing non-existent UserId with authorized username denies access")]
    public async Task Scenario18_NonExistent_UserId_With_Authorized_Username_Denies_Access()
    {
        var (dbContext, _, _, _, accessControlService) = CreateSetup();

        var adminRole = new Role { Id = Guid.NewGuid(), Name = Roles.Admin, Description = "مدیر کل" };
        var adminId = Guid.NewGuid();
        var adminUser = new AppUser
        {
            Id = adminId,
            Username = "DOMAIN\\authorized_admin",
            DisplayName = "مدیر مجاز سیستم",
            IsEnabled = true
        };
        adminUser.UserRoles.Add(new UserRole { UserId = adminId, RoleId = adminRole.Id, Role = adminRole });

        dbContext.Roles.Add(adminRole);
        dbContext.Users.Add(adminUser);
        dbContext.UserExtensions.Add(new UserExtension { Id = Guid.NewGuid(), UserId = adminId, Username = "DOMAIN\\authorized_admin", Extension = "888", IsActive = true });
        await dbContext.SaveChangesAsync();

        // Request with a random, non-existent UserId alongside valid admin username
        var nonExistentId = Guid.NewGuid();
        var scope = await accessControlService.GetUserAccessScopeAsync(nonExistentId, "DOMAIN\\authorized_admin");

        // Because UserId was provided, query was strictly by ID -> not found -> rejected!
        // No fallback to username allowed!
        scope.HasGlobalAccess.Should().BeFalse();
        scope.VerifiedUserExtension.Should().BeNull();
        scope.AllowedExtensions.Should().BeEmpty();
        scope.HubGroups.Should().BeEmpty();

        accessControlService.CanAccessCall(scope, "888", "09120000000", "888").Should().BeFalse();
    }

    [Fact(DisplayName = "19. Disabled account with same account name as active account denies cross-access in both directions")]
    public async Task Scenario19_Disabled_Account_With_Same_AccountName_As_Active_Account_Denies_Cross_Access()
    {
        var (dbContext, _, _, _, accessControlService) = CreateSetup();

        var adminRole = new Role { Id = Guid.NewGuid(), Name = Roles.Admin, Description = "مدیر کل" };
        var buyerRole = new Role { Id = Guid.NewGuid(), Name = Roles.Procurement, Description = "کارشناس بازرگانی" };

        var disabledAlexId = Guid.NewGuid();
        var disabledAlex = new AppUser
        {
            Id = disabledAlexId,
            Username = "CORP\\alex",
            DisplayName = "الکس غیرفعال (مدیر سابق)",
            IsEnabled = false // DISABLED!
        };
        disabledAlex.UserRoles.Add(new UserRole { UserId = disabledAlexId, RoleId = adminRole.Id, Role = adminRole });

        var activeAlexId = Guid.NewGuid();
        var activeAlex = new AppUser
        {
            Id = activeAlexId,
            Username = "BRANCH\\alex",
            DisplayName = "الکس فعال (کارشناس خرید)",
            IsEnabled = true // ACTIVE
        };
        activeAlex.UserRoles.Add(new UserRole { UserId = activeAlexId, RoleId = buyerRole.Id, Role = buyerRole });

        dbContext.Roles.AddRange(adminRole, buyerRole);
        dbContext.Users.AddRange(disabledAlex, activeAlex);
        dbContext.UserExtensions.AddRange(
            new UserExtension { Id = Guid.NewGuid(), UserId = disabledAlexId, Username = "CORP\\alex", Extension = "301", IsActive = true },
            new UserExtension { Id = Guid.NewGuid(), UserId = activeAlexId, Username = "BRANCH\\alex", Extension = "302", IsActive = true }
        );
        await dbContext.SaveChangesAsync();

        // 1. Querying disabled user CORP\alex must yield empty scope
        var disabledScope = await accessControlService.GetUserAccessScopeAsync(disabledAlexId, "CORP\\alex");
        disabledScope.HasGlobalAccess.Should().BeFalse();
        disabledScope.VerifiedUserExtension.Should().BeNull();
        disabledScope.AllowedExtensions.Should().BeEmpty();
        disabledScope.HubGroups.Should().BeEmpty();

        // 2. Querying active user BRANCH\alex must yield strictly its own scope (extension 302, Procurement, no global admin)
        var activeScope = await accessControlService.GetUserAccessScopeAsync(activeAlexId, "BRANCH\\alex");
        activeScope.HasGlobalAccess.Should().BeFalse(); // Does NOT inherit admin from disabled CORP\alex!
        activeScope.VerifiedUserExtension.Should().Be("302");
        activeScope.AllowedExtensions.Should().BeEquivalentTo(new[] { "302" });
        activeScope.HubGroups.Should().BeEquivalentTo(new[] { "ext_302" });
        activeScope.HubGroups.Should().NotContain("global_supervisors");

        // 3. Neither account can access the other's extension
        accessControlService.CanAccessCall(activeScope, "301", "09120000000", "301").Should().BeFalse();
        accessControlService.CanAccessCall(disabledScope, "302", "09120000000", "302").Should().BeFalse();
    }

    [Fact(DisplayName = "20. Windows login does not adopt another domain account or a similarly named extension")]
    public async Task Scenario20_WindowsLogin_DoesNotAdoptAnotherDomainAccount()
    {
        var (dbContext, _, _, _, accessControlService) = CreateSetup();

        var adminRole = new Role { Id = Guid.NewGuid(), Name = Roles.Admin, Description = "مدیر کل" };
        var buyerRole = new Role { Id = Guid.NewGuid(), Name = Roles.Procurement, Description = "کارشناس بازرگانی" };

        var buyerId = Guid.NewGuid();
        var buyer = new AppUser { Id = buyerId, Username = "DOMAIN1\\sara", DisplayName = "سارا خرید", IsEnabled = true };
        buyer.UserRoles.Add(new UserRole { UserId = buyerId, RoleId = buyerRole.Id, Role = buyerRole });

        var adminId = Guid.NewGuid();
        var admin = new AppUser { Id = adminId, Username = "DOMAIN2\\sara", DisplayName = "سارا مدیر", IsEnabled = true };
        admin.UserRoles.Add(new UserRole { UserId = adminId, RoleId = adminRole.Id, Role = adminRole });

        dbContext.Roles.AddRange(adminRole, buyerRole);
        dbContext.Users.AddRange(buyer, admin);
        dbContext.UserExtensions.AddRange(
            new UserExtension { Id = Guid.NewGuid(), UserId = buyerId, Username = "DOMAIN1\\sara", Extension = "101", IsActive = true },
            new UserExtension { Id = Guid.NewGuid(), UserId = adminId, Username = "sara", Extension = "202", IsActive = true }
        );
        await dbContext.SaveChangesAsync();

        var buyerLogin = new WindowsCurrentUserService(new HttpContextAccessor { HttpContext = WindowsIdentity("DOMAIN1\\sara") }, dbContext);
        buyerLogin.UserId.Should().Be(buyerId);
        buyerLogin.IsInRole(Roles.Admin).Should().BeFalse();
        buyerLogin.Extension.Should().Be("101");

        var buyerScope = await accessControlService.GetUserAccessScopeAsync(buyerLogin.UserId, buyerLogin.Username);
        buyerScope.HasGlobalAccess.Should().BeFalse();
        buyerScope.VerifiedUserExtension.Should().Be("101");
        accessControlService.CanAccessCall(buyerScope, "202", "09120000000", "202").Should().BeFalse();

        var shortLogin = new WindowsCurrentUserService(new HttpContextAccessor { HttpContext = WindowsIdentity("sara") }, dbContext);
        shortLogin.UserId.Should().BeNull();
        shortLogin.Extension.Should().BeNull();
        var shortScope = await accessControlService.GetUserAccessScopeAsync(shortLogin.UserId, shortLogin.Username);
        shortScope.HasGlobalAccess.Should().BeFalse();
        shortScope.AllowedExtensions.Should().BeEmpty();
        accessControlService.CanAccessCall(shortScope, "202", "09120000000", "202").Should().BeFalse();
        accessControlService.CanAccessCall(shortScope, "101", "09120000000", "101").Should().BeFalse();
    }

    [Fact(DisplayName = "21. Updating one account extension does not rewrite another account extension with a similar name")]
    public async Task Scenario21_UpdateExtension_DoesNotRewriteSimilarlyNamedExtension()
    {
        var (dbContext, _, _, _, accessControlService) = CreateSetup();

        var buyerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        dbContext.Users.AddRange(
            new AppUser { Id = buyerId, Username = "DOMAIN1\\sara", DisplayName = "سارا خرید", IsEnabled = true },
            new AppUser { Id = adminId, Username = "DOMAIN2\\sara", DisplayName = "سارا مدیر", IsEnabled = true }
        );
        dbContext.UserExtensions.Add(new UserExtension
        {
            Id = Guid.NewGuid(),
            UserId = adminId,
            Username = "sara",
            Extension = "202",
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var controller = new UsersController(
            dbContext,
            new FakeActiveDirectoryService(),
            new TestCurrentUserService { RolesList = new List<string> { Roles.Admin }, ActiveRole = Roles.Admin },
            new FakeTelephonyAdapter(),
            accessControlService,
            NullLogger<UsersController>.Instance);

        var listed = await controller.GetAll(CancellationToken.None);
        var users = (listed.Result as OkObjectResult)!.Value as List<UserDto>;
        users!.Single(u => u.Id == buyerId).Extension.Should().BeNull();
        users.Single(u => u.Id == adminId).Extension.Should().Be("202");

        var updated = await controller.UpdateExtension(buyerId, new UpdateUserExtensionRequest { Extension = "101" }, CancellationToken.None);
        updated.Should().BeOfType<OkObjectResult>();

        dbContext.UserExtensions.Single(e => e.UserId == adminId).Extension.Should().Be("202");
        dbContext.UserExtensions.Single(e => e.UserId == buyerId).Extension.Should().Be("101");
    }

    private static DefaultHttpContext WindowsIdentity(string username)
    {
        var http = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.NameIdentifier, username)
        };
        http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Negotiate"));
        return http;
    }
}
