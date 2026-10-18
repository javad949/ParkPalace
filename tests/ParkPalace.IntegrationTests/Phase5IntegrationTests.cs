using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using ParkPalace.Application.DTOs;
using ParkPalace.Domain.Entities;
using Xunit;

namespace ParkPalace.IntegrationTests;

public class Phase5IntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public Phase5IntegrationTests(CustomWebApplicationFactory factory)
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

    private async Task<(Guid materialId, Guid unitId, Guid supplierId)> EnsureDependenciesAsync()
    {
        // 1. Get Base Unit
        using var reqUnits = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/units", "Procurement");
        var resUnits = await _client.SendAsync(reqUnits);
        var units = await resUnits.Content.ReadFromJsonAsync<List<UnitOfMeasureDto>>();
        var unitId = units!.First(u => u.Code == "KG").Id;

        // 2. Get/Create Category
        using var reqCats = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/categories", "Procurement");
        var resCats = await _client.SendAsync(reqCats);
        var cats = await resCats.Content.ReadFromJsonAsync<List<CategoryDto>>();
        Guid catId;
        if (cats != null && cats.Any())
        {
            catId = cats.First().Id;
        }
        else
        {
            using var reqCat = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/categories", "Procurement");
            reqCat.Content = JsonContent.Create(new CreateCategoryRequest { Name = "دسته تدارکات فاز ۵", Code = $"CAT-P5-{Guid.NewGuid():N}".Substring(0, 15) });
            var resCat = await _client.SendAsync(reqCat);
            var cat = await resCat.Content.ReadFromJsonAsync<CategoryDto>();
            catId = cat!.Id;
        }

        // 3. Create Material
        var code = $"MAT-P5-{Guid.NewGuid():N}".Substring(0, 15);
        using var reqMat = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/materials", "Procurement");
        reqMat.Content = JsonContent.Create(new CreateMaterialRequest
        {
            InternalCode = code,
            Name = "ماده اولیه تست فاز ۵",
            CategoryId = catId,
            BaseUnitId = unitId
        });
        var resMat = await _client.SendAsync(reqMat);
        var mat = await resMat.Content.ReadFromJsonAsync<MaterialDto>();

        // 4. Create Supplier
        using var reqSupp = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/suppliers", "Procurement");
        reqSupp.Content = JsonContent.Create(new CreatePartyRequest
        {
            PartyType = PartyType.LegalEntity,
            DisplayName = $"تأمین کننده تست فاز ۵ {Guid.NewGuid():N}".Substring(0, 25),
            LegalName = "شرکت تأمین کننده مواد اولیه تست"
        });
        var resSupp = await _client.SendAsync(reqSupp);
        var supp = await resSupp.Content.ReadFromJsonAsync<PartyDetailDto>();

        return (mat!.Id, unitId, supp!.Id);
    }

    [Fact]
    public async Task DirectPOIssuance_ByProcurement_SucceedsWithoutManagerApproval()
    {
        var (materialId, unitId, supplierId) = await EnsureDependenciesAsync();

        // 1. Create and Directly Issue PO
        var orderReq = new CreatePurchaseOrderRequest
        {
            SupplierId = supplierId,
            Notes = "سفارش مستقیم واحد تدارکات بدون نیاز به تایید مدیر",
            DirectIssue = true,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new()
                {
                    MaterialId = materialId,
                    UnitId = unitId,
                    OrderedQuantity = 100m,
                    UnitPrice = 250000m,
                    TaxRate = 0.10m,
                    DiscountAmount = 0m
                }
            }
        };

        using var req = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/procurement/orders", "Procurement");
        req.Content = JsonContent.Create(orderReq);
        var res = await _client.SendAsync(req);

        // 2. Assert PO is directly Issued
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await res.Content.ReadFromJsonAsync<PurchaseOrderDto>();
        order.Should().NotBeNull();
        order!.Status.Should().Be("Issued");
        order.IssuedAtUtc.Should().NotBeNull();
        order.IssuedByUsername.Should().Contain("procurement");
        order.SubTotal.Should().Be(25000000m);
        order.TaxTotal.Should().Be(2500000m);
        order.GrandTotal.Should().Be(27500000m);
        order.Lines.Should().HaveCount(1);
        order.Lines[0].UnitPrice.Should().Be(250000m);
    }

    [Fact]
    public async Task NonProcurementRole_CannotCreatePO_Returns403Forbidden()
    {
        var (materialId, unitId, supplierId) = await EnsureDependenciesAsync();

        var orderReq = new CreatePurchaseOrderRequest
        {
            SupplierId = supplierId,
            DirectIssue = true,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { MaterialId = materialId, UnitId = unitId, OrderedQuantity = 50m, UnitPrice = 100000m }
            }
        };

        using var req = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/procurement/orders", "Warehouse");
        req.Content = JsonContent.Create(orderReq);
        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Warehouse_CanCreateGoodsReceipt_AndUpdatesPOStatus()
    {
        var (materialId, unitId, supplierId) = await EnsureDependenciesAsync();

        // 1. Issue PO
        var orderReq = new CreatePurchaseOrderRequest
        {
            SupplierId = supplierId,
            DirectIssue = true,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { MaterialId = materialId, UnitId = unitId, OrderedQuantity = 100m, UnitPrice = 50000m }
            }
        };
        using var poReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/procurement/orders", "Procurement");
        poReq.Content = JsonContent.Create(orderReq);
        var poRes = await _client.SendAsync(poReq);
        var po = await poRes.Content.ReadFromJsonAsync<PurchaseOrderDto>();

        // 2. Warehouse receives partial quantity (60 of 100)
        var receiptReq = new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = po!.Id,
            DeliveryNoteNumber = "DEL-9901",
            Lines = new List<CreateGoodsReceiptLineRequest>
            {
                new()
                {
                    PurchaseOrderLineId = po.Lines[0].Id,
                    DeliveredQuantity = 60m,
                    AcceptedQuantity = 58m,
                    RejectedQuantity = 2m,
                    QcStatus = "Conditional",
                    RejectionReason = "۲ کیلوگرم به علت پارگی کیسه مرجوع شد"
                }
            }
        };

        using var grReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/warehouse/receipts", "Warehouse");
        grReq.Content = JsonContent.Create(receiptReq);
        var grRes = await _client.SendAsync(grReq);

        grRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var gr = await grRes.Content.ReadFromJsonAsync<GoodsReceiptDto>();
        gr.Should().NotBeNull();
        gr!.Status.Should().Be("Accepted");
        gr.Lines[0].AcceptedQuantity.Should().Be(58m);
        gr.Lines[0].RejectedQuantity.Should().Be(2m);

        // 3. Verify PO status transitioned to PartiallyReceived
        using var getPoReq = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/v1/procurement/orders/{po.Id}", "Procurement");
        var getPoRes = await _client.SendAsync(getPoReq);
        var updatedPo = await getPoRes.Content.ReadFromJsonAsync<PurchaseOrderDto>();
        updatedPo!.Status.Should().Be("PartiallyReceived");
        updatedPo.Lines[0].ReceivedQuantity.Should().Be(58m);
    }

    [Fact]
    public async Task Warehouse_OverReceiptBeyond105Percent_ReturnsBadRequest()
    {
        var (materialId, unitId, supplierId) = await EnsureDependenciesAsync();

        // 1. Issue PO for 100 KG
        var orderReq = new CreatePurchaseOrderRequest
        {
            SupplierId = supplierId,
            DirectIssue = true,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { MaterialId = materialId, UnitId = unitId, OrderedQuantity = 100m, UnitPrice = 50000m }
            }
        };
        using var poReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/procurement/orders", "Procurement");
        poReq.Content = JsonContent.Create(orderReq);
        var poRes = await _client.SendAsync(poReq);
        var po = await poRes.Content.ReadFromJsonAsync<PurchaseOrderDto>();

        // 2. Warehouse tries to receive 120 KG (> 105% threshold)
        var receiptReq = new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = po!.Id,
            DeliveryNoteNumber = "DEL-OVER",
            Lines = new List<CreateGoodsReceiptLineRequest>
            {
                new()
                {
                    PurchaseOrderLineId = po.Lines[0].Id,
                    DeliveredQuantity = 120m,
                    AcceptedQuantity = 120m,
                    RejectedQuantity = 0m
                }
            }
        };

        using var grReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/warehouse/receipts", "Warehouse");
        grReq.Content = JsonContent.Create(receiptReq);
        var grRes = await _client.SendAsync(grReq);

        // Assert: 400 Bad Request
        grRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Finance_CreatesSupplierInvoice_AndDuplicateDetectionRejectsRepeat()
    {
        var (materialId, unitId, supplierId) = await EnsureDependenciesAsync();

        var invNum = $"INV-DUP-{Guid.NewGuid():N}".Substring(0, 15);
        var invoiceReq = new CreateSupplierInvoiceRequest
        {
            SupplierId = supplierId,
            InvoiceNumber = invNum,
            InvoiceDateUtc = DateTime.UtcNow,
            Lines = new List<CreateSupplierInvoiceLineRequest>
            {
                new()
                {
                    MaterialId = materialId,
                    UnitId = unitId,
                    InvoicedQuantity = 50m,
                    UnitPrice = 80000m,
                    TaxAmount = 400000m
                }
            }
        };

        // 1. Create first invoice -> 201 Created
        using var invReq1 = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/finance/invoices", "Finance");
        invReq1.Content = JsonContent.Create(invoiceReq);
        var invRes1 = await _client.SendAsync(invReq1);
        invRes1.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. Try creating duplicate invoice -> 400 Bad Request
        using var invReq2 = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/finance/invoices", "Finance");
        invReq2.Content = JsonContent.Create(invoiceReq);
        var invRes2 = await _client.SendAsync(invReq2);
        invRes2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorContent = await invRes2.Content.ReadAsStringAsync();
        errorContent.Should().Contain("تأمین‌کننده");
    }

    [Fact]
    public async Task ThreeWayMatching_EvaluatesAndResolvesException()
    {
        var (materialId, unitId, supplierId) = await EnsureDependenciesAsync();

        // 1. Issue PO for 100 KG @ 1000
        var orderReq = new CreatePurchaseOrderRequest
        {
            SupplierId = supplierId,
            DirectIssue = true,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { MaterialId = materialId, UnitId = unitId, OrderedQuantity = 100m, UnitPrice = 1000m, TaxRate = 0.10m }
            }
        };
        using var poReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/procurement/orders", "Procurement");
        poReq.Content = JsonContent.Create(orderReq);
        var poRes = await _client.SendAsync(poReq);
        var po = await poRes.Content.ReadFromJsonAsync<PurchaseOrderDto>();

        // 2. Warehouse receives 100 KG
        var receiptReq = new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = po!.Id,
            DeliveryNoteNumber = "DEL-3WAY",
            Lines = new List<CreateGoodsReceiptLineRequest>
            {
                new()
                {
                    PurchaseOrderLineId = po.Lines[0].Id,
                    DeliveredQuantity = 100m,
                    AcceptedQuantity = 100m
                }
            }
        };
        using var grReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/warehouse/receipts", "Warehouse");
        grReq.Content = JsonContent.Create(receiptReq);
        var grRes = await _client.SendAsync(grReq);
        var gr = await grRes.Content.ReadFromJsonAsync<GoodsReceiptDto>();

        // 3. Invoice billed at higher unit price (1200 instead of 1000 -> mismatch)
        var invReq = new CreateSupplierInvoiceRequest
        {
            SupplierId = supplierId,
            PurchaseOrderId = po.Id,
            InvoiceNumber = $"INV-DIFF-{Guid.NewGuid():N}".Substring(0, 15),
            InvoiceDateUtc = DateTime.UtcNow,
            Lines = new List<CreateSupplierInvoiceLineRequest>
            {
                new()
                {
                    PurchaseOrderLineId = po.Lines[0].Id,
                    MaterialId = materialId,
                    UnitId = unitId,
                    InvoicedQuantity = 100m,
                    UnitPrice = 1200m,
                    TaxAmount = 12000m
                }
            }
        };
        using var invoiceReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/finance/invoices", "Finance");
        invoiceReq.Content = JsonContent.Create(invReq);
        var invoiceRes = await _client.SendAsync(invoiceReq);
        var invoice = await invoiceRes.Content.ReadFromJsonAsync<SupplierInvoiceDto>();

        // 4. Finance runs 3-way matching
        using var matchReq = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/v1/finance/matching/evaluate/{invoice!.Id}", "Finance");
        var matchRes = await _client.SendAsync(matchReq);
        matchRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var matchResult = await matchRes.Content.ReadFromJsonAsync<ThreeWayMatchResultDto>();
        matchResult.Should().NotBeNull();
        matchResult!.MatchStatus.Should().Be("PriceMismatch");

        // 5. Finance queries exceptions queue
        using var exReq = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/finance/matching/exceptions", "Finance");
        var exRes = await _client.SendAsync(exReq);
        exRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var exceptions = await exRes.Content.ReadFromJsonAsync<List<ThreeWayMatchResultDto>>();
        exceptions.Should().Contain(e => e.Id == matchResult.Id);

        // 6. Finance resolves the exception
        var resolveReq = new ResolveMatchingRequest
        {
            ResolutionAction = "ApprovedWithVariance",
            ResolutionNotes = "مبلغ مابه‌التفاوت با تأمین‌کننده مصالحه شد و اعمال بستانکاری در فاکتور بعدی تأیید گردید."
        };
        using var resActionReq = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/v1/finance/matching/resolve/{matchResult.Id}", "Finance");
        resActionReq.Content = JsonContent.Create(resolveReq);
        var resActionRes = await _client.SendAsync(resActionReq);
        resActionRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolved = await resActionRes.Content.ReadFromJsonAsync<ThreeWayMatchResultDto>();
        resolved!.IsResolved.Should().BeTrue();
        resolved.MatchStatus.Should().Be("ApprovedWithVariance");
    }

    [Fact]
    public async Task SepidarReconciliation_FindCandidates_LinkAndUnlink_Audited()
    {
        var (materialId, unitId, supplierId) = await EnsureDependenciesAsync();

        // 1. Create Invoice
        var invoiceReq = new CreateSupplierInvoiceRequest
        {
            SupplierId = supplierId,
            InvoiceNumber = $"INV-SEP-{Guid.NewGuid():N}".Substring(0, 15),
            InvoiceDateUtc = DateTime.UtcNow,
            Lines = new List<CreateSupplierInvoiceLineRequest>
            {
                new()
                {
                    MaterialId = materialId,
                    UnitId = unitId,
                    InvoicedQuantity = 200m,
                    UnitPrice = 450000m,
                    TaxAmount = 9000000m
                }
            }
        };
        using var invReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/finance/invoices", "Finance");
        invReq.Content = JsonContent.Create(invoiceReq);
        var invRes = await _client.SendAsync(invReq);
        var invoice = await invRes.Content.ReadFromJsonAsync<SupplierInvoiceDto>();

        // 2. Fetch candidates from Sepidar adapter
        using var candReq = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/v1/finance/sepidar-reconciliation/candidates/{invoice!.Id}", "Finance");
        var candRes = await _client.SendAsync(candReq);
        candRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var candidates = await candRes.Content.ReadFromJsonAsync<List<SepidarCandidateInvoiceDto>>();
        candidates.Should().NotBeNull();
        candidates.Should().NotBeEmpty();

        var selectedCandidate = candidates!.First();

        // 3. Link invoice to Sepidar document
        var linkReq = new LinkSepidarInvoiceRequest
        {
            SupplierInvoiceId = invoice.Id,
            SepidarInvoiceId = selectedCandidate.SepidarInvoiceId,
            SepidarVoucherNumber = selectedCandidate.SepidarVoucherNumber,
            SepidarVoucherDateUtc = selectedCandidate.VoucherDateUtc,
            SepidarAmount = selectedCandidate.Amount,
            AuditNotes = "تطبیق و اتصال دستی سند مالی توسط کارشناس حسابداری"
        };
        using var doLinkReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/finance/sepidar-reconciliation/link", "Finance");
        doLinkReq.Content = JsonContent.Create(linkReq);
        var doLinkRes = await _client.SendAsync(doLinkReq);
        doLinkRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var reconciliation = await doLinkRes.Content.ReadFromJsonAsync<SepidarFinancialReconciliationDto>();
        reconciliation.Should().NotBeNull();
        reconciliation!.LinkStatus.Should().Be("Confirmed");

        // 4. Unlink invoice with audit reason
        var unlinkReq = new UnlinkPayload
        {
            AuditNotes = "ابطال سند مرجع در سپیدار و نیاز به تطبیق مجدد"
        };
        using var doUnlinkReq = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/v1/finance/sepidar-reconciliation/unlink/{reconciliation.Id}", "Finance");
        doUnlinkReq.Content = JsonContent.Create(unlinkReq);
        var doUnlinkRes = await _client.SendAsync(doUnlinkReq);
        doUnlinkRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var unlinked = await doUnlinkRes.Content.ReadFromJsonAsync<SepidarFinancialReconciliationDto>();
        unlinked!.LinkStatus.Should().Be("Unlinked");

        // 5. Query Audit history
        using var histReq = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/finance/sepidar-reconciliation/history", "Finance");
        var histRes = await _client.SendAsync(histReq);
        histRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await histRes.Content.ReadFromJsonAsync<List<SepidarFinancialReconciliationDto>>();
        history.Should().NotBeNull();
        history!.Any(h => h.Id == reconciliation.Id && h.LinkStatus == "Unlinked").Should().BeTrue();
    }
}
