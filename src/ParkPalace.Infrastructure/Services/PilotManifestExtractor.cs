using Microsoft.EntityFrameworkCore;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;

namespace ParkPalace.Infrastructure.Services;

public sealed record PilotItemExclusion(
    string SourceItemId,
    Guid? MaterialId,
    string? MaterialCode,
    string? MaterialName,
    string Reason,
    string Details);

public sealed record PilotManifestExtractionResult(
    PilotManifest? Manifest,
    string? ManifestHash,
    IReadOnlyList<PilotItem> ValidItems,
    IReadOnlyList<PilotItemExclusion> ExcludedItems,
    IReadOnlyList<string> IncompleteCategoriesWarning);

/// <summary>
/// Extracts a proposed pilot manifest from valid active CRM mappings with compatible KG units.
/// In accordance with pilot rules:
/// - Historical count 220 is NOT a quota: no arbitrary addition/removal.
/// - Incomplete Sepidar category is NOT a reason to exclude.
/// - Ambiguities in identity, unit, warehouse or missing baseline snapshot prevent entry into the pilot.
/// </summary>
public static class PilotManifestExtractor
{
    public static async Task<PilotManifestExtractionResult> ExtractAsync(
        CrmDbContext db,
        int fiscalYearId,
        DateTime periodStart,
        DateTime periodEndExclusive,
        string warehouseCode = "WH-MAIN",
        string sourceWarehouseId = "1",
        CancellationToken ct = default)
    {
        var warehouse = await db.Warehouses.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Code == warehouseCode && w.IsActive, ct);
        if (warehouse == null || warehouse.SepidarWarehouseRef != sourceWarehouseId)
        {
            throw new InvalidOperationException($"PILOT_WAREHOUSE_MISMATCH: Warehouse '{warehouseCode}' with source ref '{sourceWarehouseId}' not found or inactive.");
        }

