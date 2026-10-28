using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ICrmDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CategoriesController(ICrmDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetAll([FromQuery] Guid? parentId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized(new ProblemDetails { Title = "عدم احراز هویت", Detail = "لطفاً ابتدا وارد سیستم شوید" });

        if (!_currentUserService.HasPermission(Permissions.MaterialsView))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var query = _dbContext.Categories.AsQueryable();
        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        if (parentId.HasValue)
            query = query.Where(c => c.ParentId == parentId.Value);

        var list = await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                ParentId = c.ParentId,
                Name = c.Name,
                Code = c.Code,
                Description = c.Description,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive,
                MaterialCount = c.Materials.Count(m => m.IsActive),
                ChildrenCount = c.Children.Count(ch => ch.IsActive)
            })
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpGet("tree")]
    public async Task<ActionResult<List<CategoryTreeDto>>> GetTree([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized(new ProblemDetails { Title = "عدم احراز هویت", Detail = "لطفاً ابتدا وارد سیستم شوید" });

        if (!_currentUserService.HasPermission(Permissions.MaterialsView))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var query = _dbContext.Categories.Include(c => c.Materials).AsQueryable();
        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        var allCategories = await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

        var lookup = allCategories.ToLookup(c => c.ParentId);

        List<CategoryTreeDto> BuildSubTree(Guid? pId)
        {
            return lookup[pId].Select(c => new CategoryTreeDto
            {
                Id = c.Id,
                ParentId = c.ParentId,
                Name = c.Name,
                Code = c.Code,
                Description = c.Description,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive,
                MaterialCount = c.Materials.Count(m => m.IsActive),
                Children = BuildSubTree(c.Id)
            }).ToList();
        }

        var tree = BuildSubTree(null);
        return Ok(tree);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryDto>> GetById(Guid id, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized(new ProblemDetails { Title = "عدم احراز هویت" });

        if (!_currentUserService.HasPermission(Permissions.MaterialsView))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var c = await _dbContext.Categories
            .Include(x => x.Materials)
            .Include(x => x.Children)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (c == null)
            return NotFound(new ProblemDetails { Title = "یافت نشد", Detail = "دسته‌بندی مورد نظر یافت نشد" });

        return Ok(new CategoryDto
        {
            Id = c.Id,
            ParentId = c.ParentId,
            Name = c.Name,
            Code = c.Code,
            Description = c.Description,
            SortOrder = c.SortOrder,
            IsActive = c.IsActive,
            MaterialCount = c.Materials.Count(m => m.IsActive),
            ChildrenCount = c.Children.Count(ch => ch.IsActive)
        });
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create([FromBody] CreateCategoryRequest req, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        // Check code uniqueness
        var codeExists = await _dbContext.Categories.AnyAsync(c => c.Code == req.Code.Trim(), ct);
        if (codeExists)
            return Conflict(new ProblemDetails { Title = "تداخل داده", Detail = $"کد دسته‌بندی '{req.Code}' قبلاً ثبت شده است" });

        // Check parent if provided
        if (req.ParentId.HasValue)
        {
            var parentExists = await _dbContext.Categories.AnyAsync(c => c.Id == req.ParentId.Value, ct);
            if (!parentExists)
                return BadRequest(new ProblemDetails { Title = "داده نامعتبر", Detail = "دسته‌بندی والد مشخص شده وجود ندارد" });
        }

        var category = new Category
        {
            Name = req.Name.Trim(),
            Code = req.Code.Trim(),
            Description = req.Description?.Trim(),
            SortOrder = req.SortOrder,
            ParentId = req.ParentId,
            IsActive = true
        };

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = category.Id }, new CategoryDto
        {
            Id = category.Id,
            ParentId = category.ParentId,
            Name = category.Name,
            Code = category.Code,
            Description = category.Description,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CategoryDto>> Update(Guid id, [FromBody] UpdateCategoryRequest req, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category == null)
            return NotFound(new ProblemDetails { Title = "یافت نشد", Detail = "دسته‌بندی مورد نظر یافت نشد" });

        category.Name = req.Name.Trim();
        category.Description = req.Description?.Trim();
        category.SortOrder = req.SortOrder;
        category.IsActive = req.IsActive;

        await _dbContext.SaveChangesAsync(ct);

        return Ok(new CategoryDto
        {
            Id = category.Id,
            ParentId = category.ParentId,
            Name = category.Name,
            Code = category.Code,
            Description = category.Description,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive
        });
    }

    [HttpPost("{id:guid}/move")]
    public async Task<ActionResult> Move(Guid id, [FromBody] MoveCategoryRequest req, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category == null)
            return NotFound(new ProblemDetails { Title = "یافت نشد", Detail = "دسته‌بندی مورد نظر یافت نشد" });

        // Cycle and self-parent prevention
        if (req.NewParentId.HasValue)
        {
            if (req.NewParentId.Value == id)
                return BadRequest(new ProblemDetails { Title = "حلقه نامعتبر", Detail = "یک دسته‌بندی نمی‌تواند والد خودش باشد" });

            // Check if NewParentId exists
            var newParent = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == req.NewParentId.Value, ct);
            if (newParent == null)
                return BadRequest(new ProblemDetails { Title = "داده نامعتبر", Detail = "والد جدید یافت نشد" });

            // Check if NewParentId is a descendant of this category (would create a cycle)
            var allCats = await _dbContext.Categories.ToListAsync(ct);
            var current = newParent;
            while (current.ParentId.HasValue)
            {
                if (current.ParentId.Value == id)
                {
                    return BadRequest(new ProblemDetails { Title = "تشکیل حلقه (Cycle Detected)", Detail = "نمی‌توان یک دسته‌بندی را به زیرمجموعه‌های خودش منتقل کرد" });
                }
                current = allCats.FirstOrDefault(c => c.Id == current.ParentId.Value);
                if (current == null) break;
            }
        }

        category.ParentId = req.NewParentId;
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new { message = "دسته‌بندی با موفقیت منتقل گردید", id = category.Id, newParentId = category.ParentId });
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var category = await _dbContext.Categories
            .Include(c => c.Children)
            .Include(c => c.Materials)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (category == null)
            return NotFound();

        // Soft delete / deactivate
        category.IsActive = false;
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new { message = "دسته‌بندی با موفقیت غیرفعال شد", id });
    }
}
