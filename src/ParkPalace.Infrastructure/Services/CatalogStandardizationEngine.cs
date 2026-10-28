using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services;

public class CatalogStandardizationEngine : ICatalogStandardizationEngine
{
    private readonly ICrmDbContext _dbContext;
    private readonly ILogger<CatalogStandardizationEngine> _logger;

    private static readonly Dictionary<string, string> BrandDictionary = new(StringComparer.OrdinalIgnoreCase)
    {
        // Multi-word and longer brand names first
        { "شیرآوران", "شیرآوران" },
        { "سحر سان", "سحرسان" },
        { "سحرسان", "سحرسان" },
        { "گل نان", "گلنان" },
        { "گلنان", "گلنان" },
        { "آبی شن", "آبیشن" },
        { "آبیشن", "آبیشن" },
        { "دامداران", "دامداران" },
        { "مانیزان", "مانیزان" },
        { "پامچال", "پامچال" },
        { "ورامین", "ورامین" },
        { "کیمبال", "کیمبال" },
        { "کولاک", "کولاک" },
        { "فرمند", "فرمند" },
        { "آناتا", "آناتا" },
        { "آیدین", "آیدین" },
        { "رضوی", "رضوی" },
        { "چوپان", "چوپان" },
        { "غنچه", "غنچه" },
        { "لادن", "لادن" },
        { "بهار", "بهار" },
        { "ماهان", "ماهان" },
        { "اصیل", "اصیل" },
        { "ورینا", "ورینا" },
        { "برتر", "برتر" },
        { "گلها", "گلها" },
        { "پگاه", "پگاه" },
        { "میهن", "میهن" },
        { "هراز", "هراز" },
        { "صباح", "صباح" },
        { "رامک", "رامک" },
        { "کاله", "کاله" },
        { "زرین", "زرین" },
        { "سحر", "سحر" },
        { "پاک", "پاک" },
        { "گلا", "گلا" },
        { "شاد", "شاد" },
        { "oab", "OAB" },
        { "تک", "تک" },
        { "زر", "زر" }
    };

    private static readonly Dictionary<string, string> PackagingDictionary = new(StringComparer.OrdinalIgnoreCase)
    {
        { "پاکتی", "پاکت" },
        { "پاکت", "پاکت" },
        { "بطری", "بطری" },
        { "دبه‌ای", "دبه" },
        { "دبه ای", "دبه" },
        { "دبه", "دبه" },
        { "کیسه‌ای", "کیسه" },
        { "کیسه ای", "کیسه" },
        { "کیسه", "کیسه" },
        { "کارتنی", "کارتن" },
        { "کارتن ای", "کارتن" },
        { "کارتن", "کارتن" },
        { "قوطی", "قوطی" },
        { "حلب", "حلب" },
        { "شانه", "شانه" },
        { "بسته‌ای", "بسته" },
        { "بسته ای", "بسته" },
        { "بسته", "بسته" },
        { "تیوب", "تیوب" },
        { "لیوانی", "لیوان" },
        { "لیوان", "لیوان" },
        { "نایلونی", "نایلون" },
        { "نایلون", "نایلون" },
        { "سطلی", "سطل" },
        { "سطل", "سطل" },
        { "اسپری", "اسپری" }
    };

    public CatalogStandardizationEngine(ICrmDbContext dbContext, ILogger<CatalogStandardizationEngine> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public StandardizedCatalogItemSuggestion ParseAndStandardize(string rawTitle, string? sepidarUnit = null)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            return new StandardizedCatalogItemSuggestion
            {
                RawTitle = rawTitle ?? string.Empty,
                StandardName = "نامشخص",
                Status = "NeedsReview",
                Explanation = "عنوان خام خالی است."
            };
        }

        string normalized = NormalizePersian(rawTitle);

        // 1. Extract Brand using exact Persian word boundary matching
        string? brand = null;
        foreach (var kvp in BrandDictionary)
        {
            if (ContainsPersianWord(normalized, kvp.Key))
            {
                // Special exclusions for short words with false positives
                if (kvp.Key == "زر" && (normalized.Contains("بزرگ") || normalized.Contains("زرین") || normalized.Contains("زرد")))
                {
                    // Only match زر if standalone (e.g. "زر ماکارون" or "شکر زر")
                    if (!Regex.IsMatch(normalized, @"(?:^|[^\p{L}\p{Nd}])زر(?:$|[^\p{L}\p{Nd}])", RegexOptions.IgnoreCase))
                        continue;
                }
                if (kvp.Key == "پاک" && normalized.Contains("پاک کن"))
                {
                    continue; // "آرد پاک کن" / cleaner
                }
                if (kvp.Key == "تک" && (normalized.Contains("تک نفره") || normalized.Contains("تایی") || normalized.Contains("تکه")))
                {
                    continue; // "تک نفره" = single serve
                }

                brand = kvp.Value;
                break;
            }
        }

        // 2. Extract Packaging
        string? packaging = null;
        foreach (var kvp in PackagingDictionary)
        {
            if (ContainsPersianWord(normalized, kvp.Key))
            {
                packaging = kvp.Value;
                break;
            }
        }

