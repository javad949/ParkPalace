using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Identity;
using Xunit;

namespace ParkPalace.UnitTests;

public class DomainAndEntityTests
{
    private CrmDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CrmDbContext(options);
    }

    [Fact]
    public void AppUser_Creation_ShouldSetUtcTimestampAndDefaultProperties()
    {
        // Arrange & Act
        var user = new AppUser
        {
            Username = "sahar\\alireza",
            DisplayName = "علیرضا رضایی",
            AdObjectSid = "S-1-5-21-123456789-500",
            Email = "alireza@sahar.loc"
        };

        // Assert
        user.Id.Should().NotBeEmpty();
        user.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        user.IsEnabled.Should().BeTrue();
        user.Username.Should().Be("sahar\\alireza");
    }

    [Fact]
    public async Task CrmDbSeeder_ShouldSeedStandardRolesAndPermissions()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        // Act
        await CrmDbSeeder.SeedAsync(context);

        // Assert
        var roles = await context.Roles.Include(r => r.RolePermissions).ToListAsync();
        roles.Should().HaveCount(5);
        roles.Select(r => r.Name).Should().Contain(new[] {
            Roles.Admin,
            Roles.Procurement,
            Roles.Manager,
            Roles.Warehouse,
            Roles.Finance
        });

        var adminRole = roles.First(r => r.Name == Roles.Admin);
        adminRole.RolePermissions.Should().HaveCount(Permissions.All.Length);

        var procurementRole = roles.First(r => r.Name == Roles.Procurement);
        var permCodes = await context.Permissions
            .Where(p => procurementRole.RolePermissions.Select(rp => rp.PermissionId).Contains(p.Id))
            .Select(p => p.Code)
            .ToListAsync();

        // Verify Procurement can create and directly issue POs without manager approval (FR-05, AC-08)
        permCodes.Should().Contain(Permissions.OrdersCreate);
        permCodes.Should().Contain(Permissions.OrdersIssue);
        permCodes.Should().Contain(Permissions.MaterialsView);
        permCodes.Should().Contain(Permissions.SuppliersView);
    }

    [Fact]
    public void DevCurrentUserService_WithProcurementRole_ShouldHaveProcurementPermissions()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        httpContextAccessor.HttpContext.Request.Headers["X-Dev-Role"] = "Procurement";
        httpContextAccessor.HttpContext.Request.Headers["X-Dev-User"] = "sahar\\procurement_lead";

        var userService = new DevCurrentUserService(httpContextAccessor, context);

        // Act & Assert
        userService.IsAuthenticated.Should().BeTrue();
        userService.Username.Should().Be("sahar\\procurement_lead");
        userService.IsInRole(Roles.Procurement).Should().BeTrue();
        userService.HasPermission(Permissions.OrdersIssue).Should().BeTrue();
        userService.HasPermission(Permissions.AdminUsers).Should().BeFalse();
    }

    [Fact]
    public void DevCurrentUserService_UnauthenticatedHeader_ShouldDenyAccess()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        httpContextAccessor.HttpContext.Request.Headers["X-Dev-Unauthenticated"] = "true";

        var userService = new DevCurrentUserService(httpContextAccessor, context);

        // Act & Assert
        userService.IsAuthenticated.Should().BeFalse();
        userService.Roles.Should().BeEmpty();
        userService.Permissions.Should().BeEmpty();
    }
}
