using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using ParkPalace.Application.DTOs;
using Xunit;

namespace ParkPalace.IntegrationTests;

public class Phase7IntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public Phase7IntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string uri, string role = "Procurement")
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-Dev-Role", role);
        request.Headers.Add("X-Dev-User", $"sahar\\{role.ToLower()}");
        return request;
    }

    [Fact]
    public async Task GetProcurementSummary_WithProcurementRole_Returns200()
    {
        using var req = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/reports/procurement-summary", "Procurement");
        var response = await _client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<ProcurementSummaryReportDto>();
        summary.Should().NotBeNull();
        summary!.GeneratedAtUtc.Should().BeBefore(DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task GetProcurementSummary_WithoutPermission_Returns403()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/reports/procurement-summary");
        req.Headers.Add("X-Dev-Role", "None");
        req.Headers.Add("X-Dev-User", "guest");

        var response = await _client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetRoleDashboard_ProcurementRole_AccessingProcurementDashboard_Returns200()
    {
        using var req = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/reports/role-dashboard/Procurement", "Procurement");
        var response = await _client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dash = await response.Content.ReadFromJsonAsync<RoleDashboardDto>();
        dash.Should().NotBeNull();
        dash!.Role.Should().Be("Procurement");
    }

    [Fact]
    public async Task GetRoleDashboard_CrossRoleBoundary_WarehouseCannotAccessFinanceDashboard_Returns403()
    {
        // Warehouse user trying to view Finance dashboard
        using var req = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/reports/role-dashboard/Finance", "Warehouse");
        var response = await _client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetRoleDashboard_ManagerCanAccessProcurementDashboard_Returns200()
    {
        // Manager role has cross-departmental supervisory permissions
        using var req = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/reports/role-dashboard/Procurement", "Manager");
        var response = await _client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dash = await response.Content.ReadFromJsonAsync<RoleDashboardDto>();
        dash.Should().NotBeNull();
        dash!.Role.Should().Be("Procurement");
    }

    [Fact]
    public async Task GetPurchaseOrderTrends_ReturnsTrendsList_With200()
    {
        using var req = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/reports/purchase-order-trends", "Manager");
        var response = await _client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trends = await response.Content.ReadFromJsonAsync<List<PurchaseOrderTrendItemDto>>();
        trends.Should().NotBeNull();
    }

    [Fact]
    public async Task GetInventoryHealth_WithWarehouseRole_Returns200()
    {
        using var req = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/reports/inventory-health", "Warehouse");
        var response = await _client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var health = await response.Content.ReadFromJsonAsync<InventoryHealthReportDto>();
        health.Should().NotBeNull();
        health!.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFinanceReconciliation_WithFinanceRole_Returns200()
    {
        using var req = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/reports/finance-reconciliation", "Finance");
        var response = await _client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<FinanceReconciliationReportDto>();
        report.Should().NotBeNull();
    }

    [Fact]
    public async Task GetIntegrationHealth_AdminRole_Returns200_WithFixtureAndSimulatedFlags()
    {
        using var req = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/reports/integration-health", "Admin");
        var response = await _client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var health = await response.Content.ReadFromJsonAsync<IntegrationHealthReportDto>();
        health.Should().NotBeNull();

        // Verify Sepidar and Issabel are transparently flagged
        health!.SepidarAdapter.AdapterType.Should().Contain("Fixture");
        health.SepidarAdapter.IsLiveConnected.Should().BeFalse();

        health.IssabelAdapter.AdapterType.Should().Contain("Simulated");
        health.IssabelAdapter.IsLiveConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ExportReport_WithExportPermission_ReturnsCsvFile_WithUtf8BomAndHeaders()
    {
        using var req = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/reports/export?reportType=PurchaseOrders&format=csv", "Procurement");
        var response = await _client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(3);

        // UTF-8 BOM verification
        bytes[0].Should().Be(0xEF);
        bytes[1].Should().Be(0xBB);
        bytes[2].Should().Be(0xBF);
    }

    [Fact]
    public async Task ExportReport_WithoutExportPermission_Returns403()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/reports/export?reportType=PurchaseOrders&format=csv");
        req.Headers.Add("X-Dev-Role", "None");
        req.Headers.Add("X-Dev-User", "guest");

        var response = await _client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
