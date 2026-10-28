using System;
using System.Collections.Generic;

namespace ParkPalace.Application.DTOs;

public enum DataFreshnessStatus
{
    LiveVerified = 1,
    CachedSync = 2,
    Stale = 3,
    Simulated = 4,
    Unavailable = 5
}

public class ReportFilterParams
{
    public DateTime? StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? Status { get; set; }
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class ProcurementSummaryReportDto
{
    public decimal TotalPurchaseVolume { get; set; }
    public int TotalOrdersCount { get; set; }
    public int ActiveOrdersCount { get; set; }
    public int CompletedOrdersCount { get; set; }
    public int CancelledOrdersCount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal OrderFulfillmentRate { get; set; } // percentage e.g. 94.5%
    public decimal OnTimeDeliveryRate { get; set; } // percentage e.g. 88.0%
    public int CriticalStockCount { get; set; }
    public int LowStockCount { get; set; }
    public int ActiveRecommendationsCount { get; set; }
    public decimal RecommendationsEstimatedValue { get; set; }
    public DataFreshnessStatus DataFreshness { get; set; } = DataFreshnessStatus.LiveVerified;
    public string? FreshnessWarning { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}

public class PurchaseOrderTrendItemDto
{
    public string Period { get; set; } = string.Empty; // e.g. "2026-10" or "1405-07"
    public DateTime DateUtc { get; set; }
    public string JalaliDate { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public int FulfilledCount { get; set; }
}

public class SupplierPerformanceReportItemDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int TotalOrdersCount { get; set; }
    public decimal TotalSpend { get; set; }
    public decimal AverageDeliveryLeadTimeDays { get; set; }
    public decimal OnTimeDeliveryRate { get; set; } // percentage
    public decimal FulfillmentRate { get; set; } // percentage
    public decimal PartialReceiptRate { get; set; } // percentage
    public decimal QualityAcceptanceRate { get; set; } // percentage
    public int TotalCallsCount { get; set; }
    public DateTime? LastContactDateUtc { get; set; }
}

public class MaterialConsumptionReportItemDto
{
    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public decimal TotalConsumedQuantity { get; set; }
    public decimal AverageDailyConsumption { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal CoverageDays { get; set; }
    public string InventoryRiskStatus { get; set; } = "Safe"; // Safe, Warning, Critical, Overstocked
    public DateTime? LastMovementDateUtc { get; set; }
}

public class InventoryHealthItemDto
{
    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public decimal CurrentOnHand { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal ReorderPoint { get; set; }
    public decimal LastUnitPrice { get; set; }
    public decimal TotalStockValue { get; set; }
    public decimal CoverageDays { get; set; }
    public string StockStatus { get; set; } = "Healthy"; // Critical, Low, Healthy, Overstocked
    public bool IsStale { get; set; } // no movement in 90+ days
    public DateTime? LastMovementDateUtc { get; set; }
}

public class InventoryHealthReportDto
{
    public int TotalSkus { get; set; }
    public decimal TotalInventoryValue { get; set; }
    public int CriticalStockCount { get; set; }
    public int LowStockCount { get; set; }
    public int HealthyStockCount { get; set; }
    public int OverstockedCount { get; set; }
    public int StaleInventoryCount { get; set; }
    public DateTime? LastSyncDateUtc { get; set; }
    public string SyncStatus { get; set; } = "Success";
    public DataFreshnessStatus DataFreshness { get; set; } = DataFreshnessStatus.LiveVerified;
    public string? FreshnessWarning { get; set; }
    public List<InventoryHealthItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

public class FinanceExceptionItemDto
{
    public Guid MatchResultId { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid SupplierInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public DateTime InvoiceDateUtc { get; set; }
    public decimal InvoicedAmount { get; set; }
    public decimal ReceivedValue { get; set; }
    public decimal VarianceAmount { get; set; }
    public decimal VariancePercent { get; set; }
    public string MatchStatus { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
    public string? ResolutionAction { get; set; }
    public string? ResolutionNotes { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public bool HasSepidarLink { get; set; }
    public string? SepidarVoucherNumber { get; set; }
}

public class FinanceReconciliationReportDto
{
    public decimal TotalInvoicedAmount { get; set; }
    public decimal TotalMatchedAmount { get; set; }
    public decimal TotalVarianceAmount { get; set; }
    public decimal MatchSuccessRate { get; set; } // percentage
    public int OpenExceptionsCount { get; set; }
    public int PriceMismatchCount { get; set; }
    public int QuantityMismatchCount { get; set; }
    public int MissingReceiptCount { get; set; }
    public int SepidarBacklogCount { get; set; }
    public int SepidarLinkedCount { get; set; }
    public List<FinanceExceptionItemDto> Exceptions { get; set; } = new();
    public int TotalExceptionsCount { get; set; }
}

public class TelephonyActivityReportDto
{
    public int TotalCalls { get; set; }
    public int IncomingCalls { get; set; }
    public int OutgoingCalls { get; set; }
    public int AnsweredCalls { get; set; }
    public int MissedCalls { get; set; }
    public decimal MissedCallRate { get; set; } // percentage
    public decimal AverageCallDurationSeconds { get; set; }
    public int SupplierIdentifiedCalls { get; set; }
    public int UnknownCallerCalls { get; set; }
    public int PendingFollowUpsCount { get; set; }
    public int OverdueFollowUpsCount { get; set; }
    public string PbxConnectionStatus { get; set; } = "Simulated";
    public DataFreshnessStatus DataFreshness { get; set; } = DataFreshnessStatus.Simulated;
    public string? FreshnessWarning { get; set; }
}

public class SepidarAdapterHealthDto
{
    public string AdapterName { get; set; } = "SepidarFixtureFinancialAdapter";
    public string AdapterType { get; set; } = "Fixture / Simulated";
    public bool IsLiveConnected { get; set; } = false;
    public string Status { get; set; } = "Active";
    public DateTime? LastSyncUtc { get; set; }
    public string FreshnessNote { get; set; } = "Fixture data utilized in development environment; live ERP connection inactive.";
}

public class IssabelAdapterHealthDto
{
    public string AdapterName { get; set; } = "SimulatedTelephonyAdapter";
    public string AdapterType { get; set; } = "Simulated";
    public bool IsLiveConnected { get; set; } = false;
    public string Status { get; set; } = "Connected";
    public int EventsProcessed { get; set; }
    public int ReconnectionRetries { get; set; }
    public string FreshnessNote { get; set; } = "Simulated telephony adapter active for deterministic testing.";
}

public class InventorySyncHealthDto
{
    public DateTime? LastRunUtc { get; set; }
    public string LastRunStatus { get; set; } = "Success";
    public int ItemsProcessed { get; set; }
    public int ErrorsCount { get; set; }
    public bool IsStale { get; set; }
    public TimeSpan ElapsedSinceLastSync { get; set; }
}

public class IntegrationHealthReportDto
{
    public string ApiHealth { get; set; } = "Healthy";
    public string DatabaseHealth { get; set; } = "Healthy";
    public long DatabaseLatencyMs { get; set; }
    public SepidarAdapterHealthDto SepidarAdapter { get; set; } = new();
    public IssabelAdapterHealthDto IssabelAdapter { get; set; } = new();
    public InventorySyncHealthDto InventorySync { get; set; } = new();
    public List<string> FreshnessWarnings { get; set; } = new();
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DashboardActionItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Severity { get; set; } = "Normal"; // Normal, Warning, Critical
    public string TargetView { get; set; } = string.Empty; // orders, warehouse, finance, calls, materials
    public DateTime TimestampUtc { get; set; }
    public string JalaliDate { get; set; } = string.Empty;
}

public class RoleDashboardDto
{
    public string Role { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public DataFreshnessStatus DataFreshness { get; set; } = DataFreshnessStatus.LiveVerified;
    public string FreshnessBadge { get; set; } = "داده‌های زنده CRM";
    public Dictionary<string, object> SummaryMetrics { get; set; } = new();
    public List<PurchaseOrderTrendItemDto> TrendItems { get; set; } = new();
    public List<SupplierPerformanceReportItemDto> TopSuppliers { get; set; } = new();
    public List<MaterialConsumptionReportItemDto> CriticalMaterials { get; set; } = new();
    public List<DashboardActionItemDto> ActionItems { get; set; } = new();
}

public class ExportRequestDto
{
    public string ReportType { get; set; } = "PurchaseOrders"; // PurchaseOrders, SupplierPerformance, InventoryHealth, FinanceExceptions, TelephonyActivity
    public string Format { get; set; } = "csv"; // csv, xlsx
    public DateTime? StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? Status { get; set; }
    public string? SearchTerm { get; set; }
}

public class ExportResultDto
{
    public byte[] FileBytes { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "text/csv; charset=utf-8";
    public string FileName { get; set; } = string.Empty;
    public int RowCount { get; set; }
}
