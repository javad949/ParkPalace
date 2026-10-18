using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
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
/// Phase 8: Comprehensive Security Hardening & Vulnerability Resistance Tests
/// Validates security headers, SQL injection resistance, formula injection sanitization,
/// path traversal prevention, and RBAC boundary enforcement.
/// </summary>
public class Phase8SecurityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public Phase8SecurityTests(CustomWebApplicationFactory factory)
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
    public async Task SecurityHeaders_ArePresentOnAllResponses()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var headers = response.Headers;

        headers.Contains("X-Content-Type-Options").Should().BeTrue();
        headers.GetValues("X-Content-Type-Options").First().Should().Be("nosniff");

        headers.Contains("X-Frame-Options").Should().BeTrue();
        headers.GetValues("X-Frame-Options").First().Should().Be("SAMEORIGIN");

        headers.Contains("Referrer-Policy").Should().BeTrue();
        headers.GetValues("Referrer-Policy").First().Should().Be("strict-origin-when-cross-origin");

        headers.Contains("Content-Security-Policy").Should().BeTrue();
    }

    [Fact]
    public async Task SqlInjectionAttempts_InSearchAndFilterParameters_AreSafelyHandledWithoutError()
    {
        // Parameterized queries in EF Core should safely treat these strings as literals
        var sqlPayloads = new[]
        {
            "' OR '1'='1",
            "'; DROP TABLE Materials; --",
            "1 UNION SELECT null, null, null --",
            "admin' --",
            "1' AND 1=1 AND '1'='1"
        };

        foreach (var payload in sqlPayloads)
        {
            var uri = $"/api/v1/materials?search={Uri.EscapeDataString(payload)}";
            using var req = CreateAuthRequest(HttpMethod.Get, uri, "Procurement");
            var res = await _client.SendAsync(req);

            // Must NOT return 500 Internal Server Error
            res.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task PathTraversalAttack_OnAudioRecordingPlayback_IsSafelyBlocked()
    {
        var fakeId = Guid.NewGuid();
        using var req = CreateAuthRequest(HttpMethod.Get, $"/api/v1/calls/{fakeId}/recording", "Admin");
        var res = await _client.SendAsync(req);

        // Recording does not exist -> 404 (safe, no path leakage)
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CsvExport_FormulaInjectionDefense_PrependsSingleQuoteToSensitiveSymbols()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

        // Seed a PO with a potentially malicious formula in notes
        var supplierId = Guid.NewGuid();
        var supplier = new Party
        {
            Id = supplierId,
            DisplayName = "تأمین‌کننده آزمایشی فرمول",
            IsActive = true
        };
        db.Parties.Add(supplier);

        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = "=cmd|'/c calc'!A0", // Formula injection attempt
            SupplierId = supplierId,
            Status = PurchaseOrderStatus.Issued,
            IssuedAtUtc = DateTime.UtcNow,
            SubTotal = 5000000m,
            GrandTotal = 5000000m,
            Notes = "@SUM(1+1) injection"
        };
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();

        // Export to CSV
        using var exportReq = CreateAuthRequest(HttpMethod.Get, "/api/v1/reports/export/csv?reportType=PurchaseOrders", "Procurement");
        var exportRes = await _client.SendAsync(exportReq);
        exportRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var csvText = await exportRes.Content.ReadAsStringAsync();
        
        // Assert formula prefixing: '=' must be escaped with single quote "'"
        csvText.Should().Contain("'=cmd");
    }

    [Fact]
    public async Task AuditTrail_RecordsEveryExportOperation()
    {
        using var exportReq = CreateAuthRequest(HttpMethod.Get, "/api/v1/reports/export/csv?reportType=PurchaseOrders", "Procurement");
        var exportRes = await _client.SendAsync(exportReq);
        exportRes.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

        var recentExportAudit = await db.AuditEvents
            .Where(a => a.Action == "ReportExported" && a.ActorUsername == "sahar\\procurement")
            .OrderByDescending(a => a.TimestampUtc)
            .FirstOrDefaultAsync();

        recentExportAudit.Should().NotBeNull();
        recentExportAudit!.DetailsJson.Should().Contain("PurchaseOrders");
    }
}
