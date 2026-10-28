using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkPalace.Api.Controllers;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Identity;
using ParkPalace.Infrastructure.Services;
using Xunit;

namespace ParkPalace.IntegrationTests;

public class OrdersControllerPermissionReproTests
{
    [Fact]
    public async Task OrdersController_WithProcurementRole_ShouldAllowAccessOrShowExactCause()
    {
        // 1. Setup isolated in-memory or SQL DB with permissions and roles
        var dbName = $"PP_Repro_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        using var db = new CrmDbContext(options);
        await CrmDbSeeder.SeedAsync(db);

        // Add a Procurement user and an Admin user
        var procRole = await db.Roles.FirstAsync(r => r.Name == Roles.Procurement);
        var adminRole = await db.Roles.FirstAsync(r => r.Name == Roles.Admin);

        var procUser = new AppUser
        {
            Id = Guid.NewGuid(),
            Username = "SAHAR\\procurement",
            DisplayName = "کارشناس خرید",
            IsEnabled = true
        };
        db.Users.Add(procUser);
        db.UserRoles.Add(new UserRole { UserId = procUser.Id, RoleId = procRole.Id });

        var adminUser = new AppUser
        {
            Id = Guid.NewGuid(),
            Username = "CRM\\Administrator",
            DisplayName = "مدیر سیستم",
            IsEnabled = true
        };
        db.Users.Add(adminUser);
        db.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id });

        await db.SaveChangesAsync();

        // 2. Setup HttpContext with CRM\Administrator and X-Active-Role: Procurement
        var httpContext = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "CRM\\Administrator"),
            new Claim(ClaimTypes.NameIdentifier, "CRM\\Administrator")
        };
        var identity = new ClaimsIdentity(claims, "Negotiate");
        httpContext.User = new ClaimsPrincipal(identity);
        httpContext.Request.Headers["X-Active-Role"] = "Procurement";

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var currentUserService = new WindowsCurrentUserService(accessor, db);

        // Check current user service state
        currentUserService.IsAuthenticated.Should().BeTrue();
        currentUserService.Username.Should().Be("CRM\\Administrator");
        currentUserService.ActiveRole.Should().Be("Procurement");
        currentUserService.Roles.Should().Contain("Procurement");
        currentUserService.Permissions.Should().Contain(Permissions.OrdersView);

        // 3. Instantiate OrdersController
        var poService = new PurchaseOrderService(db);
        var controller = new OrdersController(poService, currentUserService);

        // Act
        var result = await controller.GetOrders();

        // Assert
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var statusCodeResult = result.Result as Microsoft.AspNetCore.Mvc.ObjectResult;

        if (statusCodeResult != null && statusCodeResult.StatusCode == 403)
        {
            var problem = statusCodeResult.Value as Microsoft.AspNetCore.Mvc.ProblemDetails;
            throw new Exception($"OrdersController returned 403: {problem?.Detail}");
        }

        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
    }
}
