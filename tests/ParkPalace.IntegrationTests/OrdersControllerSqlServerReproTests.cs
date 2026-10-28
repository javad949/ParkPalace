using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Api.Controllers;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Identity;
using ParkPalace.Infrastructure.Services;
using Xunit;

namespace ParkPalace.IntegrationTests;

public class OrdersControllerSqlServerReproTests
{
    [Fact]
    public async Task OrdersController_OnRealSqlServer_TraceExactPermissions()
    {
        var dbName = $"PP_Repro_{Guid.NewGuid():N}".Substring(0, 20);
        var connStr = $"Server=localhost;Database={dbName};Integrated Security=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer(connStr)
            .Options;

        using (var dbSetup = new CrmDbContext(options))
        {
            await dbSetup.Database.EnsureCreatedAsync();
            await CrmDbSeeder.SeedAsync(dbSetup);

            var procRole = await dbSetup.Roles.FirstAsync(r => r.Name == Roles.Procurement);
            var adminRole = await dbSetup.Roles.FirstAsync(r => r.Name == Roles.Admin);

            var adminUser = new AppUser
            {
                Id = Guid.NewGuid(),
                Username = "CRM\\Administrator",
                DisplayName = "مدیر سیستم",
                IsEnabled = true
            };
            dbSetup.Users.Add(adminUser);
            dbSetup.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id });
            await dbSetup.SaveChangesAsync();
        }

        try
        {
            // Now open a NEW connection/context as happens in a web request
            using var db = new CrmDbContext(options);

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

            var poService = new PurchaseOrderService(db);
            var controller = new OrdersController(poService, currentUserService);

            var activeRole = currentUserService.ActiveRole;
            var perms = currentUserService.Permissions;
            var hasOrdersView = currentUserService.HasPermission(Permissions.OrdersView);

            var result = await controller.GetOrders();
            var statusCodeResult = result.Result as Microsoft.AspNetCore.Mvc.ObjectResult;

            if (statusCodeResult != null && statusCodeResult.StatusCode == 403)
            {
                var problem = statusCodeResult.Value as Microsoft.AspNetCore.Mvc.ProblemDetails;
                throw new Exception($"FAIL 403 on SQL Server! ActiveRole={activeRole}, PermCount={perms.Count}, HasOrdersView={hasOrdersView}, Detail={problem?.Detail}");
            }

            var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
            okResult.Should().NotBeNull();
            okResult!.StatusCode.Should().Be(200);
        }
        finally
        {
            using var dbClean = new CrmDbContext(options);
            await dbClean.Database.EnsureDeletedAsync();
        }
    }
}
