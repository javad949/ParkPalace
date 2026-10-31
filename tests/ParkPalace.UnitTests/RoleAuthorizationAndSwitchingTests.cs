using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Identity;
using Xunit;

namespace ParkPalace.UnitTests;

public class RoleAuthorizationAndSwitchingTests
{
    private CrmDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var ctx = new CrmDbContext(options);

        // Seed roles and permissions
        CrmDbSeeder.SeedAsync(ctx).GetAwaiter().GetResult();
        return ctx;
    }

    private HttpContext CreateHttpContext(string username, string? activeRole = null)
    {
        var ctx = new DefaultHttpContext();
        var claims = new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, username),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, username)
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "Negotiate");
        ctx.User = new System.Security.Claims.ClaimsPrincipal(identity);

        if (!string.IsNullOrEmpty(activeRole))
        {
            ctx.Request.Headers["X-Active-Role"] = activeRole;
        }

        return ctx;
    }

    [Fact]
    public void Test1_AdminUser_HasAccessToAdministrativeOperationsByDefault()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var http = CreateHttpContext("CRM\\Administrator");
        var accessor = new HttpContextAccessor { HttpContext = http };
        var service = new WindowsCurrentUserService(accessor, db);

        // Assert
        service.IsAuthenticated.Should().BeTrue();
        service.Username.Should().Be("CRM\\Administrator");
        service.ActiveRole.Should().Be(Roles.Admin);
        service.IsInRole(Roles.Admin).Should().BeTrue();
        service.HasPermission(Permissions.AdminUsers).Should().BeTrue();
        service.HasPermission(Permissions.AdminSettings).Should().BeTrue();
        service.AvailableRoles.Should().Contain(new[] { Roles.Admin, Roles.Procurement, Roles.Warehouse, Roles.Finance, Roles.Manager });
    }

    [Fact]
    public void Test2_RestrictedUser_OnlyHasAuthorizedPermissionsOfAssignedRole()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var http = CreateHttpContext("SAHAR\\warehouse");
        var accessor = new HttpContextAccessor { HttpContext = http };
        var service = new WindowsCurrentUserService(accessor, db);

        // Assert
        service.IsAuthenticated.Should().BeTrue();
        service.Username.Should().Be("SAHAR\\warehouse");
        service.ActiveRole.Should().Be(Roles.Warehouse);
        service.IsInRole(Roles.Warehouse).Should().BeTrue();
        service.IsInRole(Roles.Admin).Should().BeFalse();

        // Warehouse permissions: allowed
        service.HasPermission(Permissions.WarehouseView).Should().BeTrue();
        service.HasPermission(Permissions.WarehouseReceive).Should().BeTrue();
        service.HasPermission(Permissions.MaterialsView).Should().BeTrue();

        // Administrative & Finance permissions: forbidden
        service.HasPermission(Permissions.AdminUsers).Should().BeFalse();
        service.HasPermission(Permissions.AdminSettings).Should().BeFalse();
        service.HasPermission(Permissions.OrdersCreate).Should().BeFalse();
        service.HasPermission(Permissions.OrdersIssue).Should().BeFalse();
        service.HasPermission(Permissions.FinanceView).Should().BeFalse();
        service.HasPermission(Permissions.FinanceReconcile).Should().BeFalse();

        // Available roles strictly limited to assigned database roles
        service.AvailableRoles.Should().ContainSingle(r => r == Roles.Warehouse);
        service.AvailableRoles.Should().NotContain(Roles.Admin);
    }

    [Fact]
    public void Test3_RestrictedUser_DirectApiCallToAdminOperation_IsDenied()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var http = CreateHttpContext("SAHAR\\warehouse");
        var accessor = new HttpContextAccessor { HttpContext = http };
        var service = new WindowsCurrentUserService(accessor, db);

        // Assert: simulating endpoint check
        var canAccessRolesController = service.IsInRole(Roles.Admin) || service.HasPermission(Permissions.AdminUsers);
        canAccessRolesController.Should().BeFalse("Warehouse user must not have access to Roles endpoint");

        var canCreateOrder = service.IsInRole(Roles.Admin) || service.HasPermission(Permissions.OrdersCreate);
        canCreateOrder.Should().BeFalse("Warehouse user must not have access to Create Purchase Order");
    }

    [Fact]
    public void Test4_PrivilegeEscalationAttempt_HeaderTampering_IsSafelyRejectedByBackend()
    {
        // Arrange: Warehouse user malicious request attempting to become Admin via header
        using var db = CreateInMemoryContext();
        var http = CreateHttpContext("SAHAR\\warehouse", activeRole: Roles.Admin); // Tampered header!
        var accessor = new HttpContextAccessor { HttpContext = http };
        var service = new WindowsCurrentUserService(accessor, db);

        // Assert: Backend MUST reject the requested role because Admin is not in user's AvailableRoles
        service.ActiveRole.Should().Be(Roles.Warehouse, "Privilege escalation must be rejected and defaulted to assigned role");
        service.IsInRole(Roles.Admin).Should().BeFalse("Tampered role must NOT grant Admin role");
        service.HasPermission(Permissions.AdminUsers).Should().BeFalse("Tampered role must NOT grant Admin permissions");
        service.AvailableRoles.Should().NotContain(Roles.Admin);
    }

    [Fact]
    public void Test5_Admin_ActiveRoleSwitching_ScopesEffectivePermissionsProperly()
    {
        // Arrange: Administrator deliberately switches active role to Warehouse
        using var db = CreateInMemoryContext();
        var http = CreateHttpContext("CRM\\Administrator", activeRole: Roles.Warehouse);
        var accessor = new HttpContextAccessor { HttpContext = http };
        var service = new WindowsCurrentUserService(accessor, db);

        // Assert: While in Warehouse role, effective permissions are scoped to Warehouse only
        service.ActiveRole.Should().Be(Roles.Warehouse);
        service.Roles.Should().ContainSingle(r => r == Roles.Warehouse);
        service.IsInRole(Roles.Admin).Should().BeFalse("When active role is Warehouse, IsInRole(Admin) must be false");
        service.HasPermission(Permissions.AdminUsers).Should().BeFalse("Admin permissions must be inactive during Warehouse audit");
        service.HasPermission(Permissions.WarehouseView).Should().BeTrue();
        service.HasPermission(Permissions.WarehouseReceive).Should().BeTrue();

        // Available roles still allows switching back to Admin
        service.AvailableRoles.Should().Contain(Roles.Admin);

        // Switch back to Admin
        var httpAdmin = CreateHttpContext("CRM\\Administrator", activeRole: Roles.Admin);
        var accessorAdmin = new HttpContextAccessor { HttpContext = httpAdmin };
        var serviceAdmin = new WindowsCurrentUserService(accessorAdmin, db);

        serviceAdmin.ActiveRole.Should().Be(Roles.Admin);
        serviceAdmin.IsInRole(Roles.Admin).Should().BeTrue();
        serviceAdmin.HasPermission(Permissions.AdminUsers).Should().BeTrue();
    }

    [Fact]
    public void Test6_PersianRoleTitleAndDisplayName_AreCleanUnicodeWithoutMojibake()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var http = CreateHttpContext("CRM\\Administrator");
        var accessor = new HttpContextAccessor { HttpContext = http };
        var service = new WindowsCurrentUserService(accessor, db);

        // Assert clean display name from DB
        service.DisplayName.Should().Be("مدیر سرور CRM (Admin)");
        service.DisplayName.Should().NotContain("\u00D8", "Must not contain Mojibake Ø");
        service.DisplayName.Should().NotContain("\u00D9", "Must not contain Mojibake Ù");
        service.DisplayName.Should().NotContain("?", "Must not contain question marks");
    }
}
