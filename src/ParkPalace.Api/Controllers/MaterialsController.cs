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
    private readonly ICatalogStandardizationEngine _catalogEngine;

    public MaterialsController(
        ICrmDbContext dbContext,
        ICurrentUserService currentUserService,
        ICatalogStandardizationEngine catalogEngine)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _catalogEngine = catalogEngine;
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
            .Include(m => m.PackageSizeUnit)
            .Include(m => m.PurchaseUnit)
            .Include(m => m.ConsumptionUnit)
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
                CategoryName = m.Category != null ? (m.Category.Parent != null ? m.Category.Parent.Name + " / " + m.Category.Name : m.Category.Name) : string.Empty,
                BaseUnitId = m.BaseUnitId,
                BaseUnitName = m.BaseUnit != null ? m.BaseUnit.Name : string.Empty,
                BaseUnitCode = m.BaseUnit != null ? m.BaseUnit.Code : string.Empty,
                Description = m.Description,
                Specifications = m.Specifications,
                Brand = m.Brand,
                Packaging = m.Packaging,
                PackageSize = m.PackageSize,
                PackageSizeUnitId = m.PackageSizeUnitId,
                PackageSizeUnitName = m.PackageSizeUnit != null ? m.PackageSizeUnit.Name : null,
                PackageSizeUnitCode = m.PackageSizeUnit != null ? m.PackageSizeUnit.Code : null,
                PurchaseUnitId = m.PurchaseUnitId,
                PurchaseUnitName = m.PurchaseUnit != null ? m.PurchaseUnit.Name : null,
                PurchaseUnitCode = m.PurchaseUnit != null ? m.PurchaseUnit.Code : null,
                ConsumptionUnitId = m.ConsumptionUnitId,
                ConsumptionUnitName = m.ConsumptionUnit != null ? m.ConsumptionUnit.Name : null,
                ConsumptionUnitCode = m.ConsumptionUnit != null ? m.ConsumptionUnit.Code : null,
                ProductFamily = m.ProductFamily,
                CategorizationStatus = m.CategorizationStatus,
                IsCustomizedByUser = m.IsCustomizedByUser,
                StandardizationNotes = m.StandardizationNotes,
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
            .Include(x => x.Category).ThenInclude(c => c!.Parent)
            .Include(x => x.BaseUnit)
            .Include(x => x.PackageSizeUnit)
            .Include(x => x.PurchaseUnit)
            .Include(x => x.ConsumptionUnit)
            .Include(x => x.UnitConversions).ThenInclude(c => c.FromUnit)
            .Include(x => x.UnitConversions).ThenInclude(c => c.ToUnit)
            .Include(x => x.ExternalMappings)
            .Include(x => x.SupplierMaterials).ThenInclude(sm => sm.Party)
            .Include(x => x.SupplierMaterials).ThenInclude(sm => sm.PurchaseUnit)
            .Include(x => x.SupplierMaterials).ThenInclude(sm => sm.PriceHistories).ThenInclude(ph => ph.Unit)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (m == null)
            return NotFound(new ProblemDetails { Title = "یافت نشد", Detail = "ماده اولیه مورد نظر یافت نشد" });

        var sepidarMapping = await _dbContext.InventoryItemMappings
            .FirstOrDefaultAsync(im => im.MaterialId == m.Id, ct);

        var detail = new MaterialDetailDto
        {
            Id = m.Id,
            InternalCode = m.InternalCode,
            Name = m.Name,
            CategoryId = m.CategoryId,
            CategoryName = m.Category != null ? (m.Category.Parent != null ? $"{m.Category.Parent.Name} / {m.Category.Name}" : m.Category.Name) : string.Empty,
            BaseUnitId = m.BaseUnitId,
            BaseUnitName = m.BaseUnit?.Name ?? string.Empty,
            BaseUnitCode = m.BaseUnit?.Code ?? string.Empty,
            Description = m.Description,
            Specifications = m.Specifications,
            Brand = m.Brand,
            Packaging = m.Packaging,
            PackageSize = m.PackageSize,
            PackageSizeUnitId = m.PackageSizeUnitId,
            PackageSizeUnitName = m.PackageSizeUnit?.Name,
            PackageSizeUnitCode = m.PackageSizeUnit?.Code,
            PurchaseUnitId = m.PurchaseUnitId,
            PurchaseUnitName = m.PurchaseUnit?.Name,
            PurchaseUnitCode = m.PurchaseUnit?.Code,
            ConsumptionUnitId = m.ConsumptionUnitId,
            ConsumptionUnitName = m.ConsumptionUnit?.Name,
            ConsumptionUnitCode = m.ConsumptionUnit?.Code,
            ProductFamily = m.ProductFamily,
            CategorizationStatus = m.CategorizationStatus,
            IsCustomizedByUser = m.IsCustomizedByUser,
            StandardizationNotes = m.StandardizationNotes,
            SepidarItemCode = sepidarMapping?.SepidarItemCode,
            SepidarItemTitle = sepidarMapping?.SepidarItemTitle,
            SepidarBaseUnit = sepidarMapping?.SepidarBaseUnit,
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
                Factor = c.Factor,
                IsConfirmed = c.IsConfirmed,
                Notes = c.Notes
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
            Packaging = req.Packaging?.Trim(),
            PackageSize = req.PackageSize,
            PackageSizeUnitId = req.PackageSizeUnitId,
            PurchaseUnitId = req.PurchaseUnitId ?? req.BaseUnitId,
            ConsumptionUnitId = req.ConsumptionUnitId,
            ProductFamily = req.ProductFamily?.Trim(),
            CategorizationStatus = "ManualConfirmed",
            IsCustomizedByUser = true,
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
            Packaging = material.Packaging,
            PackageSize = material.PackageSize,
            PackageSizeUnitId = material.PackageSizeUnitId,
            PurchaseUnitId = material.PurchaseUnitId,
            ConsumptionUnitId = material.ConsumptionUnitId,
            ProductFamily = material.ProductFamily,
            CategorizationStatus = material.CategorizationStatus,
            IsCustomizedByUser = material.IsCustomizedByUser,
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
            .Include(m => m.UnitConversions)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (material == null)
            return NotFound(new ProblemDetails { Title = "یافت نشد", Detail = "ماده اولیه مورد نظر یافت نشد" });

        var category = await _dbContext.Categories.Include(c => c.Parent).FirstOrDefaultAsync(c => c.Id == req.CategoryId, ct);
        if (category == null)
            return BadRequest(new ProblemDetails { Title = "داده نامعتبر", Detail = "دسته‌بندی مشخص شده وجود ندارد" });

        var unit = await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == req.BaseUnitId, ct);
        if (unit == null)
            return BadRequest(new ProblemDetails { Title = "داده نامعتبر", Detail = "واحد سنجش مبنا مشخص شده وجود ندارد" });

        // Safe Base Unit Change Validation (Requirement 3):
        if (req.BaseUnitId != material.BaseUnitId)
        {
            var (isAllowed, errorMsg) = await _catalogEngine.ValidateBaseUnitChangeAsync(id, req.BaseUnitId, ct);
            if (!isAllowed)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "تغییر واحد مبنا غیرمجاز است",
                    Detail = errorMsg,
                    Status = 400
                });
            }
        }

        material.Name = req.Name.Trim();
        material.CategoryId = req.CategoryId;
        material.BaseUnitId = req.BaseUnitId;
        material.Description = req.Description?.Trim();
        material.Specifications = req.Specifications?.Trim();
        material.Brand = req.Brand?.Trim();
        material.Packaging = req.Packaging?.Trim();
        material.PackageSize = req.PackageSize;
        material.PackageSizeUnitId = req.PackageSizeUnitId;
        material.PurchaseUnitId = req.PurchaseUnitId ?? req.BaseUnitId;
        material.ConsumptionUnitId = req.ConsumptionUnitId;
        material.ProductFamily = req.ProductFamily?.Trim();
        material.IsCustomizedByUser = true;
        material.CategorizationStatus = "ManualConfirmed";
        material.StandardizationNotes = req.StandardizationNotes?.Trim() ?? "ویرایش و اصلاح دستی توسط کاربر.";
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
            CategoryName = category.Parent != null ? $"{category.Parent.Name} / {category.Name}" : category.Name,
            BaseUnitId = material.BaseUnitId,
            BaseUnitName = unit.Name,
            BaseUnitCode = unit.Code,
            Description = material.Description,
            Specifications = material.Specifications,
            Brand = material.Brand,
            Packaging = material.Packaging,
            PackageSize = material.PackageSize,
            PackageSizeUnitId = material.PackageSizeUnitId,
            PurchaseUnitId = material.PurchaseUnitId,
            ConsumptionUnitId = material.ConsumptionUnitId,
            ProductFamily = material.ProductFamily,
            CategorizationStatus = material.CategorizationStatus,
            IsCustomizedByUser = material.IsCustomizedByUser,
            StandardizationNotes = material.StandardizationNotes,
            MinStock = material.MinStock,
            SafetyStock = material.SafetyStock,
            LeadTimeDays = material.LeadTimeDays,
            IsActive = material.IsActive
        });
    }

    [HttpPost("{id:guid}/standardize")]
    public async Task<ActionResult<StandardizedCatalogItemSuggestion>> Standardize(Guid id, [FromQuery] bool apply = false, CancellationToken ct = default)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var material = await _dbContext.Materials.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (material == null) return NotFound();

        var mapping = await _dbContext.InventoryItemMappings.FirstOrDefaultAsync(im => im.MaterialId == id, ct);
        string rawTitle = mapping?.SepidarItemTitle ?? material.Name;

        var suggestion = _catalogEngine.ParseAndStandardize(rawTitle, mapping?.SepidarBaseUnit);

        if (apply)
        {
            await _catalogEngine.ApplyStandardizationAsync(id, suggestion, isManualConfirmation: true, ct);
        }

        return Ok(suggestion);
    }

    [HttpGet("sepidar-corrections")]
    public async Task<ActionResult<List<SepidarCorrectionProposalDto>>> GetSepidarCorrections(CancellationToken ct = default)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsView))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var list = await _catalogEngine.GenerateSepidarCorrectionProposalsAsync(ct);
        return Ok(list);
    }

    [HttpPost("conversions/{conversionId:guid}/confirm")]
    public async Task<ActionResult> ConfirmConversion(Guid conversionId, CancellationToken ct = default)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var conv = await _dbContext.MaterialUnitConversions.FirstOrDefaultAsync(c => c.Id == conversionId, ct);
        if (conv == null) return NotFound();

        conv.IsConfirmed = true;
        conv.Notes = (conv.Notes ?? string.Empty) + " [تأییدشده توسط کاربر]";
        await _dbContext.SaveChangesAsync(ct);
        return NoContent();
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
                Factor = c.Factor,
                IsConfirmed = c.IsConfirmed,
                Notes = c.Notes
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
            Factor = req.Factor,
            IsConfirmed = req.IsConfirmed ?? false,
            Notes = req.Notes ?? "ثبت دستی ضریب تبدیل توسط کاربر"
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
            Factor = conversion.Factor,
            IsConfirmed = conversion.IsConfirmed,
            Notes = conversion.Notes
        });
    }

    [HttpPost("{id:guid}/conversions/{conversionId:guid}/confirm")]
    public async Task<ActionResult> ConfirmConversion(Guid id, Guid conversionId, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var conv = await _dbContext.MaterialUnitConversions
            .FirstOrDefaultAsync(c => c.Id == conversionId && c.MaterialId == id, ct);

        if (conv == null)
            return NotFound(new ProblemDetails { Title = "یافت نشد", Detail = "تبدیل واحد مورد نظر یافت نشد" });

        conv.IsConfirmed = true;
        conv.Notes = (conv.Notes ?? "") + " (تأیید رسمی توسط کاربر)";
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new { message = "تبدیل واحد با موفقیت تأیید شد و در محاسبات کاتالوگ، موجودی و سفارش فعال گردید." });
    }

    [HttpPost("{id:guid}/confirm-categorization")]
    public async Task<ActionResult> ConfirmCategorization(Guid id, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.MaterialsManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var material = await _dbContext.Materials.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (material == null)
            return NotFound(new ProblemDetails { Title = "یافت نشد", Detail = "ماده اولیه یافت نشد" });

        material.CategorizationStatus = "ManualConfirmed";
        material.IsCustomizedByUser = true;
        material.StandardizationNotes = (material.StandardizationNotes ?? "") + " (تأیید دسته‌بندی پیشنهادی توسط کاربر)";
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new { message = "دسته‌بندی و اطلاعات استاندارد کاتالوگ با موفقیت تأیید شد و در همگام‌سازی‌های بعدی سپیدار حفظ خواهد شد." });
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
