using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.Common;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using Xunit;

namespace ParkPalace.UnitTests;

public class Phase3UnitTests
{
    private CrmDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CrmDbContext(options);
    }

    [Theory]
    [InlineData("09121234567", "09121234567")]
    [InlineData("+989121234567", "09121234567")]
    [InlineData("00989121234567", "09121234567")]
    [InlineData("9121234567", "09121234567")]
    [InlineData("۰۹۱۲۱۲۳۴۵۶۷", "09121234567")]
    [InlineData("0912-123-4567", "09121234567")]
    [InlineData("+98 (21) 8888 8888", "02188888888")]
    public void PhoneNormalizer_ShouldNormalizeIranianNumbers(string input, string expected)
    {
        var result = PhoneNormalizer.Normalize(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void PhoneNormalizer_ShouldValidateMobileAndLandline()
    {
        PhoneNormalizer.IsValidIranianMobile("09121234567").Should().BeTrue();
        PhoneNormalizer.IsValidIranianMobile("02188888888").Should().BeFalse();
        PhoneNormalizer.IsValidIranianLandline("02188888888").Should().BeTrue();
    }

    [Fact]
    public async Task Category_ShouldSupportArbitraryHierarchy()
    {
        using var context = CreateInMemoryContext();

        var root = new Category { Name = "آرد و غلات", Code = "CAT-ROOT" };
        var child1 = new Category { Name = "آرد گندم", Code = "CAT-WHEAT", Parent = root };
        var child2 = new Category { Name = "آرد ستاره", Code = "CAT-STAR", Parent = child1 };

        context.Categories.AddRange(root, child1, child2);
        await context.SaveChangesAsync();

        var stored = await context.Categories.Include(c => c.Children).ToListAsync();
        stored.Should().HaveCount(3);
        child2.ParentId.Should().Be(child1.Id);
        child1.ParentId.Should().Be(root.Id);
    }

    [Fact]
    public void UnitConversion_WithValidFactor_ShouldSucceed()
    {
        var unitKg = new UnitOfMeasure { Code = "KG", Name = "کیلوگرم", Dimension = "Mass" };
        var unitCarton = new UnitOfMeasure { Code = "CARTON", Name = "کارتن ۲۵ کیلویی", Dimension = "Packaging" };

        var material = new Material { Name = "بهبوددهنده سحر", InternalCode = "MAT-SEPEHR", BaseUnit = unitKg };

        var conversion = new MaterialUnitConversion
        {
            Material = material,
            FromUnit = unitCarton,
            ToUnit = unitKg,
            Factor = 25.000000m
        };

        conversion.Factor.Should().Be(25.0m);
        conversion.FromUnit.Code.Should().Be("CARTON");
        conversion.ToUnit.Code.Should().Be("KG");
    }

    [Fact]
    public void SupplierMaterial_PriceHistory_ShouldMaintainChronologicalOrder()
    {
        var sm = new SupplierMaterial
        {
            Status = "Active",
            IsPreferred = true
        };

        var p1 = new SupplierPriceHistory
        {
            Price = 100000m,
            Currency = "TOMAN",
            EffectiveFrom = new DateTime(2026, 1, 1),
            SupplierMaterial = sm
        };

        var p2 = new SupplierPriceHistory
        {
            Price = 115000m,
            Currency = "TOMAN",
            EffectiveFrom = new DateTime(2026, 6, 1),
            SupplierMaterial = sm
        };

        sm.PriceHistories.Add(p1);
        sm.PriceHistories.Add(p2);

        var latestPrice = sm.PriceHistories.OrderByDescending(p => p.EffectiveFrom).First();
        latestPrice.Price.Should().Be(115000m);
    }

    [Fact]
    public void Party_IndividualAndLegalEntity_ShouldDistinguishProfiles()
    {
        var individual = new Party
        {
            PartyType = PartyType.Individual,
            DisplayName = "حاج رضا قناد",
            FirstName = "رضا",
            LastName = "قناد",
            NationalId = "0012345678"
        };

        var legal = new Party
        {
            PartyType = PartyType.LegalEntity,
            DisplayName = "شرکت آرد سپید البرز",
            LegalName = "شرکت تولیدی آرد سپید البرز (سهامی خاص)",
            NationalIdentifier = "10100123456",
            EconomicCode = "4111222333"
        };

        individual.PartyType.Should().Be(PartyType.Individual);
        individual.NationalId.Should().Be("0012345678");

        legal.PartyType.Should().Be(PartyType.LegalEntity);
        legal.NationalIdentifier.Should().Be("10100123456");
    }
}
