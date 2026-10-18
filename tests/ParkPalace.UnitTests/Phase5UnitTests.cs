using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.DTOs;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Services;
using Xunit;

namespace ParkPalace.UnitTests;

public class Phase5UnitTests
{
    private static CrmDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new CrmDbContext(options);

        // Seed basic dependencies
        var supplier = new Party
        {
            Id = Guid.NewGuid(),
            DisplayName = "شرکت آرد ستاره البرز",
            NationalIdentifier = "10102345678"
        };
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = "WH-MAIN",
            Name = "انبار مرکزی کارخانه نان"
        };
        var unit = new UnitOfMeasure
        {
            Id = Guid.NewGuid(),
            Code = "KG",
            Name = "کیلوگرم"
        };
        var flour = new Material
        {
            Id = Guid.NewGuid(),
            InternalCode = "MAT-FLOUR-STAR",
            Name = "آرد گندم ستاره نان فانتزی",
            BaseUnitId = unit.Id
        };

        context.Parties.Add(supplier);
        context.Warehouses.Add(warehouse);
        context.UnitsOfMeasure.Add(unit);
        context.Materials.Add(flour);
        context.SaveChanges();

        return context;
    }

    [Fact]
    public async Task DirectPOIssuance_CreatesImmutableSnapshotsAndIssuedStatus()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var poService = new PurchaseOrderService(db);
        var supplier = await db.Parties.FirstAsync();
        var material = await db.Materials.FirstAsync();

        var createReq = new CreatePurchaseOrderRequest
        {
            SupplierId = supplier.Id,
            DirectIssue = true, // Direct PO issuance without manager approval
            PaymentTerms = "۳۰ روزه",
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new()
                {
                    MaterialId = material.Id,
                    OrderedQuantity = 10000m,
                    UnitPrice = 18500m,
                    TaxRate = 0.09m,
                    DiscountAmount = 500000m
                }
            }
        };

        // Act
        var result = await poService.CreateOrderAsync(createReq, "ProcurementOfficer");

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Issued");
        result.IssuedByUsername.Should().Be("ProcurementOfficer");
        result.GrandTotal.Should().Be((10000m * 18500m) + (10000m * 18500m * 0.09m) - 500000m);

        var line = result.Lines.First();
        line.MaterialCodeSnapshot.Should().Be("MAT-FLOUR-STAR");
        line.MaterialNameSnapshot.Should().Be("آرد گندم ستاره نان فانتزی");
        line.UnitCodeSnapshot.Should().Be("KG");
        line.SupplierNameSnapshot.Should().Be("شرکت آرد ستاره البرز");

        // Check Audit history
        result.Audits.Should().Contain(a => a.Action == "DirectlyIssuedByProcurement");
    }

    [Fact]
    public async Task GoodsReceipt_PartialAndOverReceiptProtection_EnforcesThresholds()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var poService = new PurchaseOrderService(db);
        var grService = new GoodsReceiptService(db);
        var supplier = await db.Parties.FirstAsync();
        var material = await db.Materials.FirstAsync();

        var po = await poService.CreateOrderAsync(new CreatePurchaseOrderRequest
        {
            SupplierId = supplier.Id,
            DirectIssue = true,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { MaterialId = material.Id, OrderedQuantity = 1000m, UnitPrice = 10000m }
            }
        }, "Buyer");

        var poLine = po.Lines.First();

        // Act 1: Partial Receipt of 600 kg
        var partialReceipt = await grService.CreateReceiptAsync(new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = po.Id,
            DeliveryNoteNumber = "DN-101",
            Lines = new List<CreateGoodsReceiptLineRequest>
            {
                new()
                {
                    PurchaseOrderLineId = poLine.Id,
                    DeliveredQuantity = 600m,
                    AcceptedQuantity = 600m,
                    QcStatus = "Passed"
                }
            }
        }, "WarehouseOfficer");

        // Assert 1: PO becomes PartiallyReceived
        var updatedPo1 = await poService.GetOrderByIdAsync(po.Id);
        updatedPo1!.Status.Should().Be("PartiallyReceived");
        updatedPo1.Lines.First().ReceivedQuantity.Should().Be(600m);

        // Act 2: Excessive delivery attempt exceeding 105% threshold (trying to receive 500kg more -> total 1100kg > 1050kg)
        var excessiveAction = () => grService.CreateReceiptAsync(new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = po.Id,
            Lines = new List<CreateGoodsReceiptLineRequest>
            {
                new()
                {
                    PurchaseOrderLineId = poLine.Id,
                    DeliveredQuantity = 500m,
                    AcceptedQuantity = 500m
                }
            }
        }, "WarehouseOfficer");

        // Assert 2
        await excessiveAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*سقف مجاز*");
    }

    [Fact]
    public async Task SupplierInvoice_DuplicateDetection_RejectsDuplicateInvoiceNumber()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var invService = new SupplierInvoiceService(db);
        var supplier = await db.Parties.FirstAsync();
        var material = await db.Materials.FirstAsync();

        var req1 = new CreateSupplierInvoiceRequest
        {
            InvoiceNumber = "INV-2026-9901",
            SupplierId = supplier.Id,
            Lines = new List<CreateSupplierInvoiceLineRequest>
            {
                new() { MaterialId = material.Id, InvoicedQuantity = 500m, UnitPrice = 15000m }
            }
        };

        // Act 1
        var inv1 = await invService.CreateInvoiceAsync(req1, "Accountant");
        inv1.Should().NotBeNull();

        // Act 2: Duplicate invoice registration
        var duplicateAction = () => invService.CreateInvoiceAsync(req1, "Accountant");

        // Assert 2
        await duplicateAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*فاکتور تکراری*");
    }

    [Fact]
    public async Task ThreeWayMatching_ExactMatch_ResolvesSuccessfully()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var poService = new PurchaseOrderService(db);
        var grService = new GoodsReceiptService(db);
        var invService = new SupplierInvoiceService(db);
        var matchEngine = new ThreeWayMatchingEngine(db);

        var supplier = await db.Parties.FirstAsync();
        var material = await db.Materials.FirstAsync();

        // 1. PO: 1000 kg @ 20,000 IRR
        var po = await poService.CreateOrderAsync(new CreatePurchaseOrderRequest
        {
            SupplierId = supplier.Id,
            DirectIssue = true,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { MaterialId = material.Id, OrderedQuantity = 1000m, UnitPrice = 20000m }
            }
        }, "Buyer");
        var poLine = po.Lines.First();

        // 2. Receipt: 1000 kg Accepted
        await grService.CreateReceiptAsync(new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = po.Id,
            Lines = new List<CreateGoodsReceiptLineRequest>
            {
                new() { PurchaseOrderLineId = poLine.Id, DeliveredQuantity = 1000m, AcceptedQuantity = 1000m, QcStatus = "Passed" }
            }
        }, "WarehouseKeeper");

        // 3. Invoice: 1000 kg @ 20,000 IRR
        var invoice = await invService.CreateInvoiceAsync(new CreateSupplierInvoiceRequest
        {
            InvoiceNumber = "INV-MATCH-01",
            SupplierId = supplier.Id,
            PurchaseOrderId = po.Id,
            Lines = new List<CreateSupplierInvoiceLineRequest>
            {
                new() { PurchaseOrderLineId = poLine.Id, MaterialId = material.Id, InvoicedQuantity = 1000m, UnitPrice = 20000m }
            }
        }, "FinanceUser");

        // Act
        var matchResult = await matchEngine.EvaluateMatchAsync(invoice.Id, "FinanceUser");

        // Assert
        matchResult.MatchStatus.Should().Be("ExactMatch");
        matchResult.IsResolved.Should().BeTrue();
        matchResult.TotalVarianceAmount.Should().Be(0m);
        matchResult.LineVariances.First().QuantityVariance.Should().Be(0m);
        matchResult.LineVariances.First().PriceVariance.Should().Be(0m);
    }

    [Fact]
    public async Task ThreeWayMatching_PriceAndQuantityMismatch_FlagsExceptions()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var poService = new PurchaseOrderService(db);
        var grService = new GoodsReceiptService(db);
        var invService = new SupplierInvoiceService(db);
        var matchEngine = new ThreeWayMatchingEngine(db);

        var supplier = await db.Parties.FirstAsync();
        var material = await db.Materials.FirstAsync();

        // 1. PO: 1000 kg @ 20,000 IRR
        var po = await poService.CreateOrderAsync(new CreatePurchaseOrderRequest
        {
            SupplierId = supplier.Id,
            DirectIssue = true,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { MaterialId = material.Id, OrderedQuantity = 1000m, UnitPrice = 20000m }
            }
        }, "Buyer");
        var poLine = po.Lines.First();

        // 2. Receipt: only 700 kg Accepted (partial receipt)
        await grService.CreateReceiptAsync(new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = po.Id,
            Lines = new List<CreateGoodsReceiptLineRequest>
            {
                new() { PurchaseOrderLineId = poLine.Id, DeliveredQuantity = 700m, AcceptedQuantity = 700m, QcStatus = "Passed" }
            }
        }, "WarehouseKeeper");

        // 3. Invoice: charged for 1000 kg @ 23,000 IRR (quantity overage + 15% price increase!)
        var invoice = await invService.CreateInvoiceAsync(new CreateSupplierInvoiceRequest
        {
            InvoiceNumber = "INV-MISMATCH-02",
            SupplierId = supplier.Id,
            PurchaseOrderId = po.Id,
            Lines = new List<CreateSupplierInvoiceLineRequest>
            {
                new() { PurchaseOrderLineId = poLine.Id, MaterialId = material.Id, InvoicedQuantity = 1000m, UnitPrice = 23000m }
            }
        }, "FinanceUser");

        // Act
        var matchResult = await matchEngine.EvaluateMatchAsync(invoice.Id, "FinanceUser", tolerancePercent: 1.0m);

        // Assert
        matchResult.IsResolved.Should().BeFalse();
        matchResult.MatchStatus.Should().Be("PriceMismatch"); // or QuantityMismatch flagged
        matchResult.TotalVarianceAmount.Should().BeGreaterThan(0m);

        var line = matchResult.LineVariances.First();
        line.QuantityVariance.Should().Be(300m); // Invoiced 1000 vs Received 700
        line.PriceVariance.Should().Be(3000m); // 23,000 vs 20,000
        line.PriceVariancePercent.Should().Be(15.0m);

        // Act 2: Resolve exception with Disputed status
        var resolved = await matchEngine.ResolveMatchAsync(matchResult.Id, new ResolveMatchingRequest
        {
            ResolutionAction = "Disputed",
            ResolutionNotes = "مغایرت نرخ و مقدار به تأمین‌کننده جهت صدور فاکتور اصلاحی اعلام گردید."
        }, "SeniorAccountant");

        resolved.IsResolved.Should().BeTrue();
        resolved.MatchStatus.Should().Be("Disputed");
    }

    [Fact]
    public async Task SepidarReconciliation_CandidateMatchingAndAuditedLink_Succeeds()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var invService = new SupplierInvoiceService(db);
        var fixtureAdapter = new SepidarFixtureFinancialAdapter();
        var reconService = new SepidarReconciliationService(db, fixtureAdapter);

        var supplier = await db.Parties.FirstAsync();
        var material = await db.Materials.FirstAsync();

        var invoice = await invService.CreateInvoiceAsync(new CreateSupplierInvoiceRequest
        {
            InvoiceNumber = "FLOUR-INV-1001",
            SupplierId = supplier.Id,
            Lines = new List<CreateSupplierInvoiceLineRequest>
            {
                new() { MaterialId = material.Id, InvoicedQuantity = 10000m, UnitPrice = 18750m } // Total 187,500,000 IRR
            }
        }, "FinanceUser");

        // Act 1: Find candidate Sepidar documents
        var candidates = await reconService.GetCandidatesForInvoiceAsync(invoice.Id);

        // Assert 1: Candidate SEP-INV-1001 should be High confidence match (187.5M matching Alborz Flour)
        candidates.Should().NotBeEmpty();
        var best = candidates.First();
        best.SepidarInvoiceId.Should().Be("SEP-INV-1001");
        best.MatchConfidenceLevel.Should().Be("High");
        best.ConfidenceScore.Should().BeGreaterThan(0.80m);

        // Act 2: Link invoice with audit
        var linked = await reconService.LinkInvoiceAsync(new LinkSepidarInvoiceRequest
        {
            SupplierInvoiceId = invoice.Id,
            SepidarInvoiceId = best.SepidarInvoiceId,
            SepidarVoucherNumber = best.SepidarVoucherNumber,
            SepidarVoucherDateUtc = best.VoucherDateUtc,
            SepidarAmount = best.Amount,
            AuditNotes = "تطبیق کامل فاکتور رسمی نهایی"
        }, "SeniorAccountant");

        linked.LinkStatus.Should().Be("Confirmed");
        var updatedInv = await invService.GetInvoiceByIdAsync(invoice.Id);
        updatedInv!.RegisteredInSepidar.Should().BeTrue();
        updatedInv.SepidarInvoiceRef.Should().Be("SEP-INV-1001");

        // Act 3: Audited Unlink
        var unlinked = await reconService.UnlinkInvoiceAsync(linked.Id, "اشتباه کاربری در ثبت سند", "SeniorAccountant");
        unlinked.LinkStatus.Should().Be("Unlinked");
        var revertedInv = await invService.GetInvoiceByIdAsync(invoice.Id);
        revertedInv!.RegisteredInSepidar.Should().BeFalse();
    }
}
