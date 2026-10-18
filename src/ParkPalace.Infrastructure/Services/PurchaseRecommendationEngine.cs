using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services;

public class PurchaseRecommendationEngine : IPurchaseRecommendationEngine
{
    private readonly ICrmDbContext _dbContext;
    private readonly IConsumptionCalculator _consumptionCalculator;

    public PurchaseRecommendationEngine(ICrmDbContext dbContext, IConsumptionCalculator consumptionCalculator)
    {
        _dbContext = dbContext;
        _consumptionCalculator = consumptionCalculator;
    }

    public async Task<List<PurchaseRecommendationDto>> GenerateAllRecommendationsAsync(
        Guid warehouseId, 
        CancellationToken cancellationToken = default)
    {
        var materials = await _dbContext.Materials
            .Include(m => m.Category)
            .Include(m => m.BaseUnit)
            .Include(m => m.SupplierMaterials)
                .ThenInclude(sm => sm.Party)
            .Where(m => m.IsActive)
            .ToListAsync(cancellationToken);

        var list = new List<PurchaseRecommendationDto>();
        foreach (var material in materials)
        {
            var rec = await GenerateRecommendationForMaterialAsync(material.Id, warehouseId, cancellationToken);
            if (rec != null)
            {
                list.Add(rec);
            }
        }

        return list.OrderByDescending(r => r.RoundedSuggestedQuantity > 0)
                   .ThenBy(r => r.MaterialName)
                   .ToList();
    }

