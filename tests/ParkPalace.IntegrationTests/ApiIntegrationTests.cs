using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkPalace.Application.DTOs;
using ParkPalace.Domain.Constants;
using ParkPalace.Infrastructure.Data;
using Xunit;

namespace ParkPalace.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Replace CrmDbContext with isolated InMemoryDatabase for this test factory
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CrmDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<CrmDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });
        });
    }
}

public class ApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_Health_ShouldReturn200AndHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<HealthResponseDto>();
        content.Should().NotBeNull();
        content!.Status.Should().Be("Healthy");
        content.Service.Should().Be("ParkPalace Procurement CRM API");
        content.SepidarIntegrationStatus.Should().Contain("Not configured");
    }

    [Fact]
    public async Task Get_Me_WithUnauthenticatedHeader_ShouldReturn401()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Add("X-Dev-Unauthenticated", "true");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Me_WithAuthorizedDevUser_ShouldReturn200AndUserProfile()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Add("X-Dev-User", "sahar\\procurement_user");
        request.Headers.Add("X-Dev-Role", "Procurement");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<MeResponseDto>();
        content.Should().NotBeNull();
        content!.Username.Should().Be("sahar\\procurement_user");
        content.Roles.Should().Contain(Roles.Procurement);
        content.Permissions.Should().Contain(Permissions.OrdersIssue);
    }

    [Fact]
    public async Task Get_Roles_WithNonAdminRole_ShouldReturn403Forbidden()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/roles");
        request.Headers.Add("X-Dev-Role", "Warehouse");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Roles_WithAdminRole_ShouldReturn200AndRoleList()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/roles");
        request.Headers.Add("X-Dev-Role", "Admin");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var roles = await response.Content.ReadFromJsonAsync<List<RoleDto>>();
        roles.Should().NotBeNull();
        roles!.Should().HaveCount(5);
        roles!.Select(r => r.Name).Should().Contain(new[] { Roles.Admin, Roles.Procurement, Roles.Warehouse, Roles.Finance, Roles.Manager });
    }
}