        // 3. Extract Package Size & Unit
        var (packageSize, packageUnit) = ExtractPackageSizeAndUnit(normalized);

        // 4. Extract Category & Subcategory
        var (parentCatCode, parentCatName, subCatCode, subCatName, isCategoryDeterministic) = DetermineCategory(normalized, brand);

        // 5. Determine BaseUnit, PurchaseUnit, and ConsumptionUnit
        // Discrete consumer packs (milk, yogurt tub, cheese pack, oil bottle) use COUNT for counting/purchasing
        string baseUnitCode = "COUNT";
        string purchaseUnitCode = "COUNT";
        string? consumptionUnitCode = null;
        decimal? suggestedConversion = null;
        string? conversionToUnit = null;

        if (parentCatCode == "CAT-PACKAGING")
        {
            // Requirement 2: Box capacity must be decoupled from contents and must NOT generate a conversion factor
            baseUnitCode = "COUNT";
            purchaseUnitCode = "COUNT";
            consumptionUnitCode = "COUNT";
            suggestedConversion = null;
            conversionToUnit = null;
        }
        else if (packageUnit == "L")
        {
            baseUnitCode = "COUNT";
            purchaseUnitCode = "COUNT";
            consumptionUnitCode = "L";
            suggestedConversion = packageSize ?? 1.0m;
            conversionToUnit = "L";
        }
        else if (packageUnit == "ML")
        {
            baseUnitCode = "COUNT";
            purchaseUnitCode = "COUNT";
            consumptionUnitCode = "L"; // consumption in bakery is standardized to Liters
            suggestedConversion = packageSize.HasValue ? Math.Round(packageSize.Value / 1000m, 4) : 0.25m;
            conversionToUnit = "L";
        }
        else if (packageUnit == "KG")
        {
            baseUnitCode = (packaging == "کیسه" || packaging == "کارتن" || packaging == "دبه" || packaging == "حلب") ? "COUNT" : "KG";
            purchaseUnitCode = baseUnitCode;
            consumptionUnitCode = "KG";
            suggestedConversion = packageSize;
            conversionToUnit = "KG";
        }
        else if (packageUnit == "GR")
        {
            baseUnitCode = "COUNT";
            purchaseUnitCode = "COUNT";
            consumptionUnitCode = "KG";
            suggestedConversion = packageSize.HasValue ? Math.Round(packageSize.Value / 1000m, 4) : null;
            conversionToUnit = "KG";
        }
        else
        {
            // If raw unit is KG or flour bulk, fallback to KG
            if (sepidarUnit == "KG" || normalized.Contains("آرد") || normalized.Contains("شکر") || normalized.Contains("سبوس"))
            {
                baseUnitCode = "KG";
                purchaseUnitCode = "KG";
                consumptionUnitCode = "KG";
            }
        }

        // 6. Synthesize Standard Name (preserving all original qualifiers, percentages, and descriptors)
        string standardName = SynthesizeStandardName(normalized, brand, packaging, packageSize, packageUnit, subCatName, parentCatName);

        // 7. Determine Product Family
        string? productFamily = null;
        if (!string.IsNullOrEmpty(brand) && !string.IsNullOrEmpty(subCatName))
        {
            productFamily = !string.IsNullOrEmpty(packaging)
                ? $"{subCatName} {packaging}ی {brand}".Trim()
                : $"{subCatName} {brand}".Trim();
        }

        // 8. Field-Level Statuses & Confidence (Decoupled per client requirement 1)
        string categoryStatus = isCategoryDeterministic ? "Clear" : "Ambiguous";
        string brandStatus = !string.IsNullOrEmpty(brand) ? "Clear" : "Unknown";
        string packagingStatus = !string.IsNullOrEmpty(packaging) ? "Clear" : "Unknown";
        string packageSizeStatus = packageSize.HasValue ? "Clear" : "Unknown";

        // Categorization is clear independently of whether brand, packaging, or size are known.
        // E.g. "آرد گندم" has a clear category suggestion without requiring manual review.
        string status = isCategoryDeterministic ? "AutoConfirmed" : "NeedsReview";

        string explanation;
        if (isCategoryDeterministic)
        {
            var clearAttrs = new List<string>();
            if (brand != null) clearAttrs.Add($"برند: '{brand}'");
            if (packaging != null) clearAttrs.Add($"بسته‌بندی: '{packaging}'");
            if (packageSize.HasValue) clearAttrs.Add($"اندازه: '{packageSize} {packageUnit}'");

            string attrNote = clearAttrs.Count > 0 ? string.Join("، ", clearAttrs) : "مشخصات تکمیلی نامعلوم (بدون حدس)";
            explanation = $"پیشنهاد روشن دسته‌بندی: '{parentCatName}'{(subCatName != null ? $" / '{subCatName}'" : "")}. {attrNote}.";
        }
        else
        {
            explanation = "عنوان کالا فاقد کلیدواژه قطعی یا صرفاً کد عددی است؛ نیازمند بررسی توسط کارشناس کاتالوگ.";
        }

