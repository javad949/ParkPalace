using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class UnitsController : ControllerBase
{
    private readonly ICrmDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public UnitsController(ICrmDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<List<UnitOfMeasureDto>>> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsView))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var query = _dbContext.UnitsOfMeasure.AsQueryable();
        if (!includeInactive)
            query = query.Where(u => u.IsActive);

        var list = await query
            .OrderBy(u => u.Dimension)
            .ThenBy(u => u.Name)
            .Select(u => new UnitOfMeasureDto
            {
                Id = u.Id,
                Code = u.Code,
                Name = u.Name,
                Precision = u.Precision,
                Dimension = u.Dimension,
                IsActive = u.IsActive
            })
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UnitOfMeasureDto>> GetById(Guid id, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsView))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var u = await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u == null)
            return NotFound();

        return Ok(new UnitOfMeasureDto
        {
            Id = u.Id,
            Code = u.Code,
            Name = u.Name,
            Precision = u.Precision,
            Dimension = u.Dimension,
            IsActive = u.IsActive
        });
    }

    [HttpPost]
    public async Task<ActionResult<UnitOfMeasureDto>> Create([FromBody] CreateUnitRequest req, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var codeUpper = req.Code.Trim().ToUpperInvariant();
        var exists = await _dbContext.UnitsOfMeasure.AnyAsync(u => u.Code == codeUpper, ct);
        if (exists)
            return Conflict(new ProblemDetails { Title = "تداخل داده", Detail = $"کد واحد '{codeUpper}' قبلاً ثبت شده است" });

        var unit = new UnitOfMeasure
        {
            Code = codeUpper,
            Name = req.Name.Trim(),
            Precision = req.Precision,
            Dimension = req.Dimension.Trim(),
            IsActive = true
        };

        _dbContext.UnitsOfMeasure.Add(unit);
        await _dbContext.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = unit.Id }, new UnitOfMeasureDto
        {
            Id = unit.Id,
            Code = unit.Code,
            Name = unit.Name,
            Precision = unit.Precision,
            Dimension = unit.Dimension,
            IsActive = unit.IsActive
        });
    }
}
