using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ParkPalace.Application.DTOs;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Services.Reporting;
using Xunit;

namespace ParkPalace.UnitTests;

public class Phase7UnitTests
{
    private CrmDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CrmDbContext(options);
    }

    [Fact]
    public async Task Kpi_ProcurementSummary_FulfillmentRate_CalculatedCorrectly_AndExcludesCancelled()
    {
        using var db = CreateInMemoryContext();
        var service = new ReportingService(db, NullLogger<ReportingService>.Instance);

        var supplier = new Party { Id = Guid.NewGuid(), DisplayName = "تأمین‌کننده گندم گلستان", IsActive = true };
        db.Parties.Add(supplier);

        var material = new Material { Id = Guid.NewGuid(), Name = "آرد ۲۱٪", InternalCode = "MAT-101", SafetyStock = 20, MinStock = 50 };
        db.Materials.Add(material);

        // PO 1: Issued - 100 ordered, 80 received
        var po1 = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = "PO-1001",
            SupplierId = supplier.Id,
            Status = PurchaseOrderStatus.PartiallyReceived,
            GrandTotal = 50000000m,
            IssuedAtUtc = DateTime.UtcNow.AddDays(-5),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
        };
        po1.Lines.Add(new PurchaseOrderLine
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = po1.Id,
            MaterialId = material.Id,
            OrderedQuantity = 100m,
            ReceivedQuantity = 80m,
            UnitPrice = 500000m,
            LineTotal = 50000000m
        });
        db.PurchaseOrders.Add(po1);

        // PO 2: FullyReceived - 50 ordered, 50 received
        var po2 = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = "PO-1002",
            SupplierId = supplier.Id,
            Status = PurchaseOrderStatus.FullyReceived,
            GrandTotal = 25000000m,
            IssuedAtUtc = DateTime.UtcNow.AddDays(-3),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
        };
        po2.Lines.Add(new PurchaseOrderLine
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = po2.Id,
            MaterialId = material.Id,
            OrderedQuantity = 50m,
            ReceivedQuantity = 50m,
            UnitPrice = 500000m,
            LineTotal = 25000000m
        });
        db.PurchaseOrders.Add(po2);

        // PO 3: Cancelled - should NOT count towards purchase volume or fulfillment
        var po3 = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = "PO-1003",
            SupplierId = supplier.Id,
            Status = PurchaseOrderStatus.Cancelled,
            GrandTotal = 30000000m,
            IssuedAtUtc = DateTime.UtcNow.AddDays(-2),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
        };
        po3.Lines.Add(new PurchaseOrderLine
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = po3.Id,
            MaterialId = material.Id,
            OrderedQuantity = 60m,
            ReceivedQuantity = 0m,
            UnitPrice = 500000m,
            LineTotal = 30000000m
        });
        db.PurchaseOrders.Add(po3);

        await db.SaveChangesAsync();

        var summary = await service.GetProcurementSummaryAsync(new ReportFilterParams());

        // Assertions
        summary.TotalOrdersCount.Should().Be(3);
        summary.CancelledOrdersCount.Should().Be(1);
        summary.TotalPurchaseVolume.Should().Be(75000000m); // PO1 (50M) + PO2 (25M), excluding PO3
        summary.AverageOrderValue.Should().Be(37500000m); // 75M / 2 valid orders

        // Fulfillment Rate: (80 + 50) / (100 + 50) = 130 / 150 = 86.67%
        summary.OrderFulfillmentRate.Should().Be(86.67m);
    }

    [Fact]
    public async Task Kpi_ZeroOrders_HandlesGracefully_WithoutDivideByZero()
    {
        using var db = CreateInMemoryContext();
        var service = new ReportingService(db, NullLogger<ReportingService>.Instance);

        var summary = await service.GetProcurementSummaryAsync(new ReportFilterParams());

        summary.TotalPurchaseVolume.Should().Be(0m);
        summary.TotalOrdersCount.Should().Be(0);
        summary.AverageOrderValue.Should().Be(0m);
        summary.OrderFulfillmentRate.Should().Be(0m);
        summary.OnTimeDeliveryRate.Should().Be(100m);
    }

    [Fact]
    public async Task Kpi_OnTimeDeliveryRate_CalculatesExpectedDatesAccurately()
    {
        using var db = CreateInMemoryContext();
        var service = new ReportingService(db, NullLogger<ReportingService>.Instance);

        var supplier = new Party { Id = Guid.NewGuid(), DisplayName = "شکر اهواز", IsActive = true };
        db.Parties.Add(supplier);

        // PO with expected date 5 days ago
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = "PO-2001",
            SupplierId = supplier.Id,
            Status = PurchaseOrderStatus.FullyReceived,
            GrandTotal = 10000000m,
            ExpectedDeliveryDateUtc = DateTime.UtcNow.AddDays(-5)
        };
        db.PurchaseOrders.Add(po);

        // Receipt 1: delivered 6 days ago (On-time!)
        var r1 = new GoodsReceipt
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = po.Id,
            ReceiptNumber = "GR-2001-1",
            ReceiptDateUtc = DateTime.UtcNow.AddDays(-6)
        };
        db.GoodsReceipts.Add(r1);

        // Receipt 2: delivered 4 days ago (Late!)
        var r2 = new GoodsReceipt
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = po.Id,
            ReceiptNumber = "GR-2001-2",
            ReceiptDateUtc = DateTime.UtcNow.AddDays(-4)
        };
        db.GoodsReceipts.Add(r2);

        await db.SaveChangesAsync();

        var summary = await service.GetProcurementSummaryAsync(new ReportFilterParams());

        // 1 on-time out of 2 receipts = 50.00%
        summary.OnTimeDeliveryRate.Should().Be(50.00m);
    }

    [Fact]
    public async Task Kpi_InventoryCoverageDays_CalculatesCorrectly_AndHandlesZeroConsumption()
    {
        using var db = CreateInMemoryContext();
        var service = new ReportingService(db, NullLogger<ReportingService>.Instance);

        var cat = new Category { Id = Guid.NewGuid(), Name = "مواد اولیه" };
        var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "کیلوگرم", Code = "KG" };
        db.Categories.Add(cat);
        db.UnitsOfMeasure.Add(unit);

        var mat1 = new Material { Id = Guid.NewGuid(), Name = "روغن نباتی", InternalCode = "MAT-OIL", CategoryId = cat.Id, BaseUnitId = unit.Id, SafetyStock = 10, MinStock = 30 };
        var mat2 = new Material { Id = Guid.NewGuid(), Name = "مخمر خمیر", InternalCode = "MAT-YEAST", CategoryId = cat.Id, BaseUnitId = unit.Id, SafetyStock = 5, MinStock = 15 };
        db.Materials.AddRange(mat1, mat2);

        // Mat1: 100 on hand, 10 daily consumption -> 10.0 coverage days
        db.InventorySnapshots.Add(new InventorySnapshot { Id = Guid.NewGuid(), MaterialId = mat1.Id, OnHandQuantity = 100m });
        db.MaterialConsumptionSummaries.Add(new MaterialConsumptionSummary
        {
            Id = Guid.NewGuid(),
            MaterialId = mat1.Id,
            AverageDailyConsumption = 10m,
            TotalConsumption = 300m,
            PeriodDays = 30
        });

        // Mat2: 50 on hand, 0 daily consumption -> 999.0 safe coverage days
        db.InventorySnapshots.Add(new InventorySnapshot { Id = Guid.NewGuid(), MaterialId = mat2.Id, OnHandQuantity = 50m });
        db.MaterialConsumptionSummaries.Add(new MaterialConsumptionSummary
        {
            Id = Guid.NewGuid(),
            MaterialId = mat2.Id,
            AverageDailyConsumption = 0m,
            TotalConsumption = 0m,
            PeriodDays = 30
        });

        await db.SaveChangesAsync();

        var consumption = await service.GetMaterialConsumptionAsync(new ReportFilterParams());

        var item1 = consumption.First(c => c.MaterialId == mat1.Id);
        item1.CoverageDays.Should().Be(10.0m);
        item1.InventoryRiskStatus.Should().Be("Safe");

        var item2 = consumption.First(c => c.MaterialId == mat2.Id);
        item2.CoverageDays.Should().Be(999.0m);
    }

    [Fact]
    public async Task Kpi_ThreeWayMatchSuccessRate_CalculatesExactAndToleranceMatches()
    {
        using var db = CreateInMemoryContext();
        var service = new ReportingService(db, NullLogger<ReportingService>.Instance);

        var supplier = new Party { Id = Guid.NewGuid(), DisplayName = "تأمین‌کننده آرد زرین", IsActive = true };
        db.Parties.Add(supplier);

        var po = new PurchaseOrder { Id = Guid.NewGuid(), OrderNumber = "PO-M1", SupplierId = supplier.Id };
        var inv = new SupplierInvoice { Id = Guid.NewGuid(), InvoiceNumber = "INV-M1", SupplierId = supplier.Id, TotalAmount = 10000000m };
        db.PurchaseOrders.Add(po);
        db.SupplierInvoices.Add(inv);

        // 4 matches: 2 ExactMatch, 1 WithinTolerance, 1 PriceMismatch
        db.ThreeWayMatchResults.Add(new ThreeWayMatchResult { Id = Guid.NewGuid(), PurchaseOrderId = po.Id, SupplierInvoiceId = inv.Id, MatchStatus = ThreeWayMatchStatus.ExactMatch, EvaluatedBy = "admin" });
        db.ThreeWayMatchResults.Add(new ThreeWayMatchResult { Id = Guid.NewGuid(), PurchaseOrderId = po.Id, SupplierInvoiceId = inv.Id, MatchStatus = ThreeWayMatchStatus.ExactMatch, EvaluatedBy = "admin" });
        db.ThreeWayMatchResults.Add(new ThreeWayMatchResult { Id = Guid.NewGuid(), PurchaseOrderId = po.Id, SupplierInvoiceId = inv.Id, MatchStatus = ThreeWayMatchStatus.WithinTolerance, EvaluatedBy = "admin" });
        db.ThreeWayMatchResults.Add(new ThreeWayMatchResult { Id = Guid.NewGuid(), PurchaseOrderId = po.Id, SupplierInvoiceId = inv.Id, MatchStatus = ThreeWayMatchStatus.PriceMismatch, TotalVarianceAmount = 500000m, EvaluatedBy = "admin" });

        await db.SaveChangesAsync();

        var report = await service.GetFinanceReconciliationReportAsync(new ReportFilterParams());

        // (2 + 1) / 4 = 75.00%
        report.MatchSuccessRate.Should().Be(75.00m);
        report.OpenExceptionsCount.Should().Be(1);
        report.PriceMismatchCount.Should().Be(1);
    }

    [Fact]
    public async Task Kpi_TelephonyMissedCallRate_CalculatesAccurately()
    {
        using var db = CreateInMemoryContext();
        var service = new ReportingService(db, NullLogger<ReportingService>.Instance);

        // 10 calls: 7 answered, 3 missed
        for (int i = 0; i < 7; i++)
        {
            db.CallRecords.Add(new CallRecord { Id = Guid.NewGuid(), UniqueId = $"100.{i}", Status = CallStatus.Answered, DurationSeconds = 120 });
        }
        for (int i = 7; i < 10; i++)
        {
            db.CallRecords.Add(new CallRecord { Id = Guid.NewGuid(), UniqueId = $"100.{i}", Status = CallStatus.Missed });
        }

        await db.SaveChangesAsync();

        var report = await service.GetTelephonyActivityReportAsync(new ReportFilterParams());

        report.TotalCalls.Should().Be(10);
        report.AnsweredCalls.Should().Be(7);
        report.MissedCalls.Should().Be(3);
        report.MissedCallRate.Should().Be(30.00m);
        report.AverageCallDurationSeconds.Should().Be(120m);
    }

    [Fact]
    public void ExportService_CsvFormulaInjection_IsSafelyNeutralized()
    {
        // OWASP ASVS CWE-1236 Injection triggers: =, +, -, @, \t, \r
        ExportService.SanitizeForCsv("=1+2").Should().StartWith("'=");
        ExportService.SanitizeForCsv("+447123456").Should().StartWith("'+");
        ExportService.SanitizeForCsv("-500").Should().StartWith("'-");
        ExportService.SanitizeForCsv("@SUM(A1:A10)").Should().StartWith("'@");
        ExportService.SanitizeForCsv("\tcmd.exe").Trim('\"').Should().StartWith("'\t");
        ExportService.SanitizeForCsv("\rcmd.exe").Trim('\"').Should().StartWith("'\r");

        // Normal Persian text without injection trigger
        var clean = ExportService.SanitizeForCsv("آرد گندم زرین");
        clean.Should().Be("آرد گندم زرین");

        // Text with quotes and commas
        var quoted = ExportService.SanitizeForCsv("کالای شماره \"یک\", بسته ۵۰ کیلویی");
        quoted.Should().Contain("\"\"");
    }

    [Fact]
    public async Task ExportService_ExportToCsv_ContainsUtf8Bom_ForExcelCompatibility()
    {
        using var db = CreateInMemoryContext();
        var exportService = new ExportService(db, NullLogger<ExportService>.Instance);

        var result = await exportService.ExportReportAsync(new ExportRequestDto
        {
            ReportType = "PurchaseOrders",
            Format = "csv"
        }, "admin");

        result.FileBytes.Length.Should().BeGreaterThan(3);
        // UTF-8 BOM bytes
        result.FileBytes[0].Should().Be(0xEF);
        result.FileBytes[1].Should().Be(0xBB);
        result.FileBytes[2].Should().Be(0xBF);
        result.ContentType.Should().Contain("text/csv");
    }

    [Fact]
    public async Task DataFreshness_DetectsStaleSync_WhenOlderThan24Hours()
    {
        using var db = CreateInMemoryContext();
        var service = new ReportingService(db, NullLogger<ReportingService>.Instance);

        // Stale sync job from 30 hours ago
        db.InventorySyncJobs.Add(new InventorySyncJob
        {
            Id = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow.AddHours(-30),
            Status = SyncJobStatus.Success
        });
        await db.SaveChangesAsync();

        var health = await service.GetIntegrationHealthAsync();

        health.InventorySync.IsStale.Should().BeTrue();
        health.FreshnessWarnings.Should().Contain(w => w.Contains("۲۴ ساعت"));
    }

    [Fact]
    public async Task RoleDashboard_GeneratesTailoredMetrics_ForEachRole()
    {
        using var db = CreateInMemoryContext();
        var service = new ReportingService(db, NullLogger<ReportingService>.Instance);

        var procurementDash = await service.GetRoleDashboardAsync("Procurement", new ReportFilterParams());
        procurementDash.Role.Should().Be("Procurement");
        procurementDash.SummaryMetrics.Should().ContainKey("totalPurchaseVolume");
        procurementDash.SummaryMetrics.Should().ContainKey("orderFulfillmentRate");

        var managerDash = await service.GetRoleDashboardAsync("Manager", new ReportFilterParams());
        managerDash.Role.Should().Be("Manager");
        managerDash.SummaryMetrics.Should().ContainKey("matchingSuccessRate");
        managerDash.SummaryMetrics.Should().ContainKey("totalInventoryValue");

        var warehouseDash = await service.GetRoleDashboardAsync("Warehouse", new ReportFilterParams());
        warehouseDash.Role.Should().Be("Warehouse");
        warehouseDash.SummaryMetrics.Should().ContainKey("totalSkus");
        warehouseDash.SummaryMetrics.Should().ContainKey("staleInventoryCount");

        var financeDash = await service.GetRoleDashboardAsync("Finance", new ReportFilterParams());
        financeDash.Role.Should().Be("Finance");
        financeDash.SummaryMetrics.Should().ContainKey("totalInvoicedAmount");
        financeDash.SummaryMetrics.Should().ContainKey("openExceptionsCount");

        var adminDash = await service.GetRoleDashboardAsync("Admin", new ReportFilterParams());
        adminDash.Role.Should().Be("Admin");
        adminDash.SummaryMetrics.Should().ContainKey("apiHealth");
        adminDash.SummaryMetrics.Should().ContainKey("databaseHealth");
    }
}
