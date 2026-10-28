using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/v1/inventory")]
public class InventoryController : ControllerBase
{
    private readonly ICrmDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IInventorySyncService _syncService;
    private readonly IConsumptionCalculator _consumptionCalculator;

    public InventoryController(
        ICrmDbContext dbContext,
        ICurrentUserService currentUserService,
        IInventorySyncService syncService,
        IConsumptionCalculator consumptionCalculator)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _syncService = syncService;
        _consumptionCalculator = consumptionCalculator;
    }

    private bool HasPermission(string permission)
    {
        return _currentUserService.Roles.Contains(Roles.Admin) || 
               _currentUserService.Permissions.Contains(permission);
    }

    [HttpGet("warehouse")]
    public async Task<ActionResult<WarehouseDto>> GetWarehouse(CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.InventoryView) && !HasPermission(Permissions.WarehouseView))
        {
            return StatusCode(403, new ProblemDetails
            {
                Title = "دسترسی غیرمجاز",
                Detail = "شما مجوز مشاهده اطلاعات انبار را ندارید.",
                Status = 403
            });
        }

        var warehouse = await _dbContext.Warehouses
            .FirstOrDefaultAsync(w => w.Code == "WH-MAIN", cancellationToken);

        if (warehouse == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "انبار یافت نشد",
                Detail = "انبار مرکزی پیکربندی نشده است.",
                Status = 404
            });
        }

        var totalItems = await _dbContext.InventorySnapshots
            .CountAsync(s => s.WarehouseId == warehouse.Id, cancellationToken);

        var criticalItems = await _dbContext.InventorySnapshots
            .Include(s => s.Material)
            .CountAsync(s => s.WarehouseId == warehouse.Id && s.OnHandQuantity <= (s.Material!.SafetyStock ?? 0m), cancellationToken);

        return Ok(new WarehouseDto
        {
            Id = warehouse.Id,
            Code = warehouse.Code,
            Name = warehouse.Name,
            Description = warehouse.Description,
            IsActive = warehouse.IsActive,
            LastSyncUtc = warehouse.LastSyncUtc,
            TotalItemsCount = totalItems,
            CriticalItemsCount = criticalItems
        });
    }

    [HttpGet("items")]
    public async Task<ActionResult<PagedResult<InventoryItemDto>>> GetItems(
        [FromQuery] string? search,
        [FromQuery] Guid? categoryId,
        [FromQuery] bool? isBelowSafetyStock,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (!HasPermission(Permissions.InventoryView) && !HasPermission(Permissions.WarehouseView))
        {
            return StatusCode(403, new ProblemDetails
            {
                Title = "دسترسی غیرمجاز",
                Detail = "شما مجوز مشاهده موجودی اقلام را ندارید.",
                Status = 403
            });
        }

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var warehouse = await _dbContext.Warehouses.FirstOrDefaultAsync(w => w.Code == "WH-MAIN", cancellationToken);
        var warehouseId = warehouse?.Id ?? Guid.Empty;

        var query = _dbContext.Materials
            .Include(m => m.Category)
            .Include(m => m.BaseUnit)
            .Where(m => m.IsActive)
            .AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(m => m.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var sUpper = s.ToUpperInvariant();
            query = query.Where(m => m.Name.Contains(s) || m.InternalCode.Contains(sUpper) || (m.Brand != null && m.Brand.Contains(s)));
        }

        var materials = await query.ToListAsync(cancellationToken);
        var materialIds = materials.Select(m => m.Id).ToList();

        var snapshots = await _dbContext.InventorySnapshots
            .Where(s => s.WarehouseId == warehouseId && materialIds.Contains(s.MaterialId))
            .ToDictionaryAsync(s => s.MaterialId, cancellationToken);

        var now = DateTime.UtcNow;
        var items = new List<InventoryItemDto>();

        foreach (var mat in materials)
        {
            snapshots.TryGetValue(mat.Id, out var snap);
            decimal onHand = snap?.OnHandQuantity ?? 0m;
            decimal reservable = snap?.ReservableQuantity ?? onHand;
            decimal safetyStock = mat.SafetyStock ?? 0m;
            decimal minStock = mat.MinStock ?? 0m;

            bool belowSafety = onHand <= safetyStock;
            if (isBelowSafetyStock.HasValue && isBelowSafetyStock.Value && !belowSafety)
            {
                continue;
            }

            // Get pre-calculated or instant consumption
            decimal daily7d = await _consumptionCalculator.GetAverageDailyConsumptionAsync(mat.Id, 7, now, cancellationToken);
            decimal daily30d = await _consumptionCalculator.GetAverageDailyConsumptionAsync(mat.Id, 30, now, cancellationToken);

            bool isStale = snap == null || snap.IsStale || (now - snap.SnapshotTimeUtc).TotalHours > 24;
            bool isUnitMismatch = snap != null && snap.SourceSnapshotKey != null && snap.SourceSnapshotKey.Contains(":UNIT-MISMATCH");
            string stockStatus = isUnitMismatch ? "UnitMismatch"
                : (belowSafety ? "Critical" : (onHand <= 0 ? "OutOfStock" : (isStale ? "Stale" : "Normal")));

            items.Add(new InventoryItemDto
            {
                MaterialId = mat.Id,
                InternalCode = mat.InternalCode,
                Name = mat.Name,
                CategoryName = mat.Category?.Name ?? "-",
                UnitName = mat.BaseUnit?.Name ?? "کیلوگرم",
                UnitCode = mat.BaseUnit?.Code ?? "KG",
                OnHandQuantity = isUnitMismatch ? 0m : onHand,
                ReservableQuantity = isUnitMismatch ? 0m : reservable,
                SafetyStock = safetyStock,
                MinStock = minStock,
                DailyConsumption7d = isUnitMismatch ? 0m : Math.Round(daily7d, 2),
                DailyConsumption30d = isUnitMismatch ? 0m : Math.Round(daily30d, 2),
                IsBelowSafetyStock = !isUnitMismatch && belowSafety,
                IsStale = isStale || isUnitMismatch,
                IsUnitMismatch = isUnitMismatch,
                StockStatus = stockStatus,
                LastSyncUtc = snap?.SnapshotTimeUtc
            });
        }

        int totalCount = items.Count;
        var pagedItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Ok(new PagedResult<InventoryItemDto>
        {
            Items = pagedItems,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    [HttpGet("items/{id:guid}")]
    public async Task<ActionResult<InventoryItemDetailDto>> GetItemDetail(Guid id, CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.InventoryView) && !HasPermission(Permissions.WarehouseView))
        {
            return StatusCode(403, new ProblemDetails { Title = "دسترسی غیرمجاز", Status = 403 });
        }

        var mat = await _dbContext.Materials
            .Include(m => m.Category)
            .Include(m => m.BaseUnit)
            .Include(m => m.SupplierMaterials)
                .ThenInclude(sm => sm.Party)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (mat == null)
        {
            return NotFound(new ProblemDetails { Title = "کالا یافت نشد", Status = 404 });
        }

        var warehouse = await _dbContext.Warehouses.FirstOrDefaultAsync(w => w.Code == "WH-MAIN", cancellationToken);
        var warehouseId = warehouse?.Id ?? Guid.Empty;

        var snap = await _dbContext.InventorySnapshots
            .FirstOrDefaultAsync(s => s.MaterialId == id && s.WarehouseId == warehouseId, cancellationToken);

        var mapping = await _dbContext.InventoryItemMappings
            .FirstOrDefaultAsync(m => m.MaterialId == id, cancellationToken);

        var policy = await _dbContext.ReplenishmentPolicies
            .Include(p => p.PreferredSupplier)
            .FirstOrDefaultAsync(p => p.MaterialId == id, cancellationToken);

        var now = DateTime.UtcNow;
        decimal onHand = snap?.OnHandQuantity ?? 0m;
        decimal reservable = snap?.ReservableQuantity ?? onHand;
        decimal safetyStock = policy?.SafetyStock ?? mat.SafetyStock ?? 0m;
        decimal minStock = mat.MinStock ?? 0m;

        decimal daily7d = await _consumptionCalculator.GetAverageDailyConsumptionAsync(id, 7, now, cancellationToken);
        decimal daily30d = await _consumptionCalculator.GetAverageDailyConsumptionAsync(id, 30, now, cancellationToken);

        bool isStale = snap == null || snap.IsStale || (now - snap.SnapshotTimeUtc).TotalHours > 24;

        var suppliers = mat.SupplierMaterials.Select(sm => new SupplierMaterialSummaryDto
        {
            SupplierId = sm.PartyId,
            SupplierName = sm.Party?.DisplayName ?? "-",
            IsPreferred = sm.IsPreferred,
            Status = sm.Status
        }).ToList();

        return Ok(new InventoryItemDetailDto
        {
            MaterialId = mat.Id,
            InternalCode = mat.InternalCode,
            Name = mat.Name,
            CategoryName = mat.Category?.Name ?? "-",
            UnitName = mat.BaseUnit?.Name ?? "کیلوگرم",
            UnitCode = mat.BaseUnit?.Code ?? "KG",
            OnHandQuantity = onHand,
            ReservableQuantity = reservable,
            SafetyStock = safetyStock,
            MinStock = minStock,
            DailyConsumption7d = Math.Round(daily7d, 2),
            DailyConsumption30d = Math.Round(daily30d, 2),
            IsBelowSafetyStock = onHand <= safetyStock,
            IsStale = isStale,
            LastSyncUtc = snap?.SnapshotTimeUtc,
            Brand = mat.Brand,
            Specifications = mat.Specifications,
            Description = mat.Description,
            LeadTimeDays = policy?.LeadTimeDays ?? mat.LeadTimeDays ?? 2,
            IncomingQuantity = snap?.IncomingQuantity ?? 0m,
            ReservedQuantity = snap?.ReservedQuantity ?? 0m,
            SepidarItemCode = mapping?.SepidarItemCode,
            SepidarItemId = mapping?.SepidarItemId,
            ReplenishmentPolicy = policy == null ? null : new ReplenishmentPolicyDto
            {
                MaterialId = policy.MaterialId,
                MaterialName = mat.Name,
                ReviewPeriodDays = policy.ReviewPeriodDays,
                LeadTimeDays = policy.LeadTimeDays,
                SafetyStock = policy.SafetyStock,
                MinOrderQuantity = policy.MinOrderQuantity,
                PackageMultiple = policy.PackageMultiple,
                PreferredSupplierId = policy.PreferredSupplierId,
                PreferredSupplierName = policy.PreferredSupplier?.DisplayName,
                Notes = policy.Notes
            },
            Suppliers = suppliers
        });
    }

    [HttpGet("items/{id:guid}/movements")]
    public async Task<ActionResult<PagedResult<InventoryMovementDto>>> GetMovements(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        CancellationToken cancellationToken = default)
    {
        if (!HasPermission(Permissions.InventoryView) && !HasPermission(Permissions.WarehouseView))
        {
            return StatusCode(403, new ProblemDetails { Title = "دسترسی غیرمجاز", Status = 403 });
        }

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 15;

        var query = _dbContext.InventoryMovements
            .Include(m => m.Unit)
            .Where(m => m.MaterialId == id)
            .OrderByDescending(m => m.TransactionDateUtc);

        int totalCount = await query.CountAsync(cancellationToken);
        var movs = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var dtos = movs.Select(m => new InventoryMovementDto
        {
            Id = m.Id,
            VoucherNumber = m.VoucherNumber,
            MovementType = m.MovementType switch
            {
                InventoryMovementType.Consumption => "حواله خروج به خط تولید",
                InventoryMovementType.Receipt => "رسید ورود از خرید",
                InventoryMovementType.Transfer => "انتقال بین انبارها",
                InventoryMovementType.Adjustment => "تعدیل انبارگردانی",
                InventoryMovementType.Waste => "ضایعات خط تولید",
                InventoryMovementType.Return => "مرجوعی به فروشنده",
                _ => "سایر"
            },
            MovementTypeValue = (int)m.MovementType,
            Quantity = m.Quantity,
            UnitName = m.Unit?.Name ?? "کیلوگرم",
            UnitCode = m.Unit?.Code ?? "KG",
            TransactionDateUtc = m.TransactionDateUtc,
            PostingStatus = m.PostingStatus,
            IsCancelled = m.IsCancelled,
            ReferenceDocument = m.ReferenceDocument
        }).ToList();

        return Ok(new PagedResult<InventoryMovementDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    [HttpGet("items/{id:guid}/consumption")]
    public async Task<ActionResult<InventoryConsumptionDto>> GetConsumption(Guid id, CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.InventoryView) && !HasPermission(Permissions.WarehouseView))
        {
            return StatusCode(403, new ProblemDetails { Title = "دسترسی غیرمجاز", Status = 403 });
        }

        try
        {
            var res = await _consumptionCalculator.CalculateConsumptionAsync(id, DateTime.UtcNow, cancellationToken);
            return Ok(res);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails { Title = "ماده اولیه یافت نشد", Status = 404 });
        }
    }

    [HttpGet("sync/status")]
    public async Task<ActionResult<SyncStatusDto>> GetSyncStatus(CancellationToken cancellationToken)
    {
        var status = await _syncService.GetSyncStatusAsync(cancellationToken);
        return Ok(status);
    }

    [HttpPost("sync")]
    public async Task<ActionResult<SyncJobDto>> TriggerSync(CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.InventorySync) && !HasPermission(Permissions.WarehouseReceive) && !HasPermission(Permissions.OrdersCreate))
        {
            return StatusCode(403, new ProblemDetails
            {
                Title = "دسترسی غیرمجاز",
                Detail = "شما مجوز همگام‌سازی اطلاعات انبار را ندارید.",
                Status = 403
            });
        }

        try
        {
            var user = _currentUserService.Username ?? "ManualTrigger";
            var job = await _syncService.TriggerSyncAsync(SyncJobType.Manual, user, cancellationToken);
            return Ok(job);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "تداخل اجرای همگام‌سازی",
                Detail = ex.Message,
                Status = 409
            });
        }
    }

    [HttpPost("sync/dry-run")]
    public async Task<ActionResult<SyncDryRunResultDto>> ExecuteDryRun([FromQuery] string warehouseCode = "WH-MAIN", CancellationToken cancellationToken = default)
    {
        if (!HasPermission(Permissions.InventorySync) && !HasPermission(Permissions.InventoryView))
        {
            return StatusCode(403, new ProblemDetails { Title = "دسترسی غیرمجاز", Status = 403 });
        }

        try
        {
            var result = await _syncService.ExecuteDryRunAsync(warehouseCode, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "خطا در اجرای آزمایشی (Dry Run)", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpGet("replenishment-policies")]
    public async Task<ActionResult<List<ReplenishmentPolicyDto>>> GetReplenishmentPolicies(CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.PoliciesManage) && !HasPermission(Permissions.InventoryView) && !HasPermission(Permissions.OrdersView))
        {
            return StatusCode(403, new ProblemDetails { Title = "دسترسی غیرمجاز", Status = 403 });
        }

        var policies = await _dbContext.ReplenishmentPolicies
            .Include(p => p.Material)
            .Include(p => p.PreferredSupplier)
            .Where(p => p.IsActive)
            .ToListAsync(cancellationToken);

        var list = policies.Select(p => new ReplenishmentPolicyDto
        {
            MaterialId = p.MaterialId,
            MaterialName = p.Material?.Name ?? "-",
            ReviewPeriodDays = p.ReviewPeriodDays,
            LeadTimeDays = p.LeadTimeDays,
            SafetyStock = p.SafetyStock,
            MinOrderQuantity = p.MinOrderQuantity,
            PackageMultiple = p.PackageMultiple,
            PreferredSupplierId = p.PreferredSupplierId,
            PreferredSupplierName = p.PreferredSupplier?.DisplayName,
            Notes = p.Notes
        }).ToList();

        return Ok(list);
    }

    [HttpPut("replenishment-policies/{materialId:guid}")]
    public async Task<ActionResult<ReplenishmentPolicyDto>> UpdateReplenishmentPolicy(
        Guid materialId, 
        [FromBody] UpdateReplenishmentPolicyRequest req, 
        CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.PoliciesManage))
        {
            return StatusCode(403, new ProblemDetails
            {
                Title = "دسترسی غیرمجاز",
                Detail = "شما مجوز ویرایش خط‌مشی سفارش‌دهی را ندارید.",
                Status = 403
            });
        }

        var material = await _dbContext.Materials.FirstOrDefaultAsync(m => m.Id == materialId, cancellationToken);
        if (material == null)
        {
            return NotFound(new ProblemDetails { Title = "ماده اولیه یافت نشد", Status = 404 });
        }

        if (req.ReviewPeriodDays < 1) req.ReviewPeriodDays = 1;
        if (req.LeadTimeDays < 0) req.LeadTimeDays = 0;
        if (req.SafetyStock < 0) req.SafetyStock = 0;
        if (req.MinOrderQuantity < 0) req.MinOrderQuantity = 0;

        var policy = await _dbContext.ReplenishmentPolicies
            .Include(p => p.PreferredSupplier)
            .FirstOrDefaultAsync(p => p.MaterialId == materialId, cancellationToken);

        if (policy == null)
        {
            policy = new ReplenishmentPolicy
            {
                MaterialId = materialId,
                ReviewPeriodDays = req.ReviewPeriodDays,
                LeadTimeDays = req.LeadTimeDays,
                SafetyStock = req.SafetyStock,
                MinOrderQuantity = req.MinOrderQuantity,
                PackageMultiple = req.PackageMultiple,
                PreferredSupplierId = req.PreferredSupplierId,
                Notes = req.Notes,
                UpdatedBy = _currentUserService.Username ?? "User",
                IsActive = true
            };
            _dbContext.ReplenishmentPolicies.Add(policy);
        }
        else
        {
            policy.ReviewPeriodDays = req.ReviewPeriodDays;
            policy.LeadTimeDays = req.LeadTimeDays;
            policy.SafetyStock = req.SafetyStock;
            policy.MinOrderQuantity = req.MinOrderQuantity;
            policy.PackageMultiple = req.PackageMultiple;
            policy.PreferredSupplierId = req.PreferredSupplierId;
            policy.Notes = req.Notes;
            policy.UpdatedBy = _currentUserService.Username ?? "User";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ReplenishmentPolicyDto
        {
            MaterialId = policy.MaterialId,
            MaterialName = material.Name,
            ReviewPeriodDays = policy.ReviewPeriodDays,
            LeadTimeDays = policy.LeadTimeDays,
            SafetyStock = policy.SafetyStock,
            MinOrderQuantity = policy.MinOrderQuantity,
            PackageMultiple = policy.PackageMultiple,
            PreferredSupplierId = policy.PreferredSupplierId,
            PreferredSupplierName = policy.PreferredSupplier?.DisplayName,
            Notes = policy.Notes
        });
    }
}
