using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ParkPalace.Application.DTOs;
using ParkPalace.Domain.Entities;
using Xunit;

namespace ParkPalace.IntegrationTests;

public class Phase3IntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public Phase3IntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string uri, string role = "Procurement")
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-Dev-Role", role);
        request.Headers.Add("X-Dev-User", $"sahar\\{role.ToLower()}");
        return request;
    }

    [Fact]
    public async Task Categories_TreeAndCycleDetection_ShouldBlockCircularParenting()
    {
        // 1. Create Category A
        using var reqA = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/categories");
        reqA.Content = JsonContent.Create(new CreateCategoryRequest { Name = "دسته تست اصلی", Code = $"CAT-TEST-A-{Guid.NewGuid():N}" });
        var resA = await _client.SendAsync(reqA);
        resA.StatusCode.Should().Be(HttpStatusCode.Created);
        var catA = await resA.Content.ReadFromJsonAsync<CategoryDto>();

        // 2. Create Category B under A
        using var reqB = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/categories");
        reqB.Content = JsonContent.Create(new CreateCategoryRequest { Name = "دسته تست فرعی", Code = $"CAT-TEST-B-{Guid.NewGuid():N}", ParentId = catA!.Id });
        var resB = await _client.SendAsync(reqB);
        resB.StatusCode.Should().Be(HttpStatusCode.Created);
        var catB = await resB.Content.ReadFromJsonAsync<CategoryDto>();

        // 3. Attempt to move A under B (Cycle!) -> Should be blocked with 400
        using var reqCycle = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/v1/categories/{catA.Id}/move");
        reqCycle.Content = JsonContent.Create(new MoveCategoryRequest { NewParentId = catB!.Id });
        var resCycle = await _client.SendAsync(reqCycle);
        resCycle.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 4. Verify Tree endpoint
        using var reqTree = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/categories/tree");
        var resTree = await _client.SendAsync(reqTree);
        resTree.StatusCode.Should().Be(HttpStatusCode.OK);
        var tree = await resTree.Content.ReadFromJsonAsync<List<CategoryTreeDto>>();
        tree.Should().NotBeNull();
        tree!.Any(c => c.Id == catA.Id).Should().BeTrue();
    }

    [Fact]
    public async Task Materials_CreateAndQuery_ShouldSucceed()
    {
        // 1. Get Categories & Units
        using var reqUnits = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/units");
        var resUnits = await _client.SendAsync(reqUnits);
        var units = await resUnits.Content.ReadFromJsonAsync<List<UnitOfMeasureDto>>();
        var kgUnit = units!.First(u => u.Code == "KG");

        using var reqCats = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/categories");
        var resCats = await _client.SendAsync(reqCats);
        var cats = await resCats.Content.ReadFromJsonAsync<List<CategoryDto>>();
        var cat = cats!.First();

        // 2. Create Material
        var code = $"MAT-TST-{Guid.NewGuid():N}".Substring(0, 15).ToUpperInvariant();
        using var reqMat = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/materials");
        reqMat.Content = JsonContent.Create(new CreateMaterialRequest
        {
            InternalCode = code,
            Name = "آرد تستی سبوس دار",
            CategoryId = cat.Id,
            BaseUnitId = kgUnit.Id,
            Brand = "آرد البرز",
            MinStock = 500,
            SafetyStock = 1000,
            LeadTimeDays = 2
        });
        var resMat = await _client.SendAsync(reqMat);
        resMat.StatusCode.Should().Be(HttpStatusCode.Created);
        var mat = await resMat.Content.ReadFromJsonAsync<MaterialDto>();
        mat!.InternalCode.Should().Be(code.ToUpperInvariant());

        // 3. Query Material by search
        using var reqSearch = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/v1/materials?search={code}");
        var resSearch = await _client.SendAsync(reqSearch);
        var searchResult = await resSearch.Content.ReadFromJsonAsync<PagedResult<MaterialDto>>();
        searchResult!.Items.Should().ContainSingle(m => m.InternalCode == code.ToUpperInvariant());
    }

    [Fact]
    public async Task UnitConversions_ShouldEnforceFactorGreaterThanZero()
    {
        // 1. Get Base Unit and Secondary Unit
        using var reqUnits = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/units");
        var resUnits = await _client.SendAsync(reqUnits);
        var units = await resUnits.Content.ReadFromJsonAsync<List<UnitOfMeasureDto>>();
        var kg = units!.First(u => u.Code == "KG");
        var carton = units!.First(u => u.Code == "CARTON");

        // 2. Create a test material
        using var reqCats = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/categories");
        var resCats = await _client.SendAsync(reqCats);
        var cat = (await resCats.Content.ReadFromJsonAsync<List<CategoryDto>>())!.First();

        var code = $"MAT-CNV-{Guid.NewGuid():N}".Substring(0, 15);
        using var reqMat = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/materials");
        reqMat.Content = JsonContent.Create(new CreateMaterialRequest
        {
            InternalCode = code,
            Name = "کنجد سفید قنادی",
            CategoryId = cat.Id,
            BaseUnitId = kg.Id
        });
        var resMat = await _client.SendAsync(reqMat);
        var mat = await resMat.Content.ReadFromJsonAsync<MaterialDto>();

        // 3. Add Invalid conversion (Factor <= 0) -> 400
        using var reqInvalid = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/v1/materials/{mat!.Id}/conversions");
        reqInvalid.Content = JsonContent.Create(new CreateUnitConversionRequest
        {
            FromUnitId = carton.Id,
            ToUnitId = kg.Id,
            Factor = 0
        });
        var resInvalid = await _client.SendAsync(reqInvalid);
        resInvalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 4. Add Valid conversion (1 Carton = 10 KG) -> 200
        using var reqValid = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/v1/materials/{mat.Id}/conversions");
        reqValid.Content = JsonContent.Create(new CreateUnitConversionRequest
        {
            FromUnitId = carton.Id,
            ToUnitId = kg.Id,
            Factor = 10.0m
        });
        var resValid = await _client.SendAsync(reqValid);
        resValid.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Suppliers_CreateLegalAndSearchByNormalizedPhone_ShouldMatch()
    {
        var rawPhone = "۰۹۱۲۳۴۵۶۷۸۹";
        var companyName = $"شرکت فرآورده های غلات سحر {Guid.NewGuid():N}".Substring(0, 25);

        // 1. Create Supplier
        using var reqCreate = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/suppliers");
        reqCreate.Content = JsonContent.Create(new CreatePartyRequest
        {
            PartyType = PartyType.LegalEntity,
            DisplayName = companyName,
            LegalName = companyName,
            NationalIdentifier = $"1010{Random.Shared.Next(1000000, 9999999)}",
            PrimaryPhone = rawPhone,
            PrimaryContactName = "مهندس علوی"
        });
        var resCreate = await _client.SendAsync(reqCreate);
        resCreate.StatusCode.Should().Be(HttpStatusCode.Created);
        var supplier = await resCreate.Content.ReadFromJsonAsync<PartyDto>();

        // 2. Search using different phone format (+989123456789)
        using var reqSearch = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/suppliers?search=+989123456789");
        var resSearch = await _client.SendAsync(reqSearch);
        var searchResult = await resSearch.Content.ReadFromJsonAsync<PagedResult<PartyDto>>();
        searchResult!.Items.Should().Contain(s => s.Id == supplier!.Id);
    }

    [Fact]
    public async Task SupplierMaterial_PriceHistory_AndReverseQueries_ShouldSucceed()
    {
        // 1. Get a Unit and Category
        using var reqUnits = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/units");
        var units = await (await _client.SendAsync(reqUnits)).Content.ReadFromJsonAsync<List<UnitOfMeasureDto>>();
        var kg = units!.First(u => u.Code == "KG");

        using var reqCats = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/categories");
        var cats = await (await _client.SendAsync(reqCats)).Content.ReadFromJsonAsync<List<CategoryDto>>();

        // 2. Create Material
        var matCode = $"MAT-REV-{Guid.NewGuid():N}".Substring(0, 15);
        using var reqMat = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/materials");
        reqMat.Content = JsonContent.Create(new CreateMaterialRequest
        {
            InternalCode = matCode,
            Name = "مخمر خشک فوری طلایی",
            CategoryId = cats!.First().Id,
            BaseUnitId = kg.Id
        });
        var mat = await (await _client.SendAsync(reqMat)).Content.ReadFromJsonAsync<MaterialDto>();

        // 3. Create Supplier
        using var reqSup = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/suppliers");
        reqSup.Content = JsonContent.Create(new CreatePartyRequest
        {
            PartyType = PartyType.LegalEntity,
            DisplayName = $"بازرگانی مخمر زرین {Guid.NewGuid():N}".Substring(0, 25),
            PrimaryPhone = "09351234567"
        });
        var sup = await (await _client.SendAsync(reqSup)).Content.ReadFromJsonAsync<PartyDto>();

        // 4. Link Material to Supplier with initial price
        using var reqLink = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/v1/suppliers/{sup!.Id}/materials");
        reqLink.Content = JsonContent.Create(new AddSupplierMaterialRequest
        {
            MaterialId = mat!.Id,
            IsPreferred = true,
            InitialPrice = 285000,
            Currency = "TOMAN",
            PriceUnitId = kg.Id,
            PriceSource = "استعلام تلفنی مدیر فروش"
        });
        var resLink = await _client.SendAsync(reqLink);
        resLink.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Query reverse from Material: /api/v1/materials/{id}/suppliers
        using var reqReverse = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/v1/materials/{mat.Id}/suppliers");
        var resReverse = await _client.SendAsync(reqReverse);
        var suppliers = await resReverse.Content.ReadFromJsonAsync<List<SupplierMaterialSummaryDto>>();
        suppliers.Should().ContainSingle(s => s.SupplierId == sup.Id);
        suppliers!.First().LatestPrice.Should().Be(285000);
        suppliers!.First().Currency.Should().Be("TOMAN");

        // 6. Append new price quote: /api/v1/suppliers/{id}/materials/{matId}/prices
        using var reqPrice = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/v1/suppliers/{sup.Id}/materials/{mat.Id}/prices");
        reqPrice.Content = JsonContent.Create(new AddPriceHistoryRequest
        {
            Price = 310000,
            Currency = "TOMAN",
            UnitId = kg.Id,
            EffectiveFrom = DateTime.UtcNow.AddDays(1),
            Source = "پیش فاکتور جدید شماره ۹۹۰"
        });
        var resPrice = await _client.SendAsync(reqPrice);
        resPrice.StatusCode.Should().Be(HttpStatusCode.OK);

        // 7. Verify price history has 2 records
        using var reqHist = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/v1/suppliers/{sup.Id}/materials/{mat.Id}/prices");
        var resHist = await _client.SendAsync(reqHist);
        var priceList = await resHist.Content.ReadFromJsonAsync<List<SupplierPriceHistoryDto>>();
        priceList.Should().HaveCount(2);
        priceList!.First().Price.Should().Be(310000);
    }

    [Fact]
    public async Task RBAC_Enforcement_ShouldDenyUnauthorizedOperations()
    {
        // 1. Unauthenticated request -> 401
        using var unauthReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/categories");
        unauthReq.Headers.Add("X-Dev-Unauthenticated", "true");
        var unauthRes = await _client.SendAsync(unauthReq);
        unauthRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2. Warehouse role attempting to create category -> 403 Forbidden
        using var warehouseReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/categories", role: "Warehouse");
        warehouseReq.Content = JsonContent.Create(new CreateCategoryRequest { Name = "دسته غیرمجاز", Code = "CAT-NO-PERM" });
        var warehouseRes = await _client.SendAsync(warehouseReq);
        warehouseRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Warehouse role attempting to add price history -> 403 Forbidden
        using var warehousePriceReq = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/v1/suppliers/{Guid.NewGuid()}/materials/{Guid.NewGuid()}/prices", role: "Warehouse");
        warehousePriceReq.Content = JsonContent.Create(new AddPriceHistoryRequest
        {
            Price = 1000,
            UnitId = Guid.NewGuid(),
            EffectiveFrom = DateTime.UtcNow
        });
        var warehousePriceRes = await _client.SendAsync(warehousePriceReq);
        warehousePriceRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
