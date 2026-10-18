using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ParkPalace.Application.DTOs;
using Xunit;

namespace ParkPalace.IntegrationTests;

public class Phase4IntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public Phase4IntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string uri, string role = "Admin")
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-Dev-Role", role);
        request.Headers.Add("X-Dev-User", $"sahar\\{role.ToLower()}");
        return request;
    }

    [Fact]
    public async Task TriggerSync_ExecutesSuccessfullyAndPopulatesSnapshotsAndMovements()
    {
        // Act: Trigger Manual Sync as Admin
        using var syncReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/inventory/sync", "Admin");
        var syncRes = await _client.SendAsync(syncReq);

        // Assert
        syncRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var job = await syncRes.Content.ReadFromJsonAsync<SyncJobDto>();
        job.Should().NotBeNull();
        job!.Status.Should().Be("Success");
        job.ItemsProcessed.Should().BeGreaterThan(0);
        job.SnapshotsUpdated.Should().BeGreaterThan(0);
        job.MovementsProcessed.Should().BeGreaterThan(0);

        // Verify Sync Status endpoint
        using var statusReq = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/inventory/sync/status", "Warehouse");
        var statusRes = await _client.SendAsync(statusReq);
        statusRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await statusRes.Content.ReadFromJsonAsync<SyncStatusDto>();
        status.Should().NotBeNull();
        status!.LastSuccessfulSyncUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task GetInventoryWarehouse_ReturnsSummaryAndCriticalItemCounts()
    {
        // 1. Ensure sync has run
        using var syncReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/inventory/sync", "Admin");
        await _client.SendAsync(syncReq);

        // 2. Query warehouse summary as Warehouse role
        using var whReq = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/inventory/warehouse", "Warehouse");
        var whRes = await _client.SendAsync(whReq);

        whRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var wh = await whRes.Content.ReadFromJsonAsync<WarehouseDto>();
        wh.Should().NotBeNull();
        wh!.Code.Should().Be("WH-MAIN");
        wh.Name.Should().Contain("انبار مرکزی");
        wh.TotalItemsCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetInventoryItems_FiltersAndComputesConsumption()
    {
        // 1. Ensure sync has run
        using var syncReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/inventory/sync", "Admin");
        await _client.SendAsync(syncReq);

        // 2. Query all inventory items as Procurement role
        using var itemsReq = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/inventory/items?page=1&pageSize=10", "Procurement");
        var itemsRes = await _client.SendAsync(itemsReq);

        itemsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedResult = await itemsRes.Content.ReadFromJsonAsync<PagedResult<InventoryItemDto>>();
        pagedResult.Should().NotBeNull();
        pagedResult!.Items.Should().NotBeEmpty();

        var flourItem = pagedResult.Items.FirstOrDefault(i => i.InternalCode == "MAT-FLOUR-STAR");
        flourItem.Should().NotBeNull();
        flourItem!.OnHandQuantity.Should().Be(12500m);
        flourItem.DailyConsumption7d.Should().BeGreaterThan(0m);
        flourItem.DailyConsumption30d.Should().BeGreaterThan(0m);

        // 3. Query item details and movements
        using var detailReq = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/v1/inventory/items/{flourItem.MaterialId}", "Procurement");
        var detailRes = await _client.SendAsync(detailReq);
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailRes.Content.ReadFromJsonAsync<InventoryItemDetailDto>();
        detail.Should().NotBeNull();
        detail!.ReservableQuantity.Should().Be(10000m);

        using var movReq = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/v1/inventory/items/{flourItem.MaterialId}/movements", "Warehouse");
        var movRes = await _client.SendAsync(movReq);
        movRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var movs = await movRes.Content.ReadFromJsonAsync<PagedResult<InventoryMovementDto>>();
        movs.Should().NotBeNull();
        movs!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetRecommendations_ReturnsCalculatedAdvisoryPurchases()
    {
        // 1. Ensure sync has run
        using var syncReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/inventory/sync", "Admin");
        await _client.SendAsync(syncReq);

        // 2. Query recommendations as Procurement role
        using var recReq = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/procurement/recommendations", "Procurement");
        var recRes = await _client.SendAsync(recReq);

        recRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var recs = await recRes.Content.ReadFromJsonAsync<List<PurchaseRecommendationDto>>();
        recs.Should().NotBeNull();
        recs.Should().NotBeEmpty();

        // Yeast (MAT-YEAST-DRY) has OnHand 80 kg vs SafetyStock 100 kg, so recommendation should be active
        var yeastRec = recs!.FirstOrDefault(r => r.MaterialCode == "MAT-YEAST-DRY");
        yeastRec.Should().NotBeNull();
        yeastRec!.AverageDailyConsumption.Should().BeGreaterThan(0m);
        yeastRec.RecommendationExplanation.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ReplenishmentPolicy_Update_SucceedsAndEnforcesPermissions()
    {
        // 1. Ensure sync has run
        using var syncReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/inventory/sync", "Admin");
        await _client.SendAsync(syncReq);

        using var itemsReq = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/inventory/items", "Procurement");
        var itemsRes = await _client.SendAsync(itemsReq);
        var items = await itemsRes.Content.ReadFromJsonAsync<PagedResult<InventoryItemDto>>();
        var firstMat = items!.Items.First();

        // 2. Update policy as Procurement
        using var updateReq = CreateAuthenticatedRequest(HttpMethod.Put, $"/api/v1/inventory/replenishment-policies/{firstMat.MaterialId}", "Procurement");
        updateReq.Content = JsonContent.Create(new UpdateReplenishmentPolicyRequest
        {
            ReviewPeriodDays = 5,
            LeadTimeDays = 3,
            SafetyStock = 2500m,
            MinOrderQuantity = 5000m,
            PackageMultiple = 50m,
            Notes = "سفارش مستقیم کارخانه آرد"
        });

        var updateRes = await _client.SendAsync(updateReq);
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var policy = await updateRes.Content.ReadFromJsonAsync<ReplenishmentPolicyDto>();
        policy.Should().NotBeNull();
        policy!.SafetyStock.Should().Be(2500m);
        policy.MinOrderQuantity.Should().Be(5000m);
        policy.PackageMultiple.Should().Be(50m);
    }

    [Fact]
    public async Task RBAC_InventoryEndpoints_EnforceForbiddenForUnauthorizedRoles()
    {
        // 1. Role "Finance" does NOT have PoliciesManage or InventorySync permissions
        using var syncReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/inventory/sync", "Finance");
        var syncRes = await _client.SendAsync(syncReq);
        syncRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 2. Role "Warehouse" does NOT have PoliciesManage permission
        using var updateReq = CreateAuthenticatedRequest(HttpMethod.Put, $"/api/v1/inventory/replenishment-policies/{Guid.NewGuid()}", "Warehouse");
        updateReq.Content = JsonContent.Create(new UpdateReplenishmentPolicyRequest { ReviewPeriodDays = 4 });
        var updateRes = await _client.SendAsync(updateReq);
        updateRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