    public async Task<PurchaseRecommendationDto?> GenerateRecommendationForMaterialAsync(
        Guid materialId, 
        Guid warehouseId, 
        CancellationToken cancellationToken = default)
    {
        var material = await _dbContext.Materials
            .Include(m => m.Category)
            .Include(m => m.BaseUnit)
            .Include(m => m.SupplierMaterials)
                .ThenInclude(sm => sm.Party)
            .FirstOrDefaultAsync(m => m.Id == materialId, cancellationToken);

        if (material == null) return null;

        var snapshot = await _dbContext.InventorySnapshots
            .FirstOrDefaultAsync(s => s.MaterialId == materialId && s.WarehouseId == warehouseId, cancellationToken);

        var policy = await _dbContext.ReplenishmentPolicies
            .Include(p => p.PreferredSupplier)
            .FirstOrDefaultAsync(p => p.MaterialId == materialId, cancellationToken);

        decimal onHand = snapshot?.OnHandQuantity ?? 0m;
        decimal reservableOnHand = snapshot?.ReservableQuantity ?? onHand;
        decimal incomingOrders = snapshot?.IncomingQuantity ?? 0m;
        decimal reservedQuantity = snapshot?.ReservedQuantity ?? 0m;

        // Effective policy values (falling back to material attributes or standard bakery defaults)
        int leadTimeDays = policy?.LeadTimeDays ?? material.LeadTimeDays ?? 2;
        int reviewPeriodDays = policy?.ReviewPeriodDays ?? 3;
        decimal safetyStock = policy?.SafetyStock ?? material.SafetyStock ?? 0m;
        decimal moq = policy?.MinOrderQuantity ?? 0m;
        decimal? packageMultiple = policy?.PackageMultiple;

        // Calculate 14-day average daily consumption
        var now = DateTime.UtcNow;
        decimal avgDailyConsumption = await _consumptionCalculator.GetAverageDailyConsumptionAsync(
            materialId, 14, now, cancellationToken);

        // Core Replenishment Formula:
        // TargetStock = AverageDailyConsumption * (LeadTimeDays + ReviewPeriodDays) + SafetyStock
        decimal targetStock = Math.Round((avgDailyConsumption * (leadTimeDays + reviewPeriodDays)) + safetyStock, 2);

        // InventoryPosition = UsableOnHand + EligibleIncomingPurchaseOrders - ReservedQuantity
        decimal inventoryPosition = Math.Round(reservableOnHand + incomingOrders - reservedQuantity, 2);

        // RawSuggestedQuantity = max(0, TargetStock - InventoryPosition)
        decimal rawSuggested = Math.Max(0m, Math.Round(targetStock - inventoryPosition, 2));
        decimal roundedSuggested = rawSuggested;

        // Apply MOQ
        if (roundedSuggested > 0 && moq > 0 && roundedSuggested < moq)
        {
            roundedSuggested = moq;
        }

        // Apply Package Multiple rounding
        if (roundedSuggested > 0 && packageMultiple.HasValue && packageMultiple.Value > 0)
        {
            decimal multiple = packageMultiple.Value;
            roundedSuggested = Math.Ceiling(roundedSuggested / multiple) * multiple;
        }

        // Determine preferred supplier
        var preferredSupplier = policy?.PreferredSupplier;
        if (preferredSupplier == null)
        {
            preferredSupplier = material.SupplierMaterials
                .Where(sm => sm.IsPreferred && sm.Party != null)
                .Select(sm => sm.Party)
                .FirstOrDefault();
        }

        // Generate warnings if data is stale or estimates are assumed
        var warnings = new List<string>();
        if (snapshot == null)
        {
            warnings.Add("موجودی فیزیکی در انبار ثبت نشده است (فرض موجودی صفر).");
        }
        else if (snapshot.IsStale || (now - snapshot.SnapshotTimeUtc).TotalHours > 24)
        {
            warnings.Add("داده‌های موجودی انبار قدیمی است (بیش از ۲۴ ساعت از آخرین همگام‌سازی).");
        }

        if (incomingOrders == 0m)
        {
            warnings.Add("سفارشات در راه خرید صفر در نظر گرفته شده است.");
        }

        string warningText = string.Join(" | ", warnings);

        string unitCode = material.BaseUnit?.Code ?? "KG";
        string explanation = $"میانگین مصرف روزانه ({avgDailyConsumption:N1} {unitCode}) در دوره تأمین و بازبینی ({leadTimeDays + reviewPeriodDays} روز) به علاوه ذخیره احتیاطی ({safetyStock:N0} {unitCode}) = سطح هدف {targetStock:N0}. کسری موقعیت انبار ({inventoryPosition:N0}) برابر با {rawSuggested:N0} {unitCode} است.";
        if (roundedSuggested > rawSuggested)
        {
            explanation += $" بر اساس حداقل سفارش ({moq:N0}) یا مضرب بسته ({packageMultiple:N0}) به {roundedSuggested:N0} {unitCode} افزایش یافت.";
        }

        // Upsert into PurchaseRecommendations table for persistence and audit
        var existingRec = await _dbContext.PurchaseRecommendations
            .FirstOrDefaultAsync(r => r.MaterialId == materialId && r.WarehouseId == warehouseId && r.Status == RecommendationStatus.Active, cancellationToken);

        if (existingRec == null)
        {
            existingRec = new PurchaseRecommendation
            {
                MaterialId = materialId,
                WarehouseId = warehouseId,
                CurrentOnHand = onHand,
                ReservableOnHand = reservableOnHand,
                IncomingOrders = incomingOrders,
                ReservedQuantity = reservedQuantity,
                AverageDailyConsumption = avgDailyConsumption,
                LeadTimeDays = leadTimeDays,
                ReviewPeriodDays = reviewPeriodDays,
                SafetyStock = safetyStock,
                TargetStock = targetStock,
                InventoryPosition = inventoryPosition,
                RawSuggestedQuantity = rawSuggested,
                RoundedSuggestedQuantity = roundedSuggested,
                UnitId = material.BaseUnitId,
                PreferredSupplierId = preferredSupplier?.Id,
                Status = RecommendationStatus.Active,
                RecommendationExplanation = explanation,
                DataWarning = string.IsNullOrWhiteSpace(warningText) ? null : warningText,
                GeneratedAtUtc = now
            };
            _dbContext.PurchaseRecommendations.Add(existingRec);
        }
        else
        {
            existingRec.CurrentOnHand = onHand;
            existingRec.ReservableOnHand = reservableOnHand;
            existingRec.IncomingOrders = incomingOrders;
            existingRec.ReservedQuantity = reservedQuantity;
            existingRec.AverageDailyConsumption = avgDailyConsumption;
            existingRec.LeadTimeDays = leadTimeDays;
            existingRec.ReviewPeriodDays = reviewPeriodDays;
            existingRec.SafetyStock = safetyStock;
            existingRec.TargetStock = targetStock;
            existingRec.InventoryPosition = inventoryPosition;
            existingRec.RawSuggestedQuantity = rawSuggested;
            existingRec.RoundedSuggestedQuantity = roundedSuggested;
            existingRec.PreferredSupplierId = preferredSupplier?.Id;
            existingRec.RecommendationExplanation = explanation;
            existingRec.DataWarning = string.IsNullOrWhiteSpace(warningText) ? null : warningText;
            existingRec.GeneratedAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PurchaseRecommendationDto
        {
            Id = existingRec.Id,
            MaterialId = materialId,
            MaterialCode = material.InternalCode,
            MaterialName = material.Name,
            CategoryName = material.Category?.Name ?? "-",
            UnitName = material.BaseUnit?.Name ?? "کیلوگرم",
            UnitCode = unitCode,
            CurrentOnHand = onHand,
            ReservableOnHand = reservableOnHand,
            IncomingOrders = incomingOrders,
            AverageDailyConsumption = avgDailyConsumption,
            LeadTimeDays = leadTimeDays,
            ReviewPeriodDays = reviewPeriodDays,
            SafetyStock = safetyStock,
            TargetStock = targetStock,
            InventoryPosition = inventoryPosition,
            RawSuggestedQuantity = rawSuggested,
            RoundedSuggestedQuantity = roundedSuggested,
            PreferredSupplierId = preferredSupplier?.Id,
            PreferredSupplierName = preferredSupplier?.DisplayName,
            Status = "Active",
            RecommendationExplanation = explanation,
            DataWarning = string.IsNullOrWhiteSpace(warningText) ? null : warningText,
            GeneratedAtUtc = now
        };
    }
}
