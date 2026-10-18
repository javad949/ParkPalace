using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly ICrmDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public RolesController(ICrmDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<List<RoleDto>>> Get(CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = 401,
                Title = "احراز هویت نشده",
                Detail = "دسترسی نیازمند ورود به سیستم است."
            });
        }

        // Require Admin role or AdminUsers permission
        if (!_currentUserService.IsInRole(Roles.Admin) && !_currentUserService.HasPermission(Permissions.AdminUsers))
        {
            return StatusCode(403, new ProblemDetails
            {
                Status = 403,
                Title = "عدم دسترسی",
                Detail = "مشاهده لیست نقش‌ها و مجوزها نیازمند دسترسی مدیریت است."
            });
        }

        var roles = await _dbContext.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .Select(r => new RoleDto
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                Permissions = r.RolePermissions.Select(rp => rp.Permission.Code).ToList()
            })
            .ToListAsync(cancellationToken);

        return Ok(roles);
    }
}
