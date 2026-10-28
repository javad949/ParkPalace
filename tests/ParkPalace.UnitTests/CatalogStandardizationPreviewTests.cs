using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ParkPalace.Application.DTOs;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Services;
using Xunit;

namespace ParkPalace.UnitTests;

public class CatalogStandardizationPreviewTests
{
    private class MaterialSnapshotItem
    {
        public string Id { get; set; } = string.Empty;
        public string InternalCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryCode { get; set; }
        public string? BaseUnitId { get; set; }
        public string? BaseUnitName { get; set; }
        public string? BaseUnitCode { get; set; }
        public string? Brand { get; set; }
        public string? Packaging { get; set; }
        public decimal? PackageSize { get; set; }
        public string? PackageSizeUnitId { get; set; }
        public string? PackageSizeUnitName { get; set; }
        public string? PackageSizeUnitCode { get; set; }
        public string? PurchaseUnitId { get; set; }
        public string? ConsumptionUnitId { get; set; }
        public string? ProductFamily { get; set; }
        public string? CategorizationStatus { get; set; }
        public bool IsCustomizedByUser { get; set; }
        public string? StandardizationNotes { get; set; }
        public bool IsActive { get; set; }
        public int? SepidarItemId { get; set; }
        public string? SepidarItemCode { get; set; }
        public string? SepidarItemTitle { get; set; }
        public string? SepidarBaseUnit { get; set; }
    }

    public class PreviewItemResult
    {
        public string MaterialId { get; set; } = string.Empty;
        public string InternalCode { get; set; } = string.Empty;
        public string RawTitle { get; set; } = string.Empty;
        public string CurrentName { get; set; } = string.Empty;
        public string ProposedStandardName { get; set; } = string.Empty;
        public string ProposedCategoryName { get; set; } = string.Empty;
        public string? ProposedSubcategoryName { get; set; }
        public string? ProposedBrand { get; set; }
        public string? ProposedPackaging { get; set; }
        public decimal? ProposedPackageSize { get; set; }
        public string? ProposedPackageSizeUnit { get; set; }
        public string ProposedBaseUnit { get; set; } = string.Empty;
        public string? ProposedPurchaseUnit { get; set; }
        public string? ProposedConsumptionUnit { get; set; }
        public decimal? SuggestedConversionFactor { get; set; }
        public string? SuggestedConversionToUnit { get; set; }
        public string CategoryStatus { get; set; } = "Ambiguous";
        public string BrandStatus { get; set; } = "Unknown";
        public string PackagingStatus { get; set; } = "Unknown";
        public string PackageSizeStatus { get; set; } = "Unknown";
        public string Group { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
    }

    public class PreviewSummary
    {
        public int TotalMaterialsCount { get; set; }
        public int ManualCustomizedCount { get; set; }
        public int ClearCategorySuggestionsCount { get; set; }
        public int ClearBrandSuggestionsCount { get; set; }
        public int ClearPackagingSuggestionsCount { get; set; }
        public int ClearPackageSizeSuggestionsCount { get; set; }
        public int TrulyAmbiguousCount { get; set; }
        public List<PreviewItemResult> Items { get; set; } = new();
    }

