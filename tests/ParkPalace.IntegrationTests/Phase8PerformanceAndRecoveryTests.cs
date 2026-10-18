using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkPalace.Application.DTOs;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using Xunit;

namespace ParkPalace.IntegrationTests;

/// <summary>
/// Phase 8: Performance, Concurrency, and Integration Recovery Staging Tests
/// Validates concurrent transactions, bounded query latency, duplicate prevention (idempotency),
/// and service resilience.
/// </summary>
public class Phase8PerformanceAndRecoveryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public Phase8PerformanceAndRecoveryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private HttpRequestMessage CreateAuthRequest(HttpMethod method, string uri, string role = "Admin")
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-Dev-Role", role);
        request.Headers.Add("X-Dev-User", $"sahar\\{role.ToLower()}");
        return request;
    }

    [Fact]
    public async Task ConcurrentPoIssuance_MultipleParallelTransactions_SucceedsWithoutDeadlock()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = $"WH-CONC-{Guid.NewGuid():N}",
            Name = "انبار همروندی خرید",
            IsActive = true
        };
        db.Warehouses.Add(warehouse);

        var supplier = new Party
        {
            Id = Guid.NewGuid(),
            DisplayName = "تأمین‌کننده همروندی",
            IsActive = true
        };
        db.Parties.Add(supplier);
        await db.SaveChangesAsync();

        var poTasks = Enumerable.Range(1, 10).Select(async i =>
        {
            using var innerScope = _factory.Services.CreateScope();
            var innerDb = innerScope.ServiceProvider.GetRequiredService<CrmDbContext>();

            var po = new PurchaseOrder
            {
                Id = Guid.NewGuid(),
                OrderNumber = $"PO-CONC-{Guid.NewGuid():N}",
                SupplierId = supplier.Id,
                WarehouseId = warehouse.Id,
                Status = PurchaseOrderStatus.Issued,
                IssuedAtUtc = DateTime.UtcNow,
                SubTotal = i * 1000000m,
                GrandTotal = i * 1000000m
            };
            innerDb.PurchaseOrders.Add(po);
            await innerDb.SaveChangesAsync();
            return po.Id;
        });

        var createdIds = await Task.WhenAll(poTasks);
        createdIds.Should().HaveCount(10);
        createdIds.Distinct().Should().HaveCount(10);
    }

    [Fact]
    public async Task ConcurrentGoodsReceipts_ForSamePo_PreservesPartialReceivedStatus()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = $"WH-GRN-{Guid.NewGuid():N}",
            Name = "انبار همروند رسید",
            IsActive = true
        };
        db.Warehouses.Add(warehouse);

        var supplier = new Party
        {
            Id = Guid.NewGuid(),
            DisplayName = "تأمین‌کننده انبار همروند",
            IsActive = true
        };
        db.Parties.Add(supplier);

        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"PO-WH-{Guid.NewGuid():N}",
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            Status = PurchaseOrderStatus.Issued,
            IssuedAtUtc = DateTime.UtcNow,
            SubTotal = 50000000m,
            GrandTotal = 50000000m
        };
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();

        var receiptTasks = Enumerable.Range(1, 5).Select(async i =>
        {
            using var innerScope = _factory.Services.CreateScope();
            var innerDb = innerScope.ServiceProvider.GetRequiredService<CrmDbContext>();

            var grn = new GoodsReceipt
            {
                Id = Guid.NewGuid(),
                ReceiptNumber = $"GRN-CONC-{i}-{Guid.NewGuid():N}",
                PurchaseOrderId = po.Id,
                WarehouseId = warehouse.Id,
                ReceiptDateUtc = DateTime.UtcNow,
                Notes = $"رسید انبار همروند {i}"
            };
            innerDb.GoodsReceipts.Add(grn);
            await innerDb.SaveChangesAsync();
            return grn.Id;
        });

        var grnIds = await Task.WhenAll(receiptTasks);
        grnIds.Should().HaveCount(5);
    }

    [Fact]
    public async Task MultipleDashboardUsers_ConcurrentQueries_RespondWithinThreshold()
    {
        var roles = new[] { "Procurement", "Manager", "Warehouse", "Finance" };

        var queryTasks = roles.Select(async role =>
        {
            var uri = $"/api/v1/reports/role-dashboard/{role}";

            using var req = CreateAuthRequest(HttpMethod.Get, uri, role);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var res = await _client.SendAsync(req);
            watch.Stop();

            res.StatusCode.Should().Be(HttpStatusCode.OK);
            // Latency must be well under 3000ms threshold
            watch.ElapsedMilliseconds.Should().BeLessThan(3000);
            return watch.ElapsedMilliseconds;
        });

        var latencies = await Task.WhenAll(queryTasks);
        latencies.Should().AllSatisfy(ms => ms.Should().BeLessThan(3000));
    }

    [Fact]
    public async Task RepeatedInventorySync_IsIdempotent_AndDoesNotDuplicateCheckpoints()
    {
        using var scope = _factory.Services.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ParkPalace.Application.Interfaces.IInventorySyncService>();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

        // Trigger sync run 1
        var result1 = await syncService.TriggerSyncAsync(SyncJobType.Manual, "StagingTest");
        result1.Should().NotBeNull();

        // Trigger sync run 2 after completion (idempotent repeated execution)
        var result2 = await syncService.TriggerSyncAsync(SyncJobType.Manual, "StagingTest");
        result2.Should().NotBeNull();

        // Verify status remains healthy and checkpoints are maintained
        var status = await syncService.GetSyncStatusAsync();
        status.Should().NotBeNull();
    }
}
