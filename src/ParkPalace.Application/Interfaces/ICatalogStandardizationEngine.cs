using ParkPalace.Application.DTOs;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Application.Interfaces;

public interface ICatalogStandardizationEngine
{
    /// <summary>
    /// Parses a raw title and unit, extracting brand, packaging, numeric package size, standardized unit,
    /// hierarchical category, product family, and conversion rules.
    /// </summary>
    StandardizedCatalogItemSuggestion ParseAndStandardize(string rawTitle, string? sepidarUnit = null);

    /// <summary>
    /// Ensures that unified categories (Parent and Subcategory) exist in the database without duplicates.
    /// </summary>
    Task<Category> EnsureUnifiedCategoryAsync(string parentCode, string parentName, string? subcategoryCode = null, string? subcategoryName = null, CancellationToken ct = default);

    /// <summary>
    /// Applies standardized metadata to a material, creating or mapping categories and units as needed.
    /// </summary>
    Task<Material> ApplyStandardizationAsync(Guid materialId, StandardizedCatalogItemSuggestion? customSuggestion = null, bool isManualConfirmation = false, CancellationToken ct = default);

    /// <summary>
    /// Validates whether changing the base unit of a material is safe, considering existing operational history
    /// (stock balance, inventory movements, purchase orders) and presence of confirmed unit conversions.
    /// </summary>
    Task<(bool IsAllowed, string? ErrorMessage)> ValidateBaseUnitChangeAsync(Guid materialId, Guid newBaseUnitId, CancellationToken ct = default);

    /// <summary>
    /// Generates a list of recommended catalog and unit fixes for Sepidar ERP without modifying Sepidar.
    /// </summary>
    Task<List<SepidarCorrectionProposalDto>> GenerateSepidarCorrectionProposalsAsync(CancellationToken ct = default);

    /// <summary>
    /// Phase 1: Applies category & subcategory standardization ONLY.
    /// Excludes name, brand, packaging, size, units, and conversion factors.
    /// Checks concurrency against expectedCurrentCategoryId and guards against user-customized items.
    /// Logs previous CategoryId for rollback capability. Reuses existing categories without duplicates.
    /// </summary>
    Task<CategoryUpdateResultDto> ApplyCategoryStandardizationAsync(
        Guid materialId,
        Guid expectedCurrentCategoryId,
        string targetParentCode,
        string targetParentName,
        string? targetSubCode = null,
        string? targetSubName = null,
        CancellationToken ct = default);

    /// <summary>
    /// Phase 1: Reverts a material category change using recorded rollback note.
    /// </summary>
    Task<CategoryUpdateResultDto> RollbackCategoryStandardizationAsync(Guid materialId, CancellationToken ct = default);
}

