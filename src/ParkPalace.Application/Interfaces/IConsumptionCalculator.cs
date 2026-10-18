using ParkPalace.Application.DTOs;

namespace ParkPalace.Application.Interfaces;

public interface IConsumptionCalculator
{
    Task<InventoryConsumptionDto> CalculateConsumptionAsync(
        Guid materialId, 
        DateTime asOfDateUtc, 
        CancellationToken cancellationToken = default);

    Task<decimal> GetAverageDailyConsumptionAsync(
        Guid materialId, 
        int windowDays, 
        DateTime asOfDateUtc, 
        CancellationToken cancellationToken = default);
}
