using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using Xunit;

namespace ParkPalace.UnitTests;

public class PersianTextEncodingTests
{
    private CrmDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CrmDbContext(options);
    }

    [Fact]
    public async Task CrmDbSeeder_MustSeedCleanPersianUnicodeWithoutMojibake()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        // Act
        await CrmDbSeeder.SeedAsync(context);

        // Assert Categories
        var flour = await context.Categories.FirstOrDefaultAsync(c => c.Code == "CAT-FLOUR");
        flour.Should().NotBeNull();
        flour!.Name.Should().Be("آرد و غلات");
        // Verify Unicode codepoints: \u0622 ('آ'), \u0631 ('ر'), \u062F ('د')
        ((int)flour.Name[0]).Should().Be(0x0622);
        flour.Name.Should().NotContain("\u00D8", "Must not contain Latin-1 Mojibake character Ø");
        flour.Name.Should().NotContain("?", "Must not contain question marks");

        var improver = await context.Categories.FirstOrDefaultAsync(c => c.Code == "CAT-IMPROVER");
        improver.Should().NotBeNull();
        improver!.Name.Should().Be("بهبوددهنده‌ها و مخمر");
        improver.Name.Should().NotContain("\u00D8");

        // Assert Units of Measure
        var kg = await context.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == "KG");
        kg.Should().NotBeNull();
        kg!.Name.Should().Be("کیلوگرم");
        kg.Name.Should().NotContain("\u00D8");
        kg.Name.Should().NotContain("?");

        var bag = await context.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == "BAG50");
        bag.Should().NotBeNull();
        bag!.Name.Should().Be("کیسه ۵۰ کیلویی"); // Persian digits

        // Assert Warehouse
        var wh = await context.Warehouses.FirstOrDefaultAsync(w => w.Code == "WH-MAIN");
        wh.Should().NotBeNull();
        wh!.Name.Should().Be("انبار مرکزی کارخانه نان");
        wh.Name.Should().NotContain("\u00D8");

        // Assert Roles
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
        adminRole.Should().NotBeNull();
        adminRole!.Description.Should().Be("مدیر ارشد سیستم - دسترسی کامل به کلیه بخش‌ها و لاگ‌ها");
        adminRole.Description.Should().NotContain("\u00D8");
    }

    [Fact]
    public async Task Entity_PersianRoundTrip_ShouldPreserveSpecialCharsAndZwnj()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        // Test string with:
        // 1. Full Persian alphabet including گ چ پ ژ
        // 2. Persian Yeh (ی \u06CC) and Keheh (ک \u06A9)
        // 3. Persian digits (۰۱۲۳۴۵۶۷۸۹)
        // 4. Zero-Width Non-Joiner (نیم‌فاصله \u200C)
        // 5. Mixed Persian & English with symbols
        var testMaterialName = "آرد گندم ستاره پلاس (Super Star Flour) - کد کالا: ۱۲۳/۴۵\u200C۶ - تست ژله و پودر";
        var testDescription = "تأمین‌کنندهٔ اصلی: شرکت بازرگانی نان آوران پارس - تلفن: ۰۲۱-۸۸۸۸۹۹۹۹";

        var category = new Category
        {
            Code = "CAT-TEST-PERSIAN",
            Name = "دسته‌بندی آزمایشی مواد اولیه",
            Description = testDescription
        };

        var unit = new UnitOfMeasure
        {
            Code = "TEST-KG",
            Name = "کیلوگرم\u200Cبسته",
            Dimension = "Mass",
            Precision = 2
        };

        var material = new Material
        {
            InternalCode = "RAW-TST-001",
            Name = testMaterialName,
            Description = testDescription,
            Category = category,
            BaseUnit = unit,
            IsActive = true
        };

        // Act: Save to DB
        context.Materials.Add(material);
        await context.SaveChangesAsync();

        // Read back via fresh query
        var loaded = await context.Materials
            .Include(m => m.Category)
            .Include(m => m.BaseUnit)
            .FirstOrDefaultAsync(m => m.InternalCode == "RAW-TST-001");

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be(testMaterialName);
        loaded.Description.Should().Be(testDescription);
        loaded.Name.Should().Contain("\u200C", "Must preserve Zero-Width Non-Joiner (نیم‌فاصله)");
        loaded.Category!.Name.Should().Be("دسته‌بندی آزمایشی مواد اولیه");
        loaded.BaseUnit!.Name.Should().Be("کیلوگرم\u200Cبسته");

        // Test JSON Serialization (API response simulation)
        var json = JsonSerializer.Serialize(new
        {
            code = loaded.InternalCode,
            name = loaded.Name,
            categoryName = loaded.Category.Name,
            unitName = loaded.BaseUnit.Name
        });

        // Ensure JSON serializer did not corrupt Persian text
        json.Should().NotContain("\u00D8", "JSON must not contain Mojibake Ø");
        json.Should().NotContain("????", "JSON must not contain question marks");

        // Deserialize back
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("name").GetString().Should().Be(testMaterialName);
        doc.RootElement.GetProperty("categoryName").GetString().Should().Be("دسته‌بندی آزمایشی مواد اولیه");
        doc.RootElement.GetProperty("unitName").GetString().Should().Be("کیلوگرم\u200Cبسته");
    }

    [Theory]
    [InlineData("ک", 0x06A9, "Persian Keheh")]
    [InlineData("ی", 0x06CC, "Persian Yeh")]
    [InlineData("ك", 0x0643, "Arabic Kaf")]
    [InlineData("ي", 0x064A, "Arabic Yeh")]
    [InlineData("۰", 0x06F0, "Persian Digit Zero")]
    [InlineData("۹", 0x06F9, "Persian Digit Nine")]
    [InlineData("\u200C", 0x200C, "Zero-Width Non-Joiner")]
    public void PersianCharacterEncoding_ShouldMatchExactCodepoint(string charStr, int expectedCodePoint, string description)
    {
        ((int)charStr[0]).Should().Be(expectedCodePoint, description);
    }
}
