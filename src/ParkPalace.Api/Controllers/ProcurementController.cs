using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/v1/procurement")]
public class ProcurementController : ControllerBase
{
    private readonly ICrmDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPurchaseRecommendationEngine _recommendationEngine;

    public ProcurementController(
        ICrmDbContext dbContext,
        ICurrentUserService currentUserService,
        IPurchaseRecommendationEngine recommendationEngine)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _recommendationEngine = recommendationEngine;
    }

    private bool HasPermission(string permission)
    {
        return _currentUserService.Roles.Contains(Roles.Admin) || 
               _currentUserService.Permissions.Contains(permission);
    }

    [HttpGet("recommendations")]
    public async Task<ActionResult<List<PurchaseRecommendationDto>>> GetRecommendations(CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.RecommendationsView) && !HasPermission(Permissions.OrdersView))
        {
            return StatusCode(403, new ProblemDetails
            {
                Title = "دسترسی غیرمجاز",
                Detail = "شما مجوز مشاهده محاسبات و پیشنهادات خرید را ندارید.",
                Status = 403
            });
        }

        var warehouse = await _dbContext.Warehouses.FirstOrDefaultAsync(w => w.Code == "WH-MAIN", cancellationToken);
        var warehouseId = warehouse?.Id ?? Guid.Empty;

        var recommendations = await _recommendationEngine.GenerateAllRecommendationsAsync(warehouseId, cancellationToken);
        return Ok(recommendations);
    }

    [HttpGet("recommendations/{materialId:guid}")]
    public async Task<ActionResult<PurchaseRecommendationDto>> GetRecommendationForMaterial(
        Guid materialId, 
        CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.RecommendationsView) && !HasPermission(Permissions.OrdersView))
        {
            return StatusCode(403, new ProblemDetails { Title = "دسترسی غیرمجاز", Status = 403 });
        }

        var warehouse = await _dbContext.Warehouses.FirstOrDefaultAsync(w => w.Code == "WH-MAIN", cancellationToken);
        var warehouseId = warehouse?.Id ?? Guid.Empty;

        var rec = await _recommendationEngine.GenerateRecommendationForMaterialAsync(materialId, warehouseId, cancellationToken);
        if (rec == null)
        {
            return NotFound(new ProblemDetails { Title = "پیشنهاد خرید برای این کالا یافت نشد", Status = 404 });
        }

        return Ok(rec);
    }
}
