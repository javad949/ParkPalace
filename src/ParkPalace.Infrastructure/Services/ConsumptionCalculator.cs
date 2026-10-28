using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services;

public class ConsumptionCalculator : IConsumptionCalculator
{
    private readonly ICrmDbContext _dbContext;

    public ConsumptionCalculator(ICrmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InventoryConsumptionDto> CalculateConsumptionAsync(
        Guid materialId, 
        DateTime asOfDateUtc, 
        CancellationToken cancellationToken = default)
    {
        var material = await _dbContext.Materials
            .Include(m => m.BaseUnit)
            .Include(m => m.UnitConversions)
            .FirstOrDefaultAsync(m => m.Id == materialId, cancellationToken);

        if (material == null)
        {
            throw new KeyNotFoundException($"Material with ID {materialId} was not found.");
        }

        var startDate30 = asOfDateUtc.Date.AddDays(-30);

        // Fetch verified, posted, non-cancelled consumption movements in the last 30 days
        var movements = await _dbContext.InventoryMovements
            .Include(m => m.Unit)
            .Where(m => m.MaterialId == materialId
                     && m.MovementType == InventoryMovementType.Consumption
                     && m.PostingStatus == 4
                     && !m.IsCancelled
                     && m.TransactionDateUtc >= startDate30
                     && m.TransactionDateUtc <= asOfDateUtc)
            .ToListAsync(cancellationToken);

        // Convert quantities to base unit if necessary
        decimal NormalizeQty(InventoryMovement m)
        {
            if (m.UnitId == material.BaseUnitId) return m.Quantity;
            var conv = material.UnitConversions.FirstOrDefault(c => c.IsConfirmed && c.FromUnitId == m.UnitId && c.ToUnitId == material.BaseUnitId);
            if (conv != null && conv.Factor > 0) return m.Quantity * conv.Factor;
            return m.Quantity;
        }

        var startDate7 = asOfDateUtc.Date.AddDays(-7);
        var startDate14 = asOfDateUtc.Date.AddDays(-14);

        var movs7 = movements.Where(m => m.TransactionDateUtc >= startDate7).ToList();
        var movs14 = movements.Where(m => m.TransactionDateUtc >= startDate14).ToList();

        decimal total7 = movs7.Sum(NormalizeQty);
        decimal total14 = movs14.Sum(NormalizeQty);
        decimal total30 = movements.Sum(NormalizeQty);

        decimal avg7 = total7 / 7m;
        decimal avg14 = total14 / 14m;
        decimal avg30 = total30 / 30m;

        // Group by calendar date to count active vs zero days
        var activeDates30 = movements
            .Select(m => m.TransactionDateUtc.Date)
            .Distinct()
            .Count();

        int zeroDays30 = Math.Max(0, 30 - activeDates30);

        return new InventoryConsumptionDto
        {
            MaterialId = materialId,
            MaterialName = material.Name,
            UnitCode = material.BaseUnit?.Code ?? "KG",
            Consumption7dTotal = Math.Round(total7, 2),
            Consumption7dAverage = Math.Round(avg7, 2),
            Consumption14dTotal = Math.Round(total14, 2),
            Consumption14dAverage = Math.Round(avg14, 2),
            Consumption30dTotal = Math.Round(total30, 2),
            Consumption30dAverage = Math.Round(avg30, 2),
            ActiveDays30d = activeDates30,
            ZeroDays30d = zeroDays30,
            CalculatedAtUtc = DateTime.UtcNow
        };
    }

    public async Task<decimal> GetAverageDailyConsumptionAsync(
        Guid materialId, 
        int windowDays, 
        DateTime asOfDateUtc, 
        CancellationToken cancellationToken = default)
    {
        if (windowDays <= 0) windowDays = 14;

        var summary = await CalculateConsumptionAsync(materialId, asOfDateUtc, cancellationToken);
        return windowDays switch
        {
            <= 7 => summary.Consumption7dAverage,
            <= 14 => summary.Consumption14dAverage,
            _ => summary.Consumption30dAverage
        };
    }
}
