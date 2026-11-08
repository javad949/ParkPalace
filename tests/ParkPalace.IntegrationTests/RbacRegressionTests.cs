using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Api.Controllers;
using ParkPalace.Application.DTOs;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Adapters;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Identity;
using ParkPalace.Infrastructure.Services;
using ParkPalace.Infrastructure.Services.Reporting;
using Xunit;

namespace ParkPalace.IntegrationTests;

public class RbacRegressionTests
{
    private (CrmDbContext db, string dbName) CreateIsolatedSqlServerContext()
    {
        var dbName = $"PP_RBAC_{Guid.NewGuid():N}".Substring(0, 20);
        var connStr = $"Server=localhost;Database={dbName};Integrated Security=True;TrustServerCertificate=True;";
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer(connStr)
            .Options;

        var context = new CrmDbContext(options);
        context.Database.EnsureCreated();
        CrmDbSeeder.SeedAsync(context).GetAwaiter().GetResult();
        return (context, dbName);
    }

    private void CleanupSqlServerContext(CrmDbContext db, string dbName)
    {
        try
        {
            db.Database.EnsureDeleted();
            db.Dispose();
        }
        catch { }
    }

    private WindowsCurrentUserService CreateWindowsCurrentUserService(CrmDbContext db, string username, string activeRole)
    {
        var httpContext = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.NameIdentifier, username)
        };
        var identity = new ClaimsIdentity(claims, "Negotiate");
        httpContext.User = new ClaimsPrincipal(identity);
        if (!string.IsNullOrEmpty(activeRole))
        {
            httpContext.Request.Headers["X-Active-Role"] = activeRole;
        }

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        return new WindowsCurrentUserService(accessor, db);
    }

    private async Task<(Party supplier, Warehouse warehouse, Material material, UnitOfMeasure unit)> EnsurePrerequisitesAsync(CrmDbContext db)
    {
        var unit = await db.UnitsOfMeasure.FirstAsync();
        var category = await db.Categories.FirstAsync();
        var warehouse = await db.Warehouses.FirstAsync();

        var supplier = await db.Parties.FirstOrDefaultAsync();
        if (supplier == null)
        {
            supplier = new Party
            {
                Id = Guid.NewGuid(),
                DisplayName = "تأمین‌کننده آرد ممتاز سپهر",
                LegalName = "شرکت آرد سپهر سهامی خاص",
                PartyType = PartyType.LegalEntity,
                IsActive = true
            };
            db.Parties.Add(supplier);
        }

        var material = await db.Materials.FirstOrDefaultAsync();
        if (material == null)
        {
            material = new Material
            {
                Id = Guid.NewGuid(),
                InternalCode = "MAT-RBAC-01",
                Name = "آرد گندم نانوایی",
                CategoryId = category.Id,
                BaseUnitId = unit.Id,
                IsActive = true
            };
            db.Materials.Add(material);
        }

        await db.SaveChangesAsync();
        return (supplier, warehouse, material, unit);
    }

    [Fact]
    public async Task ProcurementRole_CanViewOrders_AndIsForbiddenFromFinance()
    {
        var (db, dbName) = CreateIsolatedSqlServerContext();
        try
        {
            // Seed sample prerequisites and order
            var (supplier, warehouse, material, unit) = await EnsurePrerequisitesAsync(db);

            var po = new PurchaseOrder
            {
                Id = Guid.NewGuid(),
                OrderNumber = "PO-RBAC-001",
                SupplierId = supplier.Id,
                WarehouseId = warehouse.Id,
                Status = PurchaseOrderStatus.Draft,
                SubTotal = 100000m,
                GrandTotal = 100000m,
                CreatedBy = "CRM\\Administrator",
                ConcurrencyToken = Guid.NewGuid().ToString()
            };
            db.PurchaseOrders.Add(po);
            await db.SaveChangesAsync();

            // 1. Authenticate with WindowsCurrentUserService as Procurement active role
            var procUserService = CreateWindowsCurrentUserService(db, "CRM\\Administrator", Roles.Procurement);
            var poService = new PurchaseOrderService(db);
            var ordersController = new OrdersController(poService, procUserService);

            // Act 1: Procurement views orders
            var ordersResult = await ordersController.GetOrders();
            var okResult = ordersResult.Result as OkObjectResult;
            okResult.Should().NotBeNull();
            okResult!.StatusCode.Should().Be(200);

            var paged = okResult.Value as PagedResult<PurchaseOrderDto>;
            paged.Should().NotBeNull();
            paged!.Items.Should().Contain(o => o.OrderNumber == "PO-RBAC-001");

            // Act 2: Procurement forbidden from Finance dashboard
            var repService = new ReportingService(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<ReportingService>.Instance);
            var expService = new ExportService(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<ExportService>.Instance);
            var financeService = new FinancialDashboardService(db, new SepidarFixtureFinancialAdapter());
            var executiveService = new ExecutiveDashboardService(repService, financeService);
            var reportsController = new ReportsController(
                repService,
                expService,
                executiveService,
                procUserService,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ReportsController>.Instance);

            var finDashResult = await reportsController.GetRoleDashboard(Roles.Finance, new ReportFilterParams(), CancellationToken.None);
            var objResult = finDashResult.Result as ObjectResult;
            objResult.Should().NotBeNull();
            objResult!.StatusCode.Should().Be(403);
        }
        finally
        {
            CleanupSqlServerContext(db, dbName);
        }
    }

    [Fact]
    public async Task WarehouseRole_CanAccessInventoryAndReceipts_AndIsForbiddenFromFinance()
    {
        var (db, dbName) = CreateIsolatedSqlServerContext();
        try
        {
            var whUserService = CreateWindowsCurrentUserService(db, "CRM\\Administrator", Roles.Warehouse);

            // 1. Warehouse views warehouse info
            var syncService = new InventorySyncService(db, new SepidarFixtureInventoryAdapter(Microsoft.Extensions.Logging.Abstractions.NullLogger<SepidarFixtureInventoryAdapter>.Instance), new ConsumptionCalculator(db), new PurchaseRecommendationEngine(db, new ConsumptionCalculator(db)), Microsoft.Extensions.Logging.Abstractions.NullLogger<InventorySyncService>.Instance);
            var invController = new InventoryController(db, whUserService, syncService, new ConsumptionCalculator(db));

            var whResult = await invController.GetWarehouse(CancellationToken.None);
            var okWh = whResult.Result as OkObjectResult;
            okWh.Should().NotBeNull();
            okWh!.StatusCode.Should().Be(200);

            // 2. Warehouse views inventory items
            var itemsResult = await invController.GetItems(null, null, null, 1, 10, CancellationToken.None);
            var okItems = itemsResult.Result as OkObjectResult;
            okItems.Should().NotBeNull();
            okItems!.StatusCode.Should().Be(200);

            // 3. Warehouse views goods receipts
            var grService = new GoodsReceiptService(db);
            var grController = new GoodsReceiptsController(grService, whUserService);
            var grResult = await grController.GetReceipts();
            var okGr = grResult.Result as OkObjectResult;
            okGr.Should().NotBeNull();
            okGr!.StatusCode.Should().Be(200);

            // 4. Warehouse is strictly forbidden from Finance invoices
            var invService = new SupplierInvoiceService(db);
            var invoiceController = new SupplierInvoicesController(invService, whUserService);
            var invResult = await invoiceController.GetInvoices();
            var forbiddenInv = invResult.Result as ObjectResult;
            forbiddenInv.Should().NotBeNull();
            forbiddenInv!.StatusCode.Should().Be(403);
        }
        finally
        {
            CleanupSqlServerContext(db, dbName);
        }
    }

    [Fact]
    public async Task RoleChecks_DoNotCompromise_OptimisticConcurrencyProtection()
    {
        var (db, dbName) = CreateIsolatedSqlServerContext();
        try
        {
            var (supplier, warehouse, _, _) = await EnsurePrerequisitesAsync(db);
            var initialToken = Guid.NewGuid().ToString();

            var po = new PurchaseOrder
            {
                Id = Guid.NewGuid(),
                OrderNumber = "PO-CONCURR-001",
                SupplierId = supplier.Id,
                WarehouseId = warehouse.Id,
                Status = PurchaseOrderStatus.Draft,
                SubTotal = 500000m,
                GrandTotal = 500000m,
                CreatedBy = "CRM\\Administrator",
                ConcurrencyToken = initialToken
            };
            db.PurchaseOrders.Add(po);
            await db.SaveChangesAsync();

            // Simulate two threads using WindowsCurrentUserService
            var user1 = CreateWindowsCurrentUserService(db, "CRM\\Administrator", Roles.Procurement);
            var poService1 = new PurchaseOrderService(db);
            var controller1 = new OrdersController(poService1, user1);

            // 1. Attempt to issue order with stale/mismatched token -> returns 409 Conflict
            var staleIssue = await controller1.IssueOrder(po.Id, new IssueOrderPayload { ConcurrencyToken = "STALE_TOKEN_" + Guid.NewGuid() });
            var conflictIssue = staleIssue.Result as ObjectResult;
            conflictIssue.Should().NotBeNull();
            conflictIssue!.StatusCode.Should().Be(409);

            // 2. Issue order with valid matching token -> succeeds with 200 OK
            var issue1 = await controller1.IssueOrder(po.Id, new IssueOrderPayload { ConcurrencyToken = initialToken });
            var okIssue = issue1.Result as OkObjectResult;
            okIssue.Should().NotBeNull();
            okIssue!.StatusCode.Should().Be(200);

            // 3. Repeat attempt to issue order after it is already Issued -> blocked with 400 Bad Request
            var repeatIssue = await controller1.IssueOrder(po.Id, new IssueOrderPayload { ConcurrencyToken = initialToken });
            var badRequestIssue = repeatIssue.Result as ObjectResult;
            badRequestIssue.Should().NotBeNull();
            badRequestIssue!.StatusCode.Should().Be(400);
        }
        finally
        {
            CleanupSqlServerContext(db, dbName);
        }
    }
}
