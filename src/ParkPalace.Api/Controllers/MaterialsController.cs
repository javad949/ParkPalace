using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class MaterialsController : ControllerBase
{
    private readonly ICrmDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public MaterialsController(ICrmDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<MaterialDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] Guid? categoryId,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsView))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var query = _dbContext.Materials
            .Include(m => m.Category)
            .Include(m => m.BaseUnit)
            .Include(m => m.SupplierMaterials)
            .Include(m => m.UnitConversions)
            .AsQueryable();

        if (!includeInactive)
            query = query.Where(m => m.IsActive);

        if (categoryId.HasValue)
        {
            // Include items in this category or any child category
            var childCatIds = await _dbContext.Categories
                .Where(c => c.ParentId == categoryId.Value || c.Id == categoryId.Value)
                .Select(c => c.Id)
                .ToListAsync(ct);

            query = query.Where(m => childCatIds.Contains(m.CategoryId));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var sUpper = s.ToUpperInvariant();
            query = query.Where(m => m.Name.Contains(s) || m.InternalCode.Contains(s) || m.InternalCode.Contains(sUpper) || (m.Brand != null && m.Brand.Contains(s)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(m => m.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MaterialDto
            {
                Id = m.Id,
                InternalCode = m.InternalCode,
                Name = m.Name,
                CategoryId = m.CategoryId,
                CategoryName = m.Category != null ? m.Category.Name : string.Empty,
                BaseUnitId = m.BaseUnitId,
                BaseUnitName = m.BaseUnit != null ? m.BaseUnit.Name : string.Empty,
                BaseUnitCode = m.BaseUnit != null ? m.BaseUnit.Code : string.Empty,
                Description = m.Description,
                Specifications = m.Specifications,
                Brand = m.Brand,
                MinStock = m.MinStock,
                SafetyStock = m.SafetyStock,
                LeadTimeDays = m.LeadTimeDays,
                IsActive = m.IsActive,
                SupplierCount = m.SupplierMaterials.Count(sm => sm.Status == "Active"),
                ConversionCount = m.UnitConversions.Count
            })
            .ToListAsync(ct);

        return Ok(new PagedResult<MaterialDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaterialDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsView))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var m = await _dbContext.Materials
            .Include(x => x.Category)
            .Include(x => x.BaseUnit)
            .Include(x => x.UnitConversions).ThenInclude(c => c.FromUnit)
            .Include(x => x.UnitConversions).ThenInclude(c => c.ToUnit)
            .Include(x => x.ExternalMappings)
            .Include(x => x.SupplierMaterials).ThenInclude(sm => sm.Party)
            .Include(x => x.SupplierMaterials).ThenInclude(sm => sm.PurchaseUnit)
            .Include(x => x.SupplierMaterials).ThenInclude(sm => sm.PriceHistories).ThenInclude(ph => ph.Unit)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (m == null)
            return NotFound(new ProblemDetails { Title = "یافت نشد", Detail = "ماده اولیه مورد نظر یافت نشد" });

        var detail = new MaterialDetailDto
        {
            Id = m.Id,
            InternalCode = m.InternalCode,
            Name = m.Name,
            CategoryId = m.CategoryId,
            CategoryName = m.Category?.Name ?? string.Empty,
            BaseUnitId = m.BaseUnitId,
            BaseUnitName = m.BaseUnit?.Name ?? string.Empty,
            BaseUnitCode = m.BaseUnit?.Code ?? string.Empty,
            Description = m.Description,
            Specifications = m.Specifications,
            Brand = m.Brand,
            MinStock = m.MinStock,
            SafetyStock = m.SafetyStock,
            LeadTimeDays = m.LeadTimeDays,
            IsActive = m.IsActive,
            SupplierCount = m.SupplierMaterials.Count,
            ConversionCount = m.UnitConversions.Count,
            UnitConversions = m.UnitConversions.Select(c => new MaterialUnitConversionDto
            {
                Id = c.Id,
                MaterialId = c.MaterialId,
                FromUnitId = c.FromUnitId,
                FromUnitName = c.FromUnit?.Name ?? string.Empty,
                FromUnitCode = c.FromUnit?.Code ?? string.Empty,
                ToUnitId = c.ToUnitId,
                ToUnitName = c.ToUnit?.Name ?? string.Empty,
                ToUnitCode = c.ToUnit?.Code ?? string.Empty,
                Factor = c.Factor
            }).ToList(),
            Suppliers = m.SupplierMaterials.Select(sm =>
            {
                var latestPrice = sm.PriceHistories
                    .OrderByDescending(ph => ph.EffectiveFrom)
                    .FirstOrDefault();

                return new SupplierMaterialSummaryDto
                {
                    SupplierId = sm.PartyId,
                    SupplierName = sm.Party?.DisplayName ?? string.Empty,
                    IsPreferred = sm.IsPreferred,
                    Status = sm.Status,
                    LatestPrice = latestPrice?.Price,
                    Currency = latestPrice?.Currency,
                    PriceUnitName = latestPrice?.Unit?.Name,
                    PriceEffectiveFrom = latestPrice?.EffectiveFrom
                };
            }).ToList(),
            ExternalMappings = m.ExternalMappings.Select(em => new ExternalItemMappingDto
            {
                Id = em.Id,
                SourceSystem = em.SourceSystem,
                SourceCompany = em.SourceCompany,
                ExternalItemId = em.ExternalItemId,
                ExternalItemCode = em.ExternalItemCode,
                LastSeenUtc = em.LastSeenUtc
            }).ToList()
        };

        return Ok(detail);
    }

    [HttpPost]
    public async Task<ActionResult<MaterialDto>> Create([FromBody] CreateMaterialRequest req, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var codeTrimmed = req.InternalCode.Trim().ToUpperInvariant();
        var codeExists = await _dbContext.Materials.AnyAsync(m => m.InternalCode == codeTrimmed, ct);
        if (codeExists)
            return Conflict(new ProblemDetails { Title = "تداخل داده", Detail = $"کد ماده اولیه '{codeTrimmed}' قبلاً ثبت شده است" });

        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == req.CategoryId, ct);
        if (category == null)
            return BadRequest(new ProblemDetails { Title = "داده نامعتبر", Detail = "دسته‌بندی مشخص شده وجود ندارد" });

        var unit = await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == req.BaseUnitId, ct);
        if (unit == null)
            return BadRequest(new ProblemDetails { Title = "داده نامعتبر", Detail = "واحد سنجش مبنا مشخص شده وجود ندارد" });

        var material = new Material
        {
            InternalCode = codeTrimmed,
            Name = req.Name.Trim(),
            CategoryId = req.CategoryId,
            BaseUnitId = req.BaseUnitId,
            Description = req.Description?.Trim(),
            Specifications = req.Specifications?.Trim(),
            Brand = req.Brand?.Trim(),
            MinStock = req.MinStock,
            SafetyStock = req.SafetyStock,
            LeadTimeDays = req.LeadTimeDays,
            IsActive = true
        };

        _dbContext.Materials.Add(material);
        await _dbContext.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = material.Id }, new MaterialDto
        {
            Id = material.Id,
            InternalCode = material.InternalCode,
            Name = material.Name,
            CategoryId = material.CategoryId,
            CategoryName = category.Name,
            BaseUnitId = material.BaseUnitId,
            BaseUnitName = unit.Name,
            BaseUnitCode = unit.Code,
            Description = material.Description,
            Specifications = material.Specifications,
            Brand = material.Brand,
            MinStock = material.MinStock,
            SafetyStock = material.SafetyStock,
            LeadTimeDays = material.LeadTimeDays,
            IsActive = material.IsActive
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MaterialDto>> Update(Guid id, [FromBody] UpdateMaterialRequest req, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var material = await _dbContext.Materials
            .Include(m => m.Category)
            .Include(m => m.BaseUnit)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (material == null)
            return NotFound(new ProblemDetails { Title = "یافت نشد", Detail = "ماده اولیه مورد نظر یافت نشد" });

        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == req.CategoryId, ct);
        if (category == null)
            return BadRequest(new ProblemDetails { Title = "داده نامعتبر", Detail = "دسته‌بندی مشخص شده وجود ندارد" });

        var unit = await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == req.BaseUnitId, ct);
        if (unit == null)
            return BadRequest(new ProblemDetails { Title = "داده نامعتبر", Detail = "واحد سنجش مبنا مشخص شده وجود ندارد" });

        material.Name = req.Name.Trim();
        material.CategoryId = req.CategoryId;
        material.BaseUnitId = req.BaseUnitId;
        material.Description = req.Description?.Trim();
        material.Specifications = req.Specifications?.Trim();
        material.Brand = req.Brand?.Trim();
        material.MinStock = req.MinStock;
        material.SafetyStock = req.SafetyStock;
        material.LeadTimeDays = req.LeadTimeDays;
        material.IsActive = req.IsActive;

        await _dbContext.SaveChangesAsync(ct);

        return Ok(new MaterialDto
        {
            Id = material.Id,
            InternalCode = material.InternalCode,
            Name = material.Name,
            CategoryId = material.CategoryId,
            CategoryName = category.Name,
            BaseUnitId = material.BaseUnitId,
            BaseUnitName = unit.Name,
            BaseUnitCode = unit.Code,
            Description = material.Description,
            Specifications = material.Specifications,
            Brand = material.Brand,
            MinStock = material.MinStock,
            SafetyStock = material.SafetyStock,
            LeadTimeDays = material.LeadTimeDays,
            IsActive = material.IsActive
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var material = await _dbContext.Materials.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (material == null)
            return NotFound();

        // Soft delete / deactivate
        material.IsActive = false;
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new { message = "ماده اولیه با موفقیت غیرفعال شد", id });
    }

    [HttpGet("{id:guid}/conversions")]
    public async Task<ActionResult<List<MaterialUnitConversionDto>>> GetConversions(Guid id, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsView))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var conversions = await _dbContext.MaterialUnitConversions
            .Include(c => c.FromUnit)
            .Include(c => c.ToUnit)
            .Where(c => c.MaterialId == id)
            .Select(c => new MaterialUnitConversionDto
            {
                Id = c.Id,
                MaterialId = c.MaterialId,
                FromUnitId = c.FromUnitId,
                FromUnitName = c.FromUnit != null ? c.FromUnit.Name : string.Empty,
                FromUnitCode = c.FromUnit != null ? c.FromUnit.Code : string.Empty,
                ToUnitId = c.ToUnitId,
                ToUnitName = c.ToUnit != null ? c.ToUnit.Name : string.Empty,
                ToUnitCode = c.ToUnit != null ? c.ToUnit.Code : string.Empty,
                Factor = c.Factor
            })
            .ToListAsync(ct);

        return Ok(conversions);
    }

    [HttpPost("{id:guid}/conversions")]
    public async Task<ActionResult<MaterialUnitConversionDto>> AddConversion(Guid id, [FromBody] CreateUnitConversionRequest req, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        if (req.FromUnitId == req.ToUnitId)
            return BadRequest(new ProblemDetails { Title = "داده نامعتبر", Detail = "واحد مبدا و مقصد تبدیل نمی‌توانند یکسان باشند" });

        if (req.Factor <= 0)
            return BadRequest(new ProblemDetails { Title = "داده نامعتبر", Detail = "ضریب تبدیل باید اکیداً بزرگتر از صفر باشد" });

        var material = await _dbContext.Materials.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (material == null)
            return NotFound(new ProblemDetails { Title = "یافت نشد", Detail = "ماده اولیه یافت نشد" });

        var fromUnit = await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == req.FromUnitId, ct);
        var toUnit = await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == req.ToUnitId, ct);
        if (fromUnit == null || toUnit == null)
            return BadRequest(new ProblemDetails { Title = "داده نامعتبر", Detail = "واحدهای مشخص شده معتبر نیستند" });

        var exists = await _dbContext.MaterialUnitConversions
            .AnyAsync(c => c.MaterialId == id && c.FromUnitId == req.FromUnitId && c.ToUnitId == req.ToUnitId, ct);
        if (exists)
            return Conflict(new ProblemDetails { Title = "تداخل داده", Detail = "این تبدیل واحد قبلاً برای این کالا ثبت شده است" });

        var conversion = new MaterialUnitConversion
        {
            MaterialId = id,
            FromUnitId = req.FromUnitId,
            ToUnitId = req.ToUnitId,
            Factor = req.Factor
        };

        _dbContext.MaterialUnitConversions.Add(conversion);
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new MaterialUnitConversionDto
        {
            Id = conversion.Id,
            MaterialId = conversion.MaterialId,
            FromUnitId = conversion.FromUnitId,
            FromUnitName = fromUnit.Name,
            FromUnitCode = fromUnit.Code,
            ToUnitId = conversion.ToUnitId,
            ToUnitName = toUnit.Name,
            ToUnitCode = toUnit.Code,
            Factor = conversion.Factor
        });
    }

    [HttpDelete("{id:guid}/conversions/{conversionId:guid}")]
    public async Task<ActionResult> DeleteConversion(Guid id, Guid conversionId, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var conv = await _dbContext.MaterialUnitConversions
            .FirstOrDefaultAsync(c => c.Id == conversionId && c.MaterialId == id, ct);

        if (conv == null)
            return NotFound();

        _dbContext.MaterialUnitConversions.Remove(conv);
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new { message = "تبدیل واحد با موفقیت حذف گردید" });
    }

    [HttpGet("{id:guid}/suppliers")]
    public async Task<ActionResult<List<SupplierMaterialSummaryDto>>> GetSuppliers(Guid id, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsView))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var suppliers = await _dbContext.SupplierMaterials
            .Include(sm => sm.Party)
            .Include(sm => sm.PriceHistories).ThenInclude(ph => ph.Unit)
            .Where(sm => sm.MaterialId == id)
            .Select(sm => new SupplierMaterialSummaryDto
            {
                SupplierId = sm.PartyId,
                SupplierName = sm.Party != null ? sm.Party.DisplayName : string.Empty,
                IsPreferred = sm.IsPreferred,
                Status = sm.Status,
                LatestPrice = sm.PriceHistories.OrderByDescending(p => p.EffectiveFrom).Select(p => (decimal?)p.Price).FirstOrDefault(),
                Currency = sm.PriceHistories.OrderByDescending(p => p.EffectiveFrom).Select(p => p.Currency).FirstOrDefault(),
                PriceUnitName = sm.PriceHistories.OrderByDescending(p => p.EffectiveFrom).Select(p => p.Unit != null ? p.Unit.Name : null).FirstOrDefault(),
                PriceEffectiveFrom = sm.PriceHistories.OrderByDescending(p => p.EffectiveFrom).Select(p => (DateTime?)p.EffectiveFrom).FirstOrDefault()
            })
            .ToListAsync(ct);

        return Ok(suppliers);
    }
}