    [Fact]
    public void Generate_Standardization_Preview_Across_All_Crm_Materials()
    {
        // 1. Locate snapshot file
        string snapshotPath = Path.Combine(AppContext.BaseDirectory, "../../../../scratch/materials_snapshot_1713.json");
        if (!File.Exists(snapshotPath))
        {
            snapshotPath = @"d:\Antigravity\ParkPlace\scratch\materials_snapshot_1713.json";
        }
        File.Exists(snapshotPath).Should().BeTrue("Snapshot file must exist");

        string json = File.ReadAllText(snapshotPath);
        var rawList = JsonSerializer.Deserialize<List<MaterialSnapshotItem>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<MaterialSnapshotItem>();

        // Deduplicate distinct materials by Id if multiple mappings exist
        var distinctMaterials = rawList
            .GroupBy(m => m.Id)
            .Select(g => g.First())
            .ToList();

        // 2. Setup Standardization Engine with InMemory context
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new CrmDbContext(options);
        var engine = new CatalogStandardizationEngine(context, NullLogger<CatalogStandardizationEngine>.Instance);

        var previewResults = new List<PreviewItemResult>();

        foreach (var m in distinctMaterials)
        {
            // Requirement 3: User confirmed or customized specifications MUST be preserved and not overwritten.
            bool isManuallyCustomized = m.IsCustomizedByUser || m.CategorizationStatus == "ManualConfirmed";

            if (isManuallyCustomized)
            {
                previewResults.Add(new PreviewItemResult
                {
                    MaterialId = m.Id,
                    InternalCode = m.InternalCode,
                    RawTitle = m.SepidarItemTitle ?? m.Name,
                    CurrentName = m.Name,
                    ProposedStandardName = m.Name, // Preserve verbatim
                    ProposedCategoryName = m.CategoryName ?? "دسته‌بندی تأییدشده",
                    ProposedSubcategoryName = null,
                    ProposedBrand = m.Brand,
                    ProposedPackaging = m.Packaging,
                    ProposedPackageSize = m.PackageSize,
                    ProposedPackageSizeUnit = m.PackageSizeUnitName,
                    ProposedBaseUnit = m.BaseUnitName ?? "کیلوگرم",
                    ProposedPurchaseUnit = m.PurchaseUnitId != null ? m.PurchaseUnitId : m.BaseUnitName,
                    ProposedConsumptionUnit = m.ConsumptionUnitId,
                    CategoryStatus = "Preserved",
                    BrandStatus = "Preserved",
                    PackagingStatus = "Preserved",
                    PackageSizeStatus = "Preserved",
                    Group = "Group3_ManualCustomizedPreserved",
                    Status = "ManualConfirmed",
                    Explanation = "مشخصات و دسته‌بندی این کالا قبلاً توسط کاربر به صورت دستی اصلاح یا تأیید شده است؛ جهت حفظ اطلاعات کاربر، از بازنویسی یا تغییر خودکار کنار گذاشته شد."
                });
                continue;
            }

            // Requirement 1: Suggest standard name, category & subcategory, brand, packaging, package size.
            // Do not guess unknown items.
            string rawTitle = !string.IsNullOrWhiteSpace(m.SepidarItemTitle) ? m.SepidarItemTitle : m.Name;
            var suggestion = engine.ParseAndStandardize(rawTitle, m.SepidarBaseUnit ?? m.BaseUnitCode);

            // Decoupled group classification:
            // Group 1: Clear Category (even if brand or size are unknown)
            // Group 2: Truly Ambiguous (even category cannot be determined deterministically)
            string group = suggestion.CategoryStatus == "Clear" ? "Group1_ClearCategory" : "Group2_TrulyAmbiguous";

            previewResults.Add(new PreviewItemResult
            {
                MaterialId = m.Id,
                InternalCode = m.InternalCode,
                RawTitle = rawTitle,
                CurrentName = m.Name,
                ProposedStandardName = suggestion.StandardName,
                ProposedCategoryName = suggestion.ParentCategoryName ?? "مواد اولیه عمومی",
                ProposedSubcategoryName = suggestion.SubcategoryName,
                ProposedBrand = suggestion.Brand,
                ProposedPackaging = suggestion.Packaging,
                ProposedPackageSize = suggestion.PackageSize,
                ProposedPackageSizeUnit = suggestion.PackageSizeUnitCode,
                ProposedBaseUnit = suggestion.BaseUnitCode,
                ProposedPurchaseUnit = suggestion.PurchaseUnitCode,
                ProposedConsumptionUnit = suggestion.ConsumptionUnitCode,
                SuggestedConversionFactor = suggestion.SuggestedConversionFactor,
                SuggestedConversionToUnit = suggestion.SuggestedConversionToUnit,
                CategoryStatus = suggestion.CategoryStatus,
                BrandStatus = suggestion.BrandStatus,
                PackagingStatus = suggestion.PackagingStatus,
                PackageSizeStatus = suggestion.PackageSizeStatus,
                Group = group,
                Status = suggestion.Status,
                Explanation = suggestion.Explanation
            });
        }

        var summary = new PreviewSummary
        {
            TotalMaterialsCount = previewResults.Count,
            ManualCustomizedCount = previewResults.Count(p => p.Group == "Group3_ManualCustomizedPreserved"),
            ClearCategorySuggestionsCount = previewResults.Count(p => p.CategoryStatus == "Clear"),
            ClearBrandSuggestionsCount = previewResults.Count(p => p.BrandStatus == "Clear"),
            ClearPackagingSuggestionsCount = previewResults.Count(p => p.PackagingStatus == "Clear"),
            ClearPackageSizeSuggestionsCount = previewResults.Count(p => p.PackageSizeStatus == "Clear"),
            TrulyAmbiguousCount = previewResults.Count(p => p.CategoryStatus == "Ambiguous" && p.Group != "Group3_ManualCustomizedPreserved"),
            Items = previewResults
        };

        // Save preview results to scratch
        string outputPath = @"d:\Antigravity\ParkPlace\scratch\standardization_preview_results.json";
        string outputJson = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(outputPath, outputJson);

        // Verification assertions
        summary.TotalMaterialsCount.Should().Be(distinctMaterials.Count);
        (summary.ManualCustomizedCount + summary.ClearCategorySuggestionsCount + summary.TrulyAmbiguousCount)
            .Should().Be(summary.TotalMaterialsCount);
    }
}
