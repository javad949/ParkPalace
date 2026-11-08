using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly ICrmDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITelephonyAccessControlService _accessControlService;

    public RolesController(
        ICrmDbContext dbContext,
        ICurrentUserService currentUserService,
        ITelephonyAccessControlService accessControlService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _accessControlService = accessControlService;
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

    [HttpPut("{id:guid}/permissions")]
    public async Task<ActionResult> UpdatePermissions(Guid id, [FromBody] UpdateRolePermissionsRequest req, CancellationToken cancellationToken)
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

        if (!_currentUserService.IsInRole(Roles.Admin) && !_currentUserService.HasPermission(Permissions.AdminUsers))
        {
            return StatusCode(403, new ProblemDetails
            {
                Status = 403,
                Title = "عدم دسترسی",
                Detail = "تغییر مجوز نقش‌ها نیازمند دسترسی مدیریت است."
            });
        }

        var role = await _dbContext.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = 404,
                Title = "یافت نشد",
                Detail = "نقش مورد نظر یافت نشد."
            });
        }

        var requested = (req.Permissions ?? new List<string>())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var permissions = await _dbContext.Permissions
            .Where(p => requested.Contains(p.Code))
            .ToListAsync(cancellationToken);

        _dbContext.RolePermissions.RemoveRange(role.RolePermissions);
        foreach (var permission in permissions)
        {
            _dbContext.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permission.Id,
                GrantedAtUtc = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _accessControlService.SyncActiveConnectionsAsync(cancellationToken);

        return Ok(new
        {
            message = "مجوزهای نقش به‌روزرسانی شد.",
            permissions = permissions.Select(p => p.Code).ToList()
        });
    }
}
