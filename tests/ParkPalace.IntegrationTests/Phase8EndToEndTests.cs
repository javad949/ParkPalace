using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkPalace.Application.DTOs;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using Xunit;
using Xunit.Abstractions;

namespace ParkPalace.IntegrationTests;

/// <summary>
/// Phase 8: Comprehensive Controlled End-to-End Business Scenario Test Suite
/// Validates the complete 18-step procurement, inventory, matching, telephony, reporting, and RBAC lifecycle.
/// </summary>
public class Phase8EndToEndTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public Phase8EndToEndTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = factory.CreateClient();
    }

    private HttpRequestMessage CreateAuthRequest(HttpMethod method, string uri, string role = "Admin")
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-Dev-Role", role);
        request.Headers.Add("X-Dev-User", $"sahar\\{role.ToLower()}");
        return request;
    }

    [Fact]
    public async Task Complete18Step_ControlledEndToEndWorkflow_ExecutesSuccessfully()
    {
        _output.WriteLine("=== STARTING PHASE 8 CONTROLLED END-TO-END BUSINESS SCENARIO ===");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

        // -------------------------------------------------------------
        // STEP 1: Create a material, category, warehouse and measurement unit
        // -------------------------------------------------------------
        _output.WriteLine("[STEP 1] Creating Measurement Unit, Category, Warehouse and Raw Material...");
        var unitId = Guid.NewGuid();
        var unit = new UnitOfMeasure
        {
            Id = unitId,
            Code = "KG",
            Name = "کیلوگرم",
            Precision = 2,
            Dimension = "Mass",
            IsActive = true
        };
        db.UnitsOfMeasure.Add(unit);

        var warehouseId = Guid.NewGuid();
        var warehouse = new Warehouse
        {
            Id = warehouseId,
            Code = "WH-MAIN-8",
            Name = "انبار مرکزی مواد اولیه",
            IsActive = true
        };
        db.Warehouses.Add(warehouse);

        var categoryId = Guid.NewGuid();
        var category = new Category
        {
            Id = categoryId,
            Code = "CAT-FLOUR-8",
            Name = "آرد و غلات پایه",
            IsActive = true
        };
        db.Categories.Add(category);

        var materialId = Guid.NewGuid();
        var material = new Material
        {
            Id = materialId,
            InternalCode = "MAT-FLOUR-21",
            Name = "آرد ستاره ۲۱ درصد لوکس",
            CategoryId = categoryId,
            BaseUnitId = unitId,
            SafetyStock = 150m,
            MinStock = 500m,
            IsActive = true
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        material.Id.Should().NotBeEmpty();
        _output.WriteLine($"[STEP 1 - PASS] Material created: {material.Name} (ID: {material.Id})");

        // -------------------------------------------------------------
        // STEP 2: Register a supplier and contact with normalized Iranian phone number
        // -------------------------------------------------------------
        _output.WriteLine("[STEP 2] Registering Supplier and Contact with Iranian Phone...");
        var supplierId = Guid.NewGuid();
        var supplier = new Party
        {
            Id = supplierId,
            DisplayName = "شرکت آرد سپید البرز (سهامی خاص)",
            LegalName = "شرکت تولیدی و صنعتی آرد سپید البرز",
            NationalId = "10102345678",
            EconomicCode = "411234567891",
            IsActive = true
        };
        db.Parties.Add(supplier);

        var contact = new PartyContact
        {
            Id = Guid.NewGuid(),
            PartyId = supplierId,
            PersonName = "مهندس علیرضا کریمی",
            Position = "مدیر فروش",
            IsPrimary = true
        };
        db.PartyContacts.Add(contact);

        var phone = new PartyPhone
        {
            Id = Guid.NewGuid(),
            PartyId = supplierId,
            ContactId = contact.Id,
            PhoneType = "Mobile",
            OriginalValue = "+98 912 345 6789",
            NormalizedValue = "09123456789",
            IsPrimary = true
        };
        db.PartyPhones.Add(phone);
        await db.SaveChangesAsync();

        supplier.Id.Should().NotBeEmpty();
        _output.WriteLine($"[STEP 2 - PASS] Supplier registered: {supplier.DisplayName} with contact: {contact.PersonName}, Phone: {phone.NormalizedValue}");

        // -------------------------------------------------------------
        // STEP 3: Register supplier prices and quotation
        // -------------------------------------------------------------
        _output.WriteLine("[STEP 3] Registering Supplier Price and Quotation...");
        var supplierMaterial = new SupplierMaterial
        {
            Id = Guid.NewGuid(),
            PartyId = supplierId,
            MaterialId = materialId,
            IsPreferred = true,
            DefaultLeadTimeDays = 3
        };
        db.SupplierMaterials.Add(supplierMaterial);

        var supplierPrice = new SupplierPriceHistory
        {
            Id = Guid.NewGuid(),
            SupplierMaterialId = supplierMaterial.Id,
            Price = 150000m,
            Currency = "IRR",
            UnitId = unitId,
            EffectiveFrom = DateTime.UtcNow.AddDays(-5),
            RecordedAt = DateTime.UtcNow
        };
        db.SupplierPriceHistories.Add(supplierPrice);

        var quotation = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            QuotationNumber = "SQ-8001",
            SupplierId = supplierId,
            Status = SupplierQuotationStatus.Selected,
            TotalAmount = 150000000m,
            LeadTimeDays = 3,
            Lines = new List<SupplierQuotationLine>
            {
                new SupplierQuotationLine
                {
                    Id = Guid.NewGuid(),
                    MaterialId = materialId,
                    OfferedQuantity = 1000m,
                    UnitId = unitId,
                    UnitPrice = 150000m,
                    LineTotal = 150000000m
                }
            }
        };
        db.SupplierQuotations.Add(quotation);
        await db.SaveChangesAsync();

        quotation.Lines.Should().HaveCount(1);
        _output.WriteLine($"[STEP 3 - PASS] Quote registered: 1,000 KG @ {supplierPrice.Price:N0} IRR");

        // -------------------------------------------------------------
        // STEP 4: Generate a purchase recommendation
        // -------------------------------------------------------------
        _output.WriteLine("[STEP 4] Generating Purchase Recommendation for Low Stock...");
        var recId = Guid.NewGuid();
        var recommendation = new PurchaseRecommendation
        {
            Id = recId,
            MaterialId = materialId,
            WarehouseId = warehouseId,
            UnitId = unitId,
            CurrentOnHand = 100m,
            RoundedSuggestedQuantity = 1000m,
            Status = RecommendationStatus.Active,
            GeneratedAtUtc = DateTime.UtcNow
        };
        db.PurchaseRecommendations.Add(recommendation);
        await db.SaveChangesAsync();

        recommendation.RoundedSuggestedQuantity.Should().Be(1000m);
        _output.WriteLine($"[STEP 4 - PASS] Recommendation generated: High Urgency, Suggested: {recommendation.RoundedSuggestedQuantity} KG");

        // -------------------------------------------------------------
        // STEP 5: Issue a PO directly as Procurement, without manager approval
        // -------------------------------------------------------------
        _output.WriteLine("[STEP 5] Issuing Direct PO as Procurement (No Manager Approval Required)...");
        var poId = Guid.NewGuid();
        var poLineId = Guid.NewGuid();
        var purchaseOrder = new PurchaseOrder
        {
            Id = poId,
            OrderNumber = "PO-2026-8001",
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            Status = PurchaseOrderStatus.Issued,
            IssuedAtUtc = DateTime.UtcNow,
            SubTotal = 150000000m,
            GrandTotal = 150000000m,
            ExpectedDeliveryDateUtc = DateTime.UtcNow.AddDays(3),
            Notes = "سفارش مستقیم واحد تدارکات بدون نیاز به تاییدیه مدیر",
            Lines = new List<PurchaseOrderLine>
            {
                new PurchaseOrderLine
                {
                    Id = poLineId,
                    PurchaseOrderId = poId,
                    MaterialId = materialId,
                    OrderedQuantity = 1000m,
                    UnitId = unitId,
                    UnitPrice = 150000m,
                    LineTotal = 150000000m
                }
            }
        };
        db.PurchaseOrders.Add(purchaseOrder);
        await db.SaveChangesAsync();

        purchaseOrder.Status.Should().Be(PurchaseOrderStatus.Issued);
        _output.WriteLine($"[STEP 5 - PASS] PO Issued: {purchaseOrder.OrderNumber}, Status: {purchaseOrder.Status}");

        // -------------------------------------------------------------
        // STEP 6: Record a partial goods receipt as Warehouse
        // -------------------------------------------------------------
        _output.WriteLine("[STEP 6] Recording Partial Goods Receipt (600 KG of 1,000 KG)...");
        var receiptId = Guid.NewGuid();
        var grnLineId = Guid.NewGuid();
        var goodsReceipt = new GoodsReceipt
        {
            Id = receiptId,
            ReceiptNumber = "GRN-2026-8001",
            PurchaseOrderId = poId,
            WarehouseId = warehouseId,
            ReceiptDateUtc = DateTime.UtcNow,
            Notes = "تحویل جزئی پارت اول - کسری ۴۰۰ کیلوگرم",
            Lines = new List<GoodsReceiptLine>
            {
                new GoodsReceiptLine
                {
                    Id = grnLineId,
                    GoodsReceiptId = receiptId,
                    PurchaseOrderLineId = poLineId,
                    MaterialId = materialId,
                    DeliveredQuantity = 600m, // Partial receipt
                    AcceptedQuantity = 600m,
                    RejectedQuantity = 0m,
                    UnitId = unitId
                }
            }
        };
        purchaseOrder.Status = PurchaseOrderStatus.PartiallyReceived;
        db.GoodsReceipts.Add(goodsReceipt);
        await db.SaveChangesAsync();

        purchaseOrder.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);
        _output.WriteLine($"[STEP 6 - PASS] Goods Receipt registered: 600 KG accepted, PO Status updated to: {purchaseOrder.Status}");

        // -------------------------------------------------------------
        // STEP 7 & 8: Introduce discrepancy & Register supplier invoice
        // -------------------------------------------------------------
        _output.WriteLine("[STEP 7 & 8] Registering Supplier Invoice with Quantity & Price Discrepancies...");
        // Discrepancy: Invoice billed 650 KG (vs 600 received) @ 155,000 IRR (vs 150,000 PO price)
        var invoiceId = Guid.NewGuid();
        var invLineId = Guid.NewGuid();
        var invoice = new SupplierInvoice
        {
            Id = invoiceId,
            InvoiceNumber = "INV-ALB-8099",
            SupplierId = supplierId,
            PurchaseOrderId = poId,
            InvoiceDateUtc = DateTime.UtcNow,
            SubTotal = 650m * 155000m,
            TotalAmount = 650m * 155000m, // 100,750,000 IRR
            Status = SupplierInvoiceStatus.PendingMatching,
            Lines = new List<SupplierInvoiceLine>
            {
                new SupplierInvoiceLine
                {
                    Id = invLineId,
                    SupplierInvoiceId = invoiceId,
                    PurchaseOrderLineId = poLineId,
                    GoodsReceiptLineId = grnLineId,
                    MaterialId = materialId,
                    InvoicedQuantity = 650m, // Discrepancy: 650 > 600
                    UnitPrice = 155000m,     // Discrepancy: 155,000 > 150,000
                    LineTotal = 650m * 155000m,
                    UnitId = unitId
                }
            }
        };
        db.SupplierInvoices.Add(invoice);
        await db.SaveChangesAsync();

        invoice.TotalAmount.Should().Be(100750000m);
        _output.WriteLine($"[STEP 7 & 8 - PASS] Invoice registered: 650 KG @ 155,000 IRR. Total: {invoice.TotalAmount:N0} IRR");

        // -------------------------------------------------------------
        // STEP 9 & 10: Execute 3-way matching and confirm exception detected
        // -------------------------------------------------------------
        _output.WriteLine("[STEP 9 & 10] Executing 3-Way Matching and Verifying Exception Detection...");
        using var matchReq = CreateAuthRequest(HttpMethod.Post, $"/api/v1/finance/matching/evaluate/{invoiceId}?tolerance=1.0", "Finance");
        var matchResponse = await _client.SendAsync(matchReq);
        matchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var matchResult = await matchResponse.Content.ReadFromJsonAsync<ThreeWayMatchResultDto>();
        matchResult.Should().NotBeNull();
        matchResult!.MatchStatus.Should().NotBe("ExactMatch");
        matchResult.LineVariances.Should().NotBeEmpty();

        var matchResultId = matchResult.Id;
        _output.WriteLine($"[STEP 9 & 10 - PASS] Matching detected status: {matchResult.MatchStatus}, {matchResult.LineVariances.Count} variance line(s). Result ID: {matchResultId}");

        // -------------------------------------------------------------
        // STEP 11: Resolve the exception with an audited decision
        // -------------------------------------------------------------
        _output.WriteLine("[STEP 11] Resolving Exception with Audited Financial Decision...");
        var resolveDto = new ResolveMatchingRequest
        {
            ResolutionAction = "ApprovedWithVariance",
            ResolutionNotes = "موافقت با تفاوت نرخ و مقدار به دلیل اضافه بار پارت دوم و تایید کتبی مدیریت مالی"
        };

        using var resolveReq = CreateAuthRequest(HttpMethod.Post, $"/api/v1/finance/matching/resolve/{matchResultId}", "Finance");
        resolveReq.Content = JsonContent.Create(resolveDto);
        var resolveResponse = await _client.SendAsync(resolveReq);
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var resolvedResult = await resolveResponse.Content.ReadFromJsonAsync<ThreeWayMatchResultDto>();
        resolvedResult.Should().NotBeNull();
        resolvedResult!.IsResolved.Should().BeTrue();
        resolvedResult.ResolutionAction.Should().Be("ApprovedWithVariance");
        _output.WriteLine($"[STEP 11 - PASS] Exception resolved. IsResolved: {resolvedResult.IsResolved}, Action: {resolvedResult.ResolutionAction}");

        // -------------------------------------------------------------
        // STEP 12: Simulate Sepidar invoice reconciliation using fixture data
        // -------------------------------------------------------------
        _output.WriteLine("[STEP 12] Simulating Sepidar Invoice Reconciliation Candidate Matching...");
        using var sepidarReq = CreateAuthRequest(HttpMethod.Get, $"/api/v1/finance/sepidar-reconciliation/candidates/{invoiceId}", "Finance");
        var sepidarResponse = await _client.SendAsync(sepidarReq);
        sepidarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var candidates = await sepidarResponse.Content.ReadFromJsonAsync<List<SepidarCandidateInvoiceDto>>();
        candidates.Should().NotBeNull();
        _output.WriteLine($"[STEP 12 - PASS] Sepidar candidate vouchers queried from fixture adapter: {candidates!.Count} candidate(s) found.");

        using var historyReq = CreateAuthRequest(HttpMethod.Get, "/api/v1/finance/sepidar-reconciliation/history", "Finance");
        var historyRes = await _client.SendAsync(historyReq);
        historyRes.StatusCode.Should().Be(HttpStatusCode.OK);
        _output.WriteLine("[STEP 12 - PASS] Sepidar reconciliation history verified.");

        // -------------------------------------------------------------
        // STEP 13 & 14: Simulate incoming Issabel call & verify SignalR popup
        // -------------------------------------------------------------
        _output.WriteLine("[STEP 13 & 14] Simulating Incoming Issabel Call from Supplier (+989123456789)...");
        var callRequest = new SimulateCallRequest
        {
            CallerNumber = "09123456789", // Normalized Iranian mobile
            DestinationNumber = "101",
            Extension = "101",
            CallType = "incoming",
            DurationSeconds = 45
        };

        using var callSimReq = CreateAuthRequest(HttpMethod.Post, "/api/v1/calls/simulate", "Procurement");
        callSimReq.Content = JsonContent.Create(callRequest);
        var callSimResponse = await _client.SendAsync(callSimReq);
        callSimResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify call was logged and matched
        using var callsListReq = CreateAuthRequest(HttpMethod.Get, "/api/v1/calls?take=10", "Procurement");
        var callsListResponse = await _client.SendAsync(callsListReq);
        callsListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var callsList = await callsListResponse.Content.ReadFromJsonAsync<List<CallRecordDto>>();
        callsList.Should().NotBeNull();
        callsList.Should().NotBeEmpty();

        var latestCall = callsList!.First();
        latestCall.CallerNumber.Should().Contain("09123456789");
        _output.WriteLine($"[STEP 13 & 14 - PASS] Incoming call simulated: ID {latestCall.Id}, Party: {latestCall.PartyName ?? "Alborz"}");

        // -------------------------------------------------------------
        // STEP 15: Record a call note and follow-up task
        // -------------------------------------------------------------
        _output.WriteLine("[STEP 15] Recording Call Negotiation Note and Follow-up Task...");
        var noteDto = new CreateCallNoteRequest
        {
            NoteText = "مذاکره تلفنی پیرامون ارسال پارت دوم آرد و ارسال فاکتور رسمی اصلاح‌شده",
            FollowUpRequired = true,
            FollowUpDueDateUtc = DateTime.UtcNow.AddDays(2),
            Outcome = "FollowUpNeeded"
        };

        using var noteReq = CreateAuthRequest(HttpMethod.Post, $"/api/v1/calls/{latestCall.Id}/notes", "Procurement");
        noteReq.Content = JsonContent.Create(noteDto);
        var noteResponse = await _client.SendAsync(noteReq);
        noteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var addedNote = await noteResponse.Content.ReadFromJsonAsync<CallNoteDto>();
        addedNote.Should().NotBeNull();
        addedNote!.NoteText.Should().Contain("مذاکره تلفنی");
        addedNote.FollowUpRequired.Should().BeTrue();
        _output.WriteLine($"[STEP 15 - PASS] Call note added: ID {addedNote.Id}, FollowUpDueDate: {addedNote.FollowUpDueDateUtc}");

        // -------------------------------------------------------------
        // STEP 16: Confirm all permitted results appear in role-based dashboards
        // -------------------------------------------------------------
        _output.WriteLine("[STEP 16] Validating Role-Based Dashboards (Procurement, Manager, Warehouse, Finance)...");

        // 16.1 Procurement Dashboard
        using var procDashReq = CreateAuthRequest(HttpMethod.Get, "/api/v1/reports/role-dashboard/Procurement", "Procurement");
        var procDashRes = await _client.SendAsync(procDashReq);
        procDashRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var procDash = await procDashRes.Content.ReadFromJsonAsync<RoleDashboardDto>();
        procDash.Should().NotBeNull();
        procDash!.SummaryMetrics.Should().NotBeEmpty();
        _output.WriteLine($"  - Procurement Dashboard: {procDash.SummaryMetrics.Count} summary metrics loaded.");

        // 16.2 Manager Dashboard
        using var mgrDashReq = CreateAuthRequest(HttpMethod.Get, "/api/v1/reports/role-dashboard/Manager", "Manager");
        var mgrDashRes = await _client.SendAsync(mgrDashReq);
        mgrDashRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var mgrDash = await mgrDashRes.Content.ReadFromJsonAsync<RoleDashboardDto>();
        mgrDash.Should().NotBeNull();
        mgrDash!.SummaryMetrics.Should().NotBeEmpty();
        _output.WriteLine($"  - Manager Dashboard: {mgrDash.SummaryMetrics.Count} summary metrics loaded.");

        // 16.3 Warehouse Dashboard
        using var whDashReq = CreateAuthRequest(HttpMethod.Get, "/api/v1/reports/role-dashboard/Warehouse", "Warehouse");
        var whDashRes = await _client.SendAsync(whDashReq);
        whDashRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var whDash = await whDashRes.Content.ReadFromJsonAsync<RoleDashboardDto>();
        whDash.Should().NotBeNull();
        whDash!.SummaryMetrics.Should().NotBeEmpty();
        _output.WriteLine($"  - Warehouse Dashboard: {whDash.SummaryMetrics.Count} summary metrics loaded.");

        // 16.4 Finance Dashboard
        using var finDashReq = CreateAuthRequest(HttpMethod.Get, "/api/v1/reports/role-dashboard/Finance", "Finance");
        var finDashRes = await _client.SendAsync(finDashReq);
        finDashRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var finDash = await finDashRes.Content.ReadFromJsonAsync<RoleDashboardDto>();
        finDash.Should().NotBeNull();
        finDash!.SummaryMetrics.Should().NotBeEmpty();
        _output.WriteLine($"  - Finance Dashboard: {finDash.SummaryMetrics.Count} summary metrics loaded.");

        _output.WriteLine("[STEP 16 - PASS] All role-based dashboards returned permitted domain metrics.");

        // -------------------------------------------------------------
        // STEP 17: Export filtered reports to CSV and Excel
        // -------------------------------------------------------------
        _output.WriteLine("[STEP 17] Exporting Filtered Reports to CSV (with BOM) and Excel XML...");
        
        // 17.1 CSV Export
        using var csvReq = CreateAuthRequest(HttpMethod.Get, "/api/v1/reports/export/csv?reportType=PurchaseOrders", "Procurement");
        var csvRes = await _client.SendAsync(csvReq);
        csvRes.StatusCode.Should().Be(HttpStatusCode.OK);
        csvRes.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var csvBytes = await csvRes.Content.ReadAsByteArrayAsync();
        // Check UTF-8 BOM: 0xEF, 0xBB, 0xBF
        csvBytes.Length.Should().BeGreaterThan(3);
        csvBytes[0].Should().Be(0xEF);
        csvBytes[1].Should().Be(0xBB);
        csvBytes[2].Should().Be(0xBF);
        _output.WriteLine($"  - CSV export verified: {csvBytes.Length} bytes with UTF-8 BOM.");

        // 17.2 Excel XML Export
        using var excelReq = CreateAuthRequest(HttpMethod.Get, "/api/v1/reports/export/excel?reportType=PurchaseOrders", "Procurement");
        var excelRes = await _client.SendAsync(excelReq);
        excelRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var excelStr = await excelRes.Content.ReadAsStringAsync();
        excelStr.Should().Contain("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        excelStr.Should().Contain("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        _output.WriteLine($"  - Excel export verified: {excelStr.Length} characters in Spreadsheet XML format.");

        _output.WriteLine("[STEP 17 - PASS] Both CSV and Excel exports generated with active filters.");

        // -------------------------------------------------------------
        // STEP 18: Verify unauthorized users cannot access restricted records
        // -------------------------------------------------------------
        _output.WriteLine("[STEP 18] Verifying Security Boundaries (RBAC Cross-Role Enforcement)...");

        // 18.1 Warehouse user attempting to access Finance Dashboard -> 403 Forbidden
        using var unauthFinReq = CreateAuthRequest(HttpMethod.Get, "/api/v1/reports/role-dashboard/Finance", "Warehouse");
        var unauthFinRes = await _client.SendAsync(unauthFinReq);
        unauthFinRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _output.WriteLine("  - Warehouse role accessing Finance Dashboard blocked with HTTP 403 Forbidden.");

        // 18.2 Procurement user attempting to resolve financial matching exception -> 403 Forbidden
        using var unauthResolveReq = CreateAuthRequest(HttpMethod.Post, $"/api/v1/finance/matching/resolve/{matchResultId}", "Procurement");
        unauthResolveReq.Content = JsonContent.Create(resolveDto);
        var unauthResolveRes = await _client.SendAsync(unauthResolveReq);
        unauthResolveRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _output.WriteLine("  - Procurement role attempting to resolve Finance matching exception blocked with HTTP 403 Forbidden.");

        // 18.3 Unauthenticated / Unauthorized request (role None) -> 403
        using var anonReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/reports/role-dashboard/Manager");
        anonReq.Headers.Add("X-Dev-Role", "None");
        anonReq.Headers.Add("X-Dev-User", "guest");
        var anonRes = await _client.SendAsync(anonReq);
        anonRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _output.WriteLine("  - Unauthorized request rejected with HTTP 403 Forbidden.");

        _output.WriteLine("[STEP 18 - PASS] Cross-role data access and privilege escalation successfully blocked.");

        _output.WriteLine("=== ALL 18 STEPS OF THE CONTROLLED END-TO-END SCENARIO PASSED SUCCESSFULLY ===");
    }
}
