using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ParkPalace.Api.Controllers;
using ParkPalace.Application.DTOs;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using Xunit;
using Xunit.Abstractions;

namespace ParkPalace.IntegrationTests;

public class PurchaseWorkflowExecutionTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public PurchaseWorkflowExecutionTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
    }

    private HttpRequestMessage CreateAuthRequest(HttpMethod method, string uri, string role = "Procurement")
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-Dev-Role", role);
        request.Headers.Add("X-Dev-User", $"sahar\\{role.ToLower()}");
        return request;
    }

    [Fact]
    public async Task ExecuteRealPurchaseWorkflow_WithMockData_InIsolatedEnvironment()
    {
        _output.WriteLine("================================================================================");
        _output.WriteLine(" REAL EXECUTION: PURCHASE WORKFLOW (DRAFT -> ISSUE -> GOODS RECEIPT)");
        _output.WriteLine(" Environment: Isolated Test Environment (InMemory / WebApplicationFactory)");
        _output.WriteLine(" Engine: ASP.NET Core 8 Full HTTP Pipeline + EF Core In-Memory Engine");
        _output.WriteLine("================================================================================");

        // -------------------------------------------------------------
        // PREPARATION: Seed Required Base Entities in Isolated DB
        // -------------------------------------------------------------
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

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
            Code = "WH-ISOLATED-1",
            Name = "انبار مرکزی مواد اولیه (محیط ایزوله)",
            IsActive = true
        };
        db.Warehouses.Add(warehouse);

        var categoryId = Guid.NewGuid();
        var category = new Category
        {
            Id = categoryId,
            Code = "CAT-FLOUR-TEST",
            Name = "آرد و غلات صنعتی",
            IsActive = true
        };
        db.Categories.Add(category);

        var materialId = Guid.NewGuid();
        var material = new Material
        {
            Id = materialId,
            InternalCode = "MAT-FLOUR-SPECIAL",
            Name = "آرد گندم ستاره ۲۱ درصد نانوایی",
            CategoryId = categoryId,
            BaseUnitId = unitId,
            SafetyStock = 200m,
            MinStock = 600m,
            IsActive = true
        };
        db.Materials.Add(material);

        var supplierId = Guid.NewGuid();
        var supplier = new Party
        {
            Id = supplierId,
            DisplayName = "شرکت بازرگانی غلات پارس نگین (سهامی خاص)",
            LegalName = "شرکت بازرگانی و پخش غلات پارس نگین البرز",
            NationalId = "14008765432",
            EconomicCode = "411567891234",
            IsActive = true
        };
        db.Parties.Add(supplier);

        await db.SaveChangesAsync();

        _output.WriteLine($"[پیش‌نیاز] کالای آزمایشی ثبت شد: {material.Name} (کد: {material.InternalCode}, شناسه: {material.Id})");
        _output.WriteLine($"[پیش‌نیاز] تأمین‌کننده ثبت شد: {supplier.DisplayName} (شناسه: {supplier.Id})");
        _output.WriteLine($"[پیش‌نیاز] انبار ثبت شد: {warehouse.Name} (شناسه: {warehouse.Id})");

        // =============================================================
        // STEP 1: CREATE PURCHASE ORDER (DRAFT)
        // =============================================================
        _output.WriteLine("\n--------------------------------------------------------------------------------");
        _output.WriteLine(" مرحله ۱: ثبت سفارش خرید در وضعیت پیش‌نویس (Draft Creation)");
        _output.WriteLine("--------------------------------------------------------------------------------");

        var createOrderPayload = new CreatePurchaseOrderRequest
        {
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            ExpectedDeliveryDateUtc = DateTime.UtcNow.AddDays(5),
            PaymentTerms = "نقدی ۳۰ روزه با ۲ درصد تخفیف تسویه نقدی",
            DeliveryTerms = "تحویل درب کارخانه - هزینه حمل با خریدار",
            Notes = "سفارش آزمایشی غلات ویژه تولید نان فانتزی پاییزه",
            DirectIssue = false, // Explicitly keep as DRAFT to verify issue lifecycle
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new()
                {
                    MaterialId = materialId,
                    UnitId = unitId,
                    OrderedQuantity = 500m,
                    UnitPrice = 250000m, // 250,000 IRR per KG
                    TaxRate = 0.10m,
                    DiscountAmount = 2500000m // 2,500,000 IRR discount
                }
            }
        };

        // 1.1 RBAC Enforcement Check: Warehouse role attempting to create Purchase Order -> Must be 403
        using (var unauthCreateReq = CreateAuthRequest(HttpMethod.Post, "/api/v1/procurement/orders", "Warehouse"))
        {
            unauthCreateReq.Content = JsonContent.Create(createOrderPayload);
            var unauthCreateRes = await _client.SendAsync(unauthCreateReq);
            unauthCreateRes.StatusCode.Should().Be(HttpStatusCode.Forbidden, 
                "کاربر با نقش انباردار نباید مجاز به ثبت پیش‌نویس سفارش خرید باشد");
            _output.WriteLine("  [آزمون مجوز ۱] تلاش نقش Warehouse برای ثبت سفارش: رد شد با کد ۴۰۳ (HTTP 403 Forbidden)");
        }

        // 1.2 Authorized Execution: Procurement role creates Purchase Order
        PurchaseOrderDto? createdOrder;
        using (var authCreateReq = CreateAuthRequest(HttpMethod.Post, "/api/v1/procurement/orders", "Procurement"))
        {
            authCreateReq.Content = JsonContent.Create(createOrderPayload);
            var authCreateRes = await _client.SendAsync(authCreateReq);
            authCreateRes.StatusCode.Should().Be(HttpStatusCode.Created, 
                "کاربر با نقش تدارکات باید سفارش خرید را با وضعیت پیش‌نویس ایجاد کند");

            createdOrder = await authCreateRes.Content.ReadFromJsonAsync<PurchaseOrderDto>();
        }

        createdOrder.Should().NotBeNull();
        createdOrder!.Id.Should().NotBeEmpty();
        createdOrder.Status.Should().Be("Draft", "وضعیت اولیه سفارش باید پیش‌نویس (Draft) باشد");
        createdOrder.GrandTotal.Should().Be(135000000m); // (500 * 250,000) - 2.5M + 10% tax = 122.5M + 12.5M = 135,000,000

        _output.WriteLine($"  [نتیجه مرحله ۱]");
        _output.WriteLine($"    شناسه سفارش (Sample ID): {createdOrder.Id}");
        _output.WriteLine($"    شماره سیستمی (OrderNumber): {createdOrder.OrderNumber}");
        _output.WriteLine($"    وضعیت فعلی (Status): {createdOrder.Status} (پیش‌نویس)");
        _output.WriteLine($"    مبلغ کل (GrandTotal): {createdOrder.GrandTotal:N0} ریال");
        _output.WriteLine($"    شرایط پرداخت فارسی: {createdOrder.PaymentTerms}");
        _output.WriteLine($"    شرایط تحویل فارسی: {createdOrder.DeliveryTerms}");
        _output.WriteLine($"    یادداشت سفارش: {createdOrder.Notes}");
        _output.WriteLine($"    تاریخ ثبت (CreatedAtUtc): {createdOrder.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        _output.WriteLine($"    کاربر ثبت‌کننده (CreatedBy): {createdOrder.CreatedBy}");
        _output.WriteLine($"    توکن همزمانی ثبت‌شده: '{createdOrder.ConcurrencyToken}'");

        // =============================================================
        // STEP 2: ISSUE PURCHASE ORDER (DRAFT -> ISSUED)
        // =============================================================
        _output.WriteLine("\n--------------------------------------------------------------------------------");
        _output.WriteLine(" مرحله ۲: صدور و تأیید سفارش خرید (Issue / Direct Issuance)");
        _output.WriteLine("--------------------------------------------------------------------------------");

        var issuePayload = new IssueOrderPayload
        {
            ConcurrencyToken = createdOrder.ConcurrencyToken
        };

        // 2.1 RBAC Enforcement Check: Warehouse role attempting to issue Purchase Order -> Must be 403
        using (var unauthIssueReq = CreateAuthRequest(HttpMethod.Post, $"/api/v1/procurement/orders/{createdOrder.Id}/issue", "Warehouse"))
        {
            unauthIssueReq.Content = JsonContent.Create(issuePayload);
            var unauthIssueRes = await _client.SendAsync(unauthIssueReq);
            unauthIssueRes.StatusCode.Should().Be(HttpStatusCode.Forbidden, 
                "کاربر با نقش انباردار نباید مجاز به صدور سفارش خرید باشد");
            _output.WriteLine("  [آزمون مجوز ۲] تلاش نقش Warehouse برای صدور سفارش: رد شد با کد ۴۰۳ (HTTP 403 Forbidden)");
        }

        // 2.2 Authorized Execution: Procurement role issues Purchase Order
        PurchaseOrderDto? issuedOrder;
        using (var authIssueReq = CreateAuthRequest(HttpMethod.Post, $"/api/v1/procurement/orders/{createdOrder.Id}/issue", "Procurement"))
        {
            authIssueReq.Content = JsonContent.Create(issuePayload);
            var authIssueRes = await _client.SendAsync(authIssueReq);
            var issueBody = await authIssueRes.Content.ReadAsStringAsync();
            _output.WriteLine($"  [پاسخ صدور] StatusCode: {authIssueRes.StatusCode}, Body: {issueBody}");
            authIssueRes.StatusCode.Should().Be(HttpStatusCode.OK, 
                "کاربر با نقش تدارکات باید سفارش خرید را صادر کند");

            issuedOrder = System.Text.Json.JsonSerializer.Deserialize<PurchaseOrderDto>(issueBody, 
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        issuedOrder.Should().NotBeNull();
        issuedOrder!.Status.Should().Be("Issued", "پس از صدور، وضعیت سفارش باید صادر شده (Issued) باشد");
        issuedOrder.IssuedAtUtc.Should().NotBeNull();
        issuedOrder.IssuedByUsername.Should().Be("sahar\\procurement");

        _output.WriteLine($"  [نتیجه مرحله ۲]");
        _output.WriteLine($"    وضعیت قبل از اقدام: {createdOrder.Status} (Draft)");
        _output.WriteLine($"    وضعیت بعد از اقدام: {issuedOrder.Status} (Issued / صادر شده)");
        _output.WriteLine($"    کاربر صادرکننده: {issuedOrder.IssuedByUsername}");
        _output.WriteLine($"    زمان دقیق صدور (IssuedAtUtc): {issuedOrder.IssuedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        _output.WriteLine($"    توکن همزمانی جدید: {issuedOrder.ConcurrencyToken}");

        // =============================================================
        // STEP 3: GOODS RECEIPT AT WAREHOUSE (ISSUED -> FULLY RECEIVED)
        // =============================================================
        _output.WriteLine("\n--------------------------------------------------------------------------------");
        _output.WriteLine(" مرحله ۳: رسید انبار و دریافت اقلام کالا (Goods Receipt)");
        _output.WriteLine("--------------------------------------------------------------------------------");

        var poLine = issuedOrder.Lines[0];
        var receiptPayload = new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = issuedOrder.Id,
            WarehouseId = warehouseId,
            CarrierName = "شرکت باربری سریع ترابر تهران",
            DeliveryNoteNumber = "DN-1405-9921",
            VehiclePlateNumber = "ایران ۱۱ - ۴۵۶ ج ۷۸",
            Notes = "تحویل کامل محموله با باسکول مرکزی و تایید کیفی آزمایشگاه آرد",
            Lines = new List<CreateGoodsReceiptLineRequest>
            {
                new()
                {
                    PurchaseOrderLineId = poLine.Id,
                    DeliveredQuantity = 500m,
                    AcceptedQuantity = 500m,
                    RejectedQuantity = 0m,
                    QcStatus = "Passed",
                    RejectionReason = null,
                    BatchNumber = "BATCH-1405-FLOUR-88",
                    ExpiryDateUtc = DateTime.UtcNow.AddMonths(6),
                    StorageLocation = "سوله شماره ۲ - پالت آرد",
                    Notes = "کیفیت و دانه‌بندی آرد مطابق استاندارد است"
                }
            }
        };

        // 3.1 RBAC Enforcement Check: Procurement role attempting to create Goods Receipt -> Must be 403
        using (var unauthReceiptReq = CreateAuthRequest(HttpMethod.Post, "/api/v1/warehouse/receipts", "Procurement"))
        {
            unauthReceiptReq.Content = JsonContent.Create(receiptPayload);
            var unauthReceiptRes = await _client.SendAsync(unauthReceiptReq);
            unauthReceiptRes.StatusCode.Should().Be(HttpStatusCode.Forbidden, 
                "کاربر با نقش تدارکات نباید مجاز به ثبت رسید انبار کالا باشد");
            _output.WriteLine("  [آزمون مجوز ۳] تلاش نقش Procurement برای ثبت رسید انبار: رد شد با کد ۴۰۳ (HTTP 403 Forbidden)");
        }

        // 3.2 Authorized Execution: Warehouse role creates Goods Receipt
        GoodsReceiptDto? createdReceipt;
        using (var authReceiptReq = CreateAuthRequest(HttpMethod.Post, "/api/v1/warehouse/receipts", "Warehouse"))
        {
            authReceiptReq.Content = JsonContent.Create(receiptPayload);
            var authReceiptRes = await _client.SendAsync(authReceiptReq);
            authReceiptRes.StatusCode.Should().Be(HttpStatusCode.Created, 
                "کاربر با نقش سرپرست انبار باید رسید تحویل کالا را با موفقیت ثبت کند");

            createdReceipt = await authReceiptRes.Content.ReadFromJsonAsync<GoodsReceiptDto>();
        }

        createdReceipt.Should().NotBeNull();
        createdReceipt!.Id.Should().NotBeEmpty();
        createdReceipt.ReceiptNumber.Should().StartWith("GRN-");
        createdReceipt.Lines.Should().HaveCount(1);
        createdReceipt.Lines[0].AcceptedQuantity.Should().Be(500m);

        // 3.3 Verify PO Status Transition after Receipt
        PurchaseOrderDto? finalOrderState;
        using (var getPoReq = CreateAuthRequest(HttpMethod.Get, $"/api/v1/procurement/orders/{issuedOrder.Id}", "Procurement"))
        {
            var getPoRes = await _client.SendAsync(getPoReq);
            getPoRes.StatusCode.Should().Be(HttpStatusCode.OK);
            finalOrderState = await getPoRes.Content.ReadFromJsonAsync<PurchaseOrderDto>();
        }

        finalOrderState.Should().NotBeNull();
        finalOrderState!.Status.Should().Be("FullyReceived", 
            "پس از تحویل کامل مقدار ۵۰۰ کیلوگرم، وضعیت سفارش باید دریافت کامل (FullyReceived) باشد");

        _output.WriteLine($"  [نتیجه مرحله ۳]");
        _output.WriteLine($"    شناسه رسید انبار: {createdReceipt.Id}");
        _output.WriteLine($"    شماره سند رسید (ReceiptNumber): {createdReceipt.ReceiptNumber}");
        _output.WriteLine($"    مقدار تحویل و تاییدشده: {createdReceipt.Lines[0].AcceptedQuantity} کیلوگرم");
        _output.WriteLine($"    شماره بهر/بچ کالا: {createdReceipt.Lines[0].BatchNumber}");
        _output.WriteLine($"    یادداشت انبارداری: {createdReceipt.Notes}");
        _output.WriteLine($"    وضعیت سفارش قبل از رسید: {issuedOrder.Status} (Issued)");
        _output.WriteLine($"    وضعیت سفارش بعد از رسید: {finalOrderState.Status} (FullyReceived / دریافت کامل)");

        _output.WriteLine("\n================================================================================");
        _output.WriteLine(" گردش خرید آزمایشی با داده‌های ساختگی فارسی و کنترل مجوزها با موفقیت ۱۰۰٪ اجرا شد");
        _output.WriteLine("================================================================================");
    }
}