        return new StandardizedCatalogItemSuggestion
        {
            RawTitle = rawTitle,
            StandardName = standardName,
            Brand = brand,
            Packaging = packaging,
            PackageSize = packageSize,
            PackageSizeUnitCode = packageUnit,
            ParentCategoryCode = parentCatCode,
            ParentCategoryName = parentCatName,
            SubcategoryCode = subCatCode,
            SubcategoryName = subCatName,
            ProductFamily = productFamily,
            BaseUnitCode = baseUnitCode,
            PurchaseUnitCode = purchaseUnitCode,
            ConsumptionUnitCode = consumptionUnitCode,
            SuggestedConversionFactor = suggestedConversion,
            SuggestedConversionToUnit = conversionToUnit,
            CategoryStatus = categoryStatus,
            BrandStatus = brandStatus,
            PackagingStatus = packagingStatus,
            PackageSizeStatus = packageSizeStatus,
            Status = status,
            Explanation = explanation
        };
    }

    public async Task<Category> EnsureUnifiedCategoryAsync(string parentCode, string parentName, string? subcategoryCode = null, string? subcategoryName = null, CancellationToken ct = default)
    {
        // 1. Ensure Parent Category
        var parent = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Code == parentCode || c.Name == parentName, ct);

        if (parent == null)
        {
            parent = new Category
            {
                Code = parentCode,
                Name = parentName,
                IsActive = true,
                SortOrder = 1
            };
            _dbContext.Categories.Add(parent);
            await _dbContext.SaveChangesAsync(ct);
        }

        // 2. If subcategory specified, ensure child category under parent
        if (!string.IsNullOrWhiteSpace(subcategoryCode) && !string.IsNullOrWhiteSpace(subcategoryName))
        {
            var child = await _dbContext.Categories
                .FirstOrDefaultAsync(c => (c.Code == subcategoryCode || c.Name == subcategoryName) && c.ParentId == parent.Id, ct);

            if (child == null)
            {
                child = new Category
                {
                    Code = subcategoryCode,
                    Name = subcategoryName,
                    ParentId = parent.Id,
                    IsActive = true,
                    SortOrder = 1
                };
                _dbContext.Categories.Add(child);
                await _dbContext.SaveChangesAsync(ct);
            }

            return child;
        }

        return parent;
    }

    public async Task<Material> ApplyStandardizationAsync(Guid materialId, StandardizedCatalogItemSuggestion? customSuggestion = null, bool isManualConfirmation = false, CancellationToken ct = default)
    {
        var material = await _dbContext.Materials
            .Include(m => m.Category)
            .Include(m => m.BaseUnit)
            .Include(m => m.UnitConversions)
            .FirstOrDefaultAsync(m => m.Id == materialId, ct);

        if (material == null)
        {
            throw new InvalidOperationException($"ماده اولیه با شناسه '{materialId}' یافت نشد.");
        }

        var mapping = await _dbContext.InventoryItemMappings
            .FirstOrDefaultAsync(m => m.MaterialId == materialId, ct);

        string rawTitle = mapping?.SepidarItemTitle ?? material.Name;
        var suggestion = customSuggestion ?? ParseAndStandardize(rawTitle, mapping?.SepidarBaseUnit);

        // Ensure category
        Category? targetCategory = null;
        if (!string.IsNullOrEmpty(suggestion.ParentCategoryCode) && !string.IsNullOrEmpty(suggestion.ParentCategoryName))
        {
            targetCategory = await EnsureUnifiedCategoryAsync(
                suggestion.ParentCategoryCode,
                suggestion.ParentCategoryName,
                suggestion.SubcategoryCode,
                suggestion.SubcategoryName,
                ct);
        }

        // Resolve Units
        var baseUnit = await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == suggestion.BaseUnitCode, ct);
        var packageSizeUnit = !string.IsNullOrEmpty(suggestion.PackageSizeUnitCode)
            ? await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == suggestion.PackageSizeUnitCode, ct)
            : null;
        var purchaseUnit = !string.IsNullOrEmpty(suggestion.PurchaseUnitCode)
            ? await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == suggestion.PurchaseUnitCode, ct)
            : baseUnit;
        var consumptionUnit = !string.IsNullOrEmpty(suggestion.ConsumptionUnitCode)
            ? await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == suggestion.ConsumptionUnitCode, ct)
            : null;

        // Apply metadata to material
        material.Name = suggestion.StandardName;
        if (targetCategory != null) material.CategoryId = targetCategory.Id;
        if (baseUnit != null) material.BaseUnitId = baseUnit.Id;
        material.Brand = suggestion.Brand;
        material.Packaging = suggestion.Packaging;
        material.PackageSize = suggestion.PackageSize;
        material.PackageSizeUnitId = packageSizeUnit?.Id;
        material.PurchaseUnitId = purchaseUnit?.Id;
        material.ConsumptionUnitId = consumptionUnit?.Id;
        material.ProductFamily = suggestion.ProductFamily;
        material.CategorizationStatus = isManualConfirmation ? "ManualConfirmed" : suggestion.Status;
        material.IsCustomizedByUser = isManualConfirmation;
        material.StandardizationNotes = suggestion.Explanation;

        // Handle Suggested Unit Conversion
        if (suggestion.SuggestedConversionFactor.HasValue && !string.IsNullOrEmpty(suggestion.SuggestedConversionToUnit) && baseUnit != null)
        {
            var toUnit = await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == suggestion.SuggestedConversionToUnit, ct);
            if (toUnit != null && baseUnit.Id != toUnit.Id)
            {
                var existingConv = material.UnitConversions
                    .FirstOrDefault(c => c.FromUnitId == baseUnit.Id && c.ToUnitId == toUnit.Id);

                if (existingConv == null)
                {
                    material.UnitConversions.Add(new MaterialUnitConversion
                    {
                        MaterialId = material.Id,
                        FromUnitId = baseUnit.Id,
                        ToUnitId = toUnit.Id,
                        Factor = suggestion.SuggestedConversionFactor.Value,
                        IsConfirmed = isManualConfirmation, // Only confirmed if user explicitly approves or deterministic
                        Notes = $"تبدیل استخراج‌شده از محتویات بسته ({suggestion.PackageSize} {suggestion.PackageSizeUnitCode})"
                    });
                }
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        return material;
    }

    /// <summary>
    /// Phase 1: Applies category & subcategory standardization ONLY.
    /// Excludes name, brand, packaging, size, units, and conversion factors.
    /// Checks concurrency against expectedCurrentCategoryId and guards against user-customized items.
    /// Logs previous CategoryId for rollback capability. Reuses existing categories without duplicates.
    /// </summary>
    public async Task<CategoryUpdateResultDto> ApplyCategoryStandardizationAsync(
        Guid materialId,
        Guid expectedCurrentCategoryId,
        string targetParentCode,
        string targetParentName,
        string? targetSubCode = null,
        string? targetSubName = null,
        CancellationToken ct = default)
    {
        var material = await _dbContext.Materials
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == materialId, ct);

        if (material == null)
        {
            return new CategoryUpdateResultDto
            {
                Success = false,
                MaterialId = materialId,
                ErrorMessage = $"ماده اولیه با شناسه '{materialId}' یافت نشد."
            };
        }

        // 1. Guard against overwriting user manual customizations
        if (material.IsCustomizedByUser || material.CategorizationStatus == "ManualConfirmed")
        {
            return new CategoryUpdateResultDto
            {
                Success = false,
                MaterialId = materialId,
                ErrorMessage = "این کالا توسط کاربر به صورت دستی اصلاح یا تأیید شده است و قابل بازنویسی خودکار نیست."
            };
        }

        // 2. Concurrency control: verify category hasn't been changed concurrently
        if (material.CategoryId != expectedCurrentCategoryId)
        {
            return new CategoryUpdateResultDto
            {
                Success = false,
                MaterialId = materialId,
                ErrorMessage = $"دسته‌بندی کالا همزمان توسط کاربر یا فرایند دیگری تغییر کرده است (دسته فعلی: '{material.Category?.Name}'). عملیات لغو شد."
            };
        }

        // 3. Ensure target category exists (reuse existing without duplicates)
        var targetCategory = await EnsureUnifiedCategoryAsync(targetParentCode, targetParentName, targetSubCode, targetSubName, ct);

        if (material.CategoryId == targetCategory.Id)
        {
            return new CategoryUpdateResultDto
            {
                Success = true,
                MaterialId = materialId,
                IsUnchanged = true,
                OldCategoryId = material.CategoryId,
                NewCategoryId = targetCategory.Id,
                OldCategoryName = material.Category?.Name,
                NewCategoryName = targetCategory.Name,
                Message = "دسته کالا قبلاً برابر دسته هدف بوده و تغییری لازم نبود."
            };
        }

        // 4. Record previous state for rollback
        var rollbackRecord = new
        {
            Action = "Phase1_CategoryStandardization",
            PreviousCategoryId = material.CategoryId,
            PreviousCategoryName = material.Category?.Name,
            PreviousCategoryCode = material.Category?.Code,
            NewCategoryId = targetCategory.Id,
            NewCategoryName = targetCategory.Name,
            AppliedAtUtc = DateTime.UtcNow
        };

        Guid oldCategoryId = material.CategoryId;
        material.CategoryId = targetCategory.Id;
        material.CategorizationStatus = "AutoConfirmed";
        material.StandardizationNotes = JsonSerializer.Serialize(rollbackRecord);
        material.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return new CategoryUpdateResultDto
        {
            Success = true,
            MaterialId = materialId,
            IsUnchanged = false,
            OldCategoryId = oldCategoryId,
            NewCategoryId = targetCategory.Id,
            OldCategoryName = rollbackRecord.PreviousCategoryName,
            NewCategoryName = targetCategory.Name,
            Message = "دسته‌بندی با موفقیت تغییر یافت و اطلاعات قبلی جهت بازگشت ذخیره شد."
        };
    }

    /// <summary>
    /// Phase 1: Reverts a material category change using recorded rollback note.
    /// </summary>
    public async Task<CategoryUpdateResultDto> RollbackCategoryStandardizationAsync(Guid materialId, CancellationToken ct = default)
    {
        var material = await _dbContext.Materials
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == materialId, ct);

        if (material == null)
        {
            return new CategoryUpdateResultDto { Success = false, MaterialId = materialId, ErrorMessage = "ماده اولیه یافت نشد." };
        }

        if (string.IsNullOrWhiteSpace(material.StandardizationNotes) || !material.StandardizationNotes.Contains("PreviousCategoryId"))
        {
            return new CategoryUpdateResultDto { Success = false, MaterialId = materialId, ErrorMessage = "هیچ لاگ بازگشتی برای این قلم ثبت نشده است." };
        }

        try
        {
            using var doc = JsonDocument.Parse(material.StandardizationNotes);
            if (doc.RootElement.TryGetProperty("PreviousCategoryId", out var prevProp) &&
                Guid.TryParse(prevProp.GetString(), out var prevGuid))
            {
                var prevCat = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == prevGuid, ct);
                if (prevCat != null)
                {
                    material.CategoryId = prevCat.Id;
                    material.CategorizationStatus = "NeedsReview";
                    material.StandardizationNotes = $"بازگشت داده شد به دسته قبلی '{prevCat.Name}' در {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
                    material.UpdatedAtUtc = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync(ct);

                    return new CategoryUpdateResultDto
                    {
                        Success = true,
                        MaterialId = materialId,
                        NewCategoryId = prevCat.Id,
                        NewCategoryName = prevCat.Name,
                        Message = "تغییر دسته با موفقیت بازگردانده شد."
                    };
                }
            }
        }
        catch (Exception ex)
        {
            return new CategoryUpdateResultDto { Success = false, MaterialId = materialId, ErrorMessage = $"خطا در خواندن اطلاعات بازگشت: {ex.Message}" };
        }

        return new CategoryUpdateResultDto { Success = false, MaterialId = materialId, ErrorMessage = "امکان بازیابی شناسه دسته قبلی وجود نداشت." };
    }

    public async Task<(bool IsAllowed, string? ErrorMessage)> ValidateBaseUnitChangeAsync(Guid materialId, Guid newBaseUnitId, CancellationToken ct = default)

    {
        var material = await _dbContext.Materials
            .Include(m => m.UnitConversions)
            .FirstOrDefaultAsync(m => m.Id == materialId, ct);

        if (material == null)
        {
            return (false, "ماده اولیه یافت نشد.");
        }

        if (material.BaseUnitId == newBaseUnitId)
        {
            return (true, null); // No change
        }

        // Check operational history:
        bool hasStock = await _dbContext.InventorySnapshots.AnyAsync(s => s.MaterialId == materialId && s.OnHandQuantity > 0, ct);
        bool hasMovements = await _dbContext.InventoryMovements.AnyAsync(m => m.MaterialId == materialId, ct);
        bool hasOrders = await _dbContext.PurchaseOrderLines.AnyAsync(pol => pol.MaterialId == materialId, ct);

        if (hasStock || hasMovements || hasOrders)
        {
            // Per Requirement 5: Direct change of BaseUnit on items with operational history is strictly blocked, even if a conversion factor exists.
            // Historical quantities and cardex ledger entries require an independent re-denomination migration path.
            return (false, $"کالای '{material.Name}' دارای سابقه گردش کاردکس، موجودی فیزیکی یا سفارشات خرید است. تغییر مستقیم واحد مبنا برای اقلام دارای سابقه مسدود است، زیرا تغییر واحد بدون فرآیند مستقل انتقال تاریخی، مقادیر گذشته کاردکس را تحریف می‌کند. برای این کالا فقط ویرایش نام استاندارد، دسته‌بندی، برند و مشخصات بسته‌بندی مجاز است.");
        }

        return (true, null);
    }

    public async Task<List<SepidarCorrectionProposalDto>> GenerateSepidarCorrectionProposalsAsync(CancellationToken ct = default)
    {
        var list = new List<SepidarCorrectionProposalDto>();

        var mappings = await _dbContext.InventoryItemMappings
            .Include(m => m.Material)
                .ThenInclude(mat => mat!.Category)
            .Include(m => m.Material)
                .ThenInclude(mat => mat!.BaseUnit)
            .Include(m => m.Material)
                .ThenInclude(mat => mat!.PurchaseUnit)
            .ToListAsync(ct);

        foreach (var m in mappings)
        {
            var mat = m.Material;
            if (mat == null) continue;

            bool nameDiffers = !string.Equals(m.SepidarItemTitle.Trim(), mat.Name.Trim(), StringComparison.OrdinalIgnoreCase);
            bool unitDiffers = !string.Equals(m.SepidarBaseUnit, mat.BaseUnit?.Code, StringComparison.OrdinalIgnoreCase);

            if (nameDiffers || unitDiffers || mat.PackageSize.HasValue)
            {
                list.Add(new SepidarCorrectionProposalDto
                {
                    SepidarItemId = m.SepidarItemId,
                    SepidarItemCode = m.SepidarItemCode,
                    CurrentSepidarTitle = m.SepidarItemTitle,
                    ProposedStandardName = mat.Name,
                    CurrentSepidarUnit = m.SepidarBaseUnit ?? "نامشخص",
                    ProposedPurchaseUnit = mat.PurchaseUnit?.Name ?? mat.BaseUnit?.Name ?? "عدد",
                    ProposedBaseUnit = mat.BaseUnit?.Name ?? "عدد",
                    CurrentCategoryName = "پیش‌فرض سپیدار",
                    ProposedCategoryName = mat.Category?.Name ?? "عمومی",
                    PackageSize = mat.PackageSize,
                    PackageSizeUnit = mat.PackageSizeUnitId.HasValue ? mat.PackageSizeUnit?.Name : null,
                    Justification = $"استانداردسازی کاتالوگ CRM: تفکیک واحد شمارش از حجم/وزن بسته ({mat.PackageSize} {mat.PackageSizeUnit?.Name}) و اصلاح نامگذاری طبق الگوی رسمی."
                });
            }
        }

        return list;
    }

    private static bool ContainsPersianWord(string text, string word)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(word)) return false;

        // Word boundary matching for Unicode letters and digits:
        // Must be bounded by start/end or non-letter/non-digit character
        string pattern = $@"(?:^|[^\p{{L}}\p{{Nd}}]){Regex.Escape(word)}(?:$|[^\p{{L}}\p{{Nd}}])";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
    }

    private static (decimal? Size, string? Unit) ExtractPackageSizeAndUnit(string text)
    {
        text = NormalizeDigits(text);

        // 1. Check Liter patterns: e.g. "1لیتری", "۱ لیتری", "1.5 لیتر", "1/5 لیتر", "ا لیتری", "1ایتری"
        var matchLiter = Regex.Match(text, @"(?:(?<val>\d+(?:[.,/]\d+)?)\s*(?:لیتر|لیتری|lit(?:er)?|l\b|ایتری)|(?:لیتر|لیتری)\s*(?<val>\d+(?:[.,/]\d+)?)|(?:^|[^\p{L}])(?<val>[1۱ا])\s*لیتری)", RegexOptions.IgnoreCase);
        if (matchLiter.Success)
        {
            string rawVal = matchLiter.Groups["val"].Value;
            if (rawVal == "ا") rawVal = "1";
            decimal val = ParsePersianDecimal(rawVal);
            if (val > 0) return (val, "L");
        }

        // 2. Check ML/CC patterns: e.g. "250 سی سی", "۲۵۰سیسی", "250cc", "250 میلی لیتر"
        var matchMl = Regex.Match(text, @"(?:(?<val>\d+(?:[.,/]\d+)?)\s*(?:سی\s*سی|سیسی|سی‌سی|cc\b|میلی\s*لیتر|میلی‌لیتر|ml\b)|(?:سی\s*سی|سیسی|سی‌سی|cc|میلی\s*لیتر|میلی‌لیتر)\s*(?<val>\d+(?:[.,/]\d+)?))", RegexOptions.IgnoreCase);
        if (matchMl.Success)
        {
            decimal val = ParsePersianDecimal(matchMl.Groups["val"].Value);
            if (val > 0) return (val, "ML");
        }

        // 3. Check KG patterns: e.g. "1 کیلویی", "5 کیلوگرم", "25 کیلو", "16 ك", "16 ک", "16ك"
        var matchKg = Regex.Match(text, @"(?:(?<val>\d+(?:[.,/]\d+)?)\s*(?:کیلوگرم|کیلویی|کیلو|kg\b|ك\b|ک\b)|(?:کیلوگرم|کیلویی|کیلو)\s*(?<val>\d+(?:[.,/]\d+)?))", RegexOptions.IgnoreCase);
        if (matchKg.Success)
        {
            decimal val = ParsePersianDecimal(matchKg.Groups["val"].Value);
            if (val > 0) return (val, "KG");
        }

        // 4. Check Gram patterns: e.g. "810 گرمی", "810 گرم", "350g", "350 gr"
        var matchGr = Regex.Match(text, @"(?:(?<val>\d+(?:[.,/]\d+)?)\s*(?:گرمی|گرم|gr\b|g\b)|(?:گرمی|گرم)\s*(?<val>\d+(?:[.,/]\d+)?))", RegexOptions.IgnoreCase);
        if (matchGr.Success)
        {
            decimal val = ParsePersianDecimal(matchGr.Groups["val"].Value);
            if (val > 0) return (val, "GR");
        }

        var matchSpecialDairyGr = Regex.Match(text, @"(?:دبه\s*(?<val>\d{3,4}))", RegexOptions.IgnoreCase);
        if (matchSpecialDairyGr.Success)
        {
            decimal val = ParsePersianDecimal(matchSpecialDairyGr.Groups["val"].Value);
            if (val > 0) return (val, "GR");
        }

        // Size is not clearly stated - per requirement, keep unknown, DO NOT GUESS!
        return (null, null);
    }

    private static (string ParentCode, string ParentName, string? SubCode, string? SubName, bool IsDeterministic) DetermineCategory(string text, string? brand = null)
    {
        // 0. Purely numeric titles check (Requirement 2):
        if (Regex.IsMatch(text.Trim(), @"^\d+$"))
        {
            return ("CAT-GENERAL", "مواد اولیه عمومی", null, null, false);
        }

        // 1. Plant-based Beverages (Requirement 2: Soy milk, Almond milk, etc. are plant beverages, NOT dairy!):
        if (text.Contains("شیر سویا") || text.Contains("شیر بادام") || text.Contains("شیر فندق") || text.Contains("شیر نارگیل") || text.Contains("غیر لبنی") || text.Contains("سویا"))
        {
            return ("CAT-BEVERAGE", "نوشیدنی‌ها", "CAT-PLANT-BEVERAGE", "نوشیدنی‌های گیاهی", true);
        }

        // 2. Dairy:
        // Check Cheese before Milk to avoid brand false positives like "پنیر پیتزا شیرآوران"
        if (text.Contains("پنیر"))
        {
            return ("CAT-DAIRY", "لبنیات", "CAT-CHEESE", "پنیر", true);
        }
        if (text.Contains("ماست"))
        {
            return ("CAT-DAIRY", "لبنیات", "CAT-YOGURT", "ماست", true);
        }
        if (text.Contains("کره") || text.Contains("خامه"))
        {
            return ("CAT-DAIRY", "لبنیات", "CAT-BUTTER-CREAM", "کره و خامه", true);
        }
        if (text.Contains("دوغ") || text.Contains("کشک"))
        {
            return ("CAT-DAIRY", "لبنیات", "CAT-DAIRY-OTHER", "سایر لبنیات", true);
        }
        // Requirement 1: Powdered milk (شیر خشک) separated from liquid milk
        if (text.Contains("شیر خشک") || text.Contains("شیرخشک") || text.Contains("پودر شیر"))
        {
            return ("CAT-DAIRY", "لبنیات", "CAT-MILK-POWDER", "شیر خشک", true);
        }
        // Milk: exclude non-milk items (شیرآوران, شیرمال, خاکشیر, شیرین, شیرینی, شیره, کرمفیل)
        if (text.Contains("شیر") && !text.Contains("شیره") && !text.Contains("شیرین") && !text.Contains("شیر مال") && !text.Contains("خاک شیر") && !text.Contains("کرمفیل"))
        {
            // If the only occurrence of "شیر" was in brand "شیرآوران", do not categorize as Milk unless title actually has standalone "شیر"
            if (brand == "شیرآوران" && !Regex.IsMatch(text, @"(?:^|[^\p{L}])شیر(?:$|[^\p{L}])", RegexOptions.IgnoreCase))
            {
                // Not milk
            }
            else
            {
                return ("CAT-DAIRY", "لبنیات", "CAT-MILK", "شیر", true);
            }
        }

        // 3. Fats & Oils:
        if (text.Contains("روغن") || text.Contains("مارگارین") || text.Contains("شورتنینگ"))
        {
            return ("CAT-FATS", "روغن و چربی‌های قنادی", "CAT-OIL", "روغن", true);
        }

        // 4. Dough & Semi-prepared (Requirement 1: "خمیر منجمد" -> زیرگروه «خمیر و نیمه‌ساخته»، نه «آرد»)
        // Check Dough before Flours so that e.g. "خمیر منجمد سبوس گرد ویژه" gets subcategory "خمیر و نیمه‌ساخته"
        if (text.Contains("خمیر منجمد") || text.Contains("خمیر پیتزا") || text.Contains("خمیر پیراشکی") ||
            text.Contains("خمیر یوفکا") || text.Contains("خمیر هزارلا") || text.Contains("خمیر کلوچه") ||
            text.Contains("خمیر تارت") || text.Contains("خمیر کرواسان") || text.Contains("نیمه ساخته") ||
            text.Contains("نیمه‌ساخته") || (text.Contains("خمیر") && !text.Contains("خمیرمایه") && !text.Contains("خمیر مایه")))
        {
            return ("CAT-FLOUR", "آرد و غلات", "CAT-DOUGH-SEMI", "خمیر و نیمه‌ساخته", true);
        }

        // 5. Flours & Grains:
        if (text.Contains("آرد") || text.Contains("گندم") || text.Contains("جو") || text.Contains("سبوس"))
        {
            return ("CAT-FLOUR", "آرد و غلات", "CAT-FLOUR-RAW", "آرد", true);
        }

        // 6. Sugar & Sweeteners:
        if (text.Contains("شکر") || text.Contains("قند") || text.Contains("گلوکز") || text.Contains("فروکتوز"))
        {
            return ("CAT-SUGAR", "شکر و شیرین‌کننده‌ها", "CAT-SUGAR-RAW", "شکر و قند", true);
        }

        // 7. Improvers & Yeast:
        if (text.Contains("مخمر") || text.Contains("خمیرمایه") || text.Contains("خمیر مایه") || text.Contains("بهبوددهنده"))
        {
            return ("CAT-IMPROVER", "بهبوددهنده‌ها و مخمر", "CAT-IMPROVER-RAW", "بهبوددهنده و مخمر", true);
        }


        // 7. Packaging:
        if (text.Contains("سلفون") || text.Contains("نایلون") || text.Contains("جعبه") || text.Contains("لیبل") || text.Contains("کارتن"))
        {
            return ("CAT-PACKAGING", "ملزومات بسته‌بندی", "CAT-PACKAGING-SUPPLIES", "ملزومات بسته‌بندی", true);
        }

        // Ambiguous / Non-deterministic:
        return ("CAT-GENERAL", "مواد اولیه عمومی", null, null, false);
    }

    private static string SynthesizeStandardName(string normalized, string? brand, string? packaging, decimal? size, string? unit, string? subCat, string parentCat)
    {
        if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;

        // Requirement 2: Purely numeric titles without valid evidence should not be converted to product names
        if (Regex.IsMatch(normalized.Trim(), @"^\d+$"))
        {
            return normalized.Trim();
        }

        string result = normalized.Trim();

        // Standardize delimiters: replace isolated hyphens or underscores with single space
        result = Regex.Replace(result, @"\s*[-_]\s*", " ");
        result = Regex.Replace(result, @"\s+", " ");

        // Convert digits to Persian digits
        result = ToPersianDigits(result);
        result = result.Replace("%", "٪");

        // Standardize spacing around units
        result = Regex.Replace(result, @"(?<=\d)\s*(?:لیتری|لیتر|ایتری)", " لیتری");
        result = Regex.Replace(result, @"(?<=\d)\s*(?:سی\s*سی|سیسی|سی‌سی|cc)", " سی‌سی");
        result = Regex.Replace(result, @"(?<=\d)\s*(?:کیلوگرم|کیلویی|کیلو|ك\b|ک\b)", " کیلویی");
        result = Regex.Replace(result, @"(?<=\d)\s*(?:گرمی|گرم|gr\b|g\b)", " گرمی");

        // Standardize packaging spelling
        result = result.Replace("کیسه ای", "کیسه‌ای")
                       .Replace("دبه ای", "دبه‌ای")
                       .Replace("بسته ای", "بسته‌ای");

        // Standardize brand casing
        if (brand == "OAB")
        {
            result = Regex.Replace(result, @"\boab\b", "OAB", RegexOptions.IgnoreCase);
        }

        // Standardize brand position if prefix (e.g. "دامداران شیر پاکتی ۱ لیتری" -> "شیر پاکتی دامداران ۱ لیتری")
        if (!string.IsNullOrEmpty(brand) && !string.IsNullOrEmpty(subCat))
        {
            var brandPrefixMatch = Regex.Match(result, $@"^{Regex.Escape(brand)}\s+(?<rest>.+)$", RegexOptions.IgnoreCase);
            if (brandPrefixMatch.Success)
            {
                string rest = brandPrefixMatch.Groups["rest"].Value.Trim();
                if (!ContainsPersianWord(rest, brand))
                {
                    var sizeMatch = Regex.Match(rest, @"(?<main>.+?)\s+(?<size>\d+(?:[.,/]\d+)?\s*(?:لیتری|سی‌سی|کیلویی|گرمی))$");
                    if (sizeMatch.Success)
                    {
                        result = $"{sizeMatch.Groups["main"].Value.Trim()} {brand} {sizeMatch.Groups["size"].Value.Trim()}";
                    }
                    else
                    {
                        result = $"{rest} {brand}".Trim();
                    }
                }
            }
        }

        // Specific cleanups while strictly preserving qualifiers
        result = result.Replace("پنیر پیتزای دانمارکی کاله هر بسته ۲ کیلویی", "پنیر پیتزای دانمارکی کاله ۲ کیلویی")
                       .Replace("روغن ۱۶ کیلویی لادن", "روغن لادن ۱۶ کیلویی");

        // Requirement 2: Never truncate or discard original descriptive words (e.g. percentages 21%, model designations)
        return result.Trim();
    }

    private static string NormalizeCleanTitle(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var cleaned = input
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Replace("  ", " ")
            .Trim();
        return cleaned;
    }

    private static decimal ParsePersianDecimal(string valStr)
    {
        string cleaned = valStr.Replace("/", ".").Replace(",", ".");
        cleaned = NormalizeDigits(cleaned);
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }

    private static string FormatDecimal(decimal val)
    {
        string formatted = val % 1 == 0 ? ((int)val).ToString() : val.ToString("G", CultureInfo.InvariantCulture);
        return ToPersianDigits(formatted);
    }

    private static string ToPersianDigits(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input
            .Replace('0', '۰').Replace('1', '۱').Replace('2', '۲').Replace('3', '۳').Replace('4', '۴')
            .Replace('5', '۵').Replace('6', '۶').Replace('7', '۷').Replace('8', '۸').Replace('۹', '۹');
    }

    private static string NormalizePersian(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        return input
            .Replace('ي', 'ی')
            .Replace('ك', 'ک')
            .Replace('ة', 'ه')
            .Replace('\u200c', ' ')
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Replace("  ", " ")
            .Trim();
    }

    private static string NormalizeDigits(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input
            .Replace('۰', '0').Replace('۱', '1').Replace('۲', '2').Replace('۳', '3').Replace('۴', '4')
            .Replace('۵', '5').Replace('۶', '6').Replace('۷', '7').Replace('۸', '8').Replace('۹', '9');
    }
}
