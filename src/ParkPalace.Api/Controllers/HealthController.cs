using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ICrmDbContext _dbContext;
    private readonly ISepidarReadAdapter _sepidarAdapter;

    public HealthController(ICrmDbContext dbContext, ISepidarReadAdapter sepidarAdapter)
    {
        _dbContext = dbContext;
        _sepidarAdapter = sepidarAdapter;
    }

    [HttpGet]
    public async Task<ActionResult<HealthResponseDto>> Get(CancellationToken cancellationToken)
    {
        var dbConnected = false;
        try
        {
            dbConnected = await _dbContext.Users.AnyAsync(cancellationToken) || true;
        }
        catch
        {
            dbConnected = false;
        }

        var response = new HealthResponseDto
        {
            Status = "Healthy",
            Service = "ParkPalace Procurement CRM API",
            Version = "1.0.0",
            ServerTimeUtc = DateTime.UtcNow,
            DatabaseConnected = dbConnected,
            SepidarIntegrationStatus = _sepidarAdapter.IsConfigured 
                ? "Active (Read-Only)" 
                : "Not configured (Disabled by default in Phase 2)"
        };

        return Ok(response);
    }
}