        var kgUnit = await db.UnitsOfMeasure.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Code == "KG" && u.IsActive, ct);
        if (kgUnit == null)
        {
            throw new InvalidOperationException("PILOT_KG_UNIT_NOT_FOUND: Active unit of measure 'KG' not found in CRM.");
        }

        var mappings = await db.InventoryItemMappings.AsNoTracking().ToListAsync(ct);
        var materials = await db.Materials.AsNoTracking().ToListAsync(ct);
        var units = await db.UnitsOfMeasure.AsNoTracking().ToDictionaryAsync(u => u.Id, ct);
        var categories = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        var snapshots = await db.InventorySnapshots.AsNoTracking()
            .Where(s => s.WarehouseId == warehouse.Id)
            .ToListAsync(ct);

        var validItems = new List<PilotItem>();
        var excludedItems = new List<PilotItemExclusion>();
        var warnings = new List<string>();

        // Detect duplicate source item references across mappings
        var duplicateSourceIds = mappings
            .Where(m => !string.IsNullOrWhiteSpace(m.SepidarItemId))
            .GroupBy(m => m.SepidarItemId, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        // Detect duplicate materials across active mappings
        var duplicateMaterialMappings = mappings
            .Where(m => m.IsActive)
            .GroupBy(m => m.MaterialId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        var materialsById = materials.ToDictionary(m => m.Id);

        foreach (var mapping in mappings)
        {
            materialsById.TryGetValue(mapping.MaterialId, out var material);
            var sourceId = mapping.SepidarItemId ?? "";
            var matCode = material?.InternalCode;
            var matName = material?.Name ?? mapping.SepidarItemTitle;

            if (string.IsNullOrWhiteSpace(sourceId))
            {
                excludedItems.Add(new PilotItemExclusion(sourceId, mapping.MaterialId, matCode, matName,
                    "MAPPING_EMPTY_SOURCE_ID", "شناسه قلم در سپیدار خالی یا نامعتبر است."));
                continue;
            }

            if (!mapping.IsActive)
            {
                excludedItems.Add(new PilotItemExclusion(sourceId, mapping.MaterialId, matCode, matName,
                    "MAPPING_INACTIVE", "نگاشت کالا در CRM غیرفعال است."));
                continue;
            }

            if (duplicateSourceIds.Contains(sourceId))
            {
                excludedItems.Add(new PilotItemExclusion(sourceId, mapping.MaterialId, matCode, matName,
                    "DUPLICATE_SOURCE_ITEM_ID", "شناسه کالا در سپیدار بین چندین نگاشت مختلف تکرار شده است."));
                continue;
            }

            if (material == null)
            {
                excludedItems.Add(new PilotItemExclusion(sourceId, mapping.MaterialId, matCode, matName,
                    "MATERIAL_NOT_FOUND", "کالای متناظر با این نگاشت در CRM یافت نشد."));
                continue;
            }

            if (!material.IsActive)
            {
                excludedItems.Add(new PilotItemExclusion(sourceId, mapping.MaterialId, matCode, matName,
                    "MATERIAL_INACTIVE", "کالا در CRM غیرفعال است."));
                continue;
            }

            if (duplicateMaterialMappings.Contains(material.Id))
            {
                excludedItems.Add(new PilotItemExclusion(sourceId, mapping.MaterialId, matCode, matName,
                    "DUPLICATE_MATERIAL_MAPPING", "برای این کالا در CRM بیش از یک نگاشت فعال ثبت شده است."));
                continue;
            }

            // Unit must be strictly KG
            units.TryGetValue(material.BaseUnitId, out var baseUnit);
            if (material.BaseUnitId != kgUnit.Id || baseUnit?.Code != "KG")
            {
                var unitCode = baseUnit?.Code ?? "نامشخص";
                excludedItems.Add(new PilotItemExclusion(sourceId, mapping.MaterialId, matCode, matName,
                    "UNIT_NOT_KG", $"واحد پایه کالا در CRM '{unitCode}' است و کیلوگرم نیست. پایلوت فعلی فقط از واحدهای کیلوگرم پشتیبانی می‌کند."));
                continue;
            }

            // Sepidar base unit compatibility check: must be explicitly confirmed as KG, no fallback.
            if (string.IsNullOrWhiteSpace(mapping.SepidarBaseUnit))
            {
                excludedItems.Add(new PilotItemExclusion(sourceId, mapping.MaterialId, matCode, matName,
                    "SEPIDAR_UNIT_MISSING", "واحد پایه کالا در نگاشت سپیدار خالی یا تأییدنشده است؛ نگاشت صریح واحد کیلوگرم برای ورود به پایلوت الزامی است."));
                continue;
            }

            var sUnit = mapping.SepidarBaseUnit.Trim();
            bool isKg = sUnit.Equals("KG", StringComparison.OrdinalIgnoreCase) ||
                        sUnit.Contains("کیلوگرم", StringComparison.OrdinalIgnoreCase) ||
                        sUnit.Contains("كیلوگرم", StringComparison.OrdinalIgnoreCase);

            if (!isKg)
            {
                excludedItems.Add(new PilotItemExclusion(sourceId, mapping.MaterialId, matCode, matName,
                    "SEPIDAR_UNIT_INCOMPATIBLE", $"واحد اعلام‌شده در سپیدار '{sUnit}' است و کیلوگرم نیست."));
                continue;
            }

            // Snapshot baseline check in WH-MAIN
            var itemSnapshots = snapshots.Where(s => s.MaterialId == material.Id).ToArray();
            if (itemSnapshots.Length == 0)
            {
                excludedItems.Add(new PilotItemExclusion(sourceId, mapping.MaterialId, matCode, matName,
                    "MISSING_SNAPSHOT_BASELINE", "اسنپ‌شات اولیه موجودی در انبار اصلی برای این کالا وجود ندارد. پایلوت فقط اقلام دارای اسنپ‌شات را به‌روزرسانی می‌کند."));
                continue;
            }

            if (itemSnapshots.Length > 1)
            {
                excludedItems.Add(new PilotItemExclusion(sourceId, mapping.MaterialId, matCode, matName,
                    "AMBIGUOUS_SNAPSHOT_BASELINE", "بیش از یک اسنپ‌شات موجودی برای این کالا در انبار اصلی یافت شد."));
                continue;
            }

            if (itemSnapshots[0].UnitId != kgUnit.Id)
            {
                excludedItems.Add(new PilotItemExclusion(sourceId, mapping.MaterialId, matCode, matName,
                    "SNAPSHOT_UNIT_MISMATCH", "واحد ثبت‌شده در اسنپ‌شات CRM با واحد پایه کیلوگرم مطابقت ندارد."));
                continue;
            }

            // Incomplete category warning (does NOT exclude item)
            categories.TryGetValue(material.CategoryId, out var category);
            if (material.CategoryId == Guid.Empty || category == null || string.IsNullOrWhiteSpace(category.Code))
            {
                warnings.Add($"کالای '{matCode} - {matName}' دسته‌بندی ناقص دارد اما به دلیل سازگاری واحد و هویت، وارد مانیفست پایلوت شد.");
            }

            validItems.Add(new PilotItem(sourceId, material.Id, kgUnit.Id, "KG"));
        }

        var sortedValidItems = validItems.OrderBy(i => i.SourceItemId, StringComparer.Ordinal).ToList();

        if (sortedValidItems.Count == 0)
        {
            return new PilotManifestExtractionResult(
                Manifest: null,
                ManifestHash: null,
                ValidItems: Array.Empty<PilotItem>(),
                ExcludedItems: excludedItems,
                IncompleteCategoriesWarning: warnings);
        }

        var manifest = new PilotManifest(
            warehouse.Code,
            warehouse.Id,
            warehouse.SepidarWarehouseRef!,
            fiscalYearId,
            periodStart,
            periodEndExclusive,
            sortedValidItems);

        var manifestHash = InventoryPilotPlanner.Hash(new
        {
            manifest.WarehouseCode,
            manifest.WarehouseId,
            manifest.SourceWarehouseId,
            manifest.FiscalYearId,
            manifest.PeriodStart,
            manifest.PeriodEndExclusive,
            Items = sortedValidItems
        });

        return new PilotManifestExtractionResult(
            Manifest: manifest,
            ManifestHash: manifestHash,
            ValidItems: sortedValidItems,
            ExcludedItems: excludedItems,
            IncompleteCategoriesWarning: warnings);
    }
}
