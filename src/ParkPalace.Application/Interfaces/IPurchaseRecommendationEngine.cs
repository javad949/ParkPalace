using ParkPalace.Application.DTOs;

namespace ParkPalace.Application.Interfaces;

public interface IPurchaseRecommendationEngine
{
    Task<List<PurchaseRecommendationDto>> GenerateAllRecommendationsAsync(
        Guid warehouseId, 
        CancellationToken cancellationToken = default);

    Task<PurchaseRecommendationDto?> GenerateRecommendationForMaterialAsync(
        Guid materialId, 
        Guid warehouseId, 
        CancellationToken cancellationToken = default);
}
