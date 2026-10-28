using System;
using System.Collections.Generic;

namespace ParkPalace.Application.DTOs;

// ================= Purchase Order DTOs =================
public class PurchaseOrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public string Currency { get; set; } = "IRR";
    public decimal ExchangeRate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal GrandTotal { get; set; }

    public string? IssuedByUsername { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
    public DateTime? ExpectedDeliveryDateUtc { get; set; }
    public string? PaymentTerms { get; set; }
    public string? DeliveryTerms { get; set; }
    public string? Notes { get; set; }
    public string ConcurrencyToken { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public decimal TotalOrderedQuantity { get; set; }
    public decimal TotalReceivedQuantity { get; set; }
    public decimal TotalInvoicedQuantity { get; set; }

    public List<PurchaseOrderLineDto> Lines { get; set; } = new();
    public List<PurchaseOrderAuditDto> Audits { get; set; } = new();
}

public class PurchaseOrderLineDto
{
    public Guid Id { get; set; }
    public int LineNumber { get; set; }
    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public Guid UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string UnitCode { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }

    public decimal ReceivedQuantity { get; set; }
    public decimal InvoicedQuantity { get; set; }

    // Immutable Snapshots
    public string? MaterialCodeSnapshot { get; set; }
    public string? MaterialNameSnapshot { get; set; }
    public string? UnitCodeSnapshot { get; set; }
    public string? UnitNameSnapshot { get; set; }
    public string? SupplierNameSnapshot { get; set; }

    public string? Notes { get; set; }
}

public class PurchaseOrderAuditDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string? DetailsJson { get; set; }
}

public class CreatePurchaseOrderRequest
{
    public Guid SupplierId { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateTime? ExpectedDeliveryDateUtc { get; set; }
    public string? PaymentTerms { get; set; }
    public string? DeliveryTerms { get; set; }
    public string? Notes { get; set; }
    public bool DirectIssue { get; set; } = true; // Direct issuance by Procurement
    public bool ManualReviewConfirmed { get; set; } = false; // Explicit confirmation when reserve is unverified
    public List<CreatePurchaseOrderLineRequest> Lines { get; set; } = new();
}

public class CreatePurchaseOrderLineRequest
{
    public Guid MaterialId { get; set; }
    public decimal OrderedQuantity { get; set; }
    public Guid? UnitId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; } = 0m;
    public decimal DiscountAmount { get; set; } = 0m;
    public string? Notes { get; set; }
}

public class CancelPurchaseOrderRequest
{
    public string Reason { get; set; } = string.Empty;
    public string ConcurrencyToken { get; set; } = string.Empty;
}

// ================= Goods Receipt DTOs =================
public class GoodsReceiptDto
{
    public Guid Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public Guid PurchaseOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string ReceivedByUsername { get; set; } = string.Empty;
    public DateTime ReceiptDateUtc { get; set; }
    public string? CarrierName { get; set; }
    public string? DeliveryNoteNumber { get; set; }
    public string? VehiclePlateNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? SepidarMovementRef { get; set; }
    public string? Notes { get; set; }

    public List<GoodsReceiptLineDto> Lines { get; set; } = new();
}

public class GoodsReceiptLineDto
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderLineId { get; set; }
    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public decimal DeliveredQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string UnitCode { get; set; } = string.Empty;
    public string QcStatus { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDateUtc { get; set; }
    public string? StorageLocation { get; set; }
    public string? Notes { get; set; }
}

public class CreateGoodsReceiptRequest
{
    public Guid PurchaseOrderId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? CarrierName { get; set; }
    public string? DeliveryNoteNumber { get; set; }
    public string? VehiclePlateNumber { get; set; }
    public string? SepidarMovementRef { get; set; }
    public string? Notes { get; set; }
    public List<CreateGoodsReceiptLineRequest> Lines { get; set; } = new();
}

public class CreateGoodsReceiptLineRequest
{
    public Guid PurchaseOrderLineId { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; } = 0m;
    public string QcStatus { get; set; } = "Passed"; // Passed, Conditional, Rejected
    public string? RejectionReason { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDateUtc { get; set; }
    public string? StorageLocation { get; set; }
    public string? Notes { get; set; }
}

// ================= Supplier Invoice DTOs =================
public class SupplierInvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid? PurchaseOrderId { get; set; }
    public string? OrderNumber { get; set; }
    public DateTime InvoiceDateUtc { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public DateTime ReceivedDateUtc { get; set; }
    public string Currency { get; set; } = "IRR";
    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool RegisteredInSepidar { get; set; }
    public string? SepidarInvoiceRef { get; set; }
    public string? SepidarVoucherNumber { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? Notes { get; set; }

    public List<SupplierInvoiceLineDto> Lines { get; set; } = new();
}

public class SupplierInvoiceLineDto
{
    public Guid Id { get; set; }
    public Guid? PurchaseOrderLineId { get; set; }
    public Guid? GoodsReceiptLineId { get; set; }
    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public decimal InvoicedQuantity { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string UnitCode { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public string? Description { get; set; }
}

public class CreateSupplierInvoiceRequest
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public DateTime InvoiceDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DueDateUtc { get; set; }
    public string Currency { get; set; } = "IRR";
    public decimal TaxTotal { get; set; } = 0m;
    public decimal DiscountTotal { get; set; } = 0m;
    public string? Notes { get; set; }
    public List<CreateSupplierInvoiceLineRequest> Lines { get; set; } = new();
}

public class CreateSupplierInvoiceLineRequest
{
    public Guid? PurchaseOrderLineId { get; set; }
    public Guid? GoodsReceiptLineId { get; set; }
    public Guid MaterialId { get; set; }
    public decimal InvoicedQuantity { get; set; }
    public Guid? UnitId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; } = 0m;
    public decimal DiscountAmount { get; set; } = 0m;
    public string? Description { get; set; }
}

// ================= Three-Way Matching DTOs =================
public class ThreeWayMatchResultDto
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid? GoodsReceiptId { get; set; }
    public string? ReceiptNumber { get; set; }
    public Guid SupplierInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;

    public DateTime EvaluatedAtUtc { get; set; }
    public string EvaluatedBy { get; set; } = string.Empty;
    public string MatchStatus { get; set; } = string.Empty;
    public decimal TotalVarianceAmount { get; set; }
    public decimal TolerancePercentageApplied { get; set; }

    public bool IsResolved { get; set; }
    public string? ResolutionAction { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNotes { get; set; }

    public List<ThreeWayMatchLineVarianceDto> LineVariances { get; set; } = new();
}

public class ThreeWayMatchLineVarianceDto
{
    public Guid Id { get; set; }
    public Guid? PurchaseOrderLineId { get; set; }
    public Guid? GoodsReceiptLineId { get; set; }
    public Guid? SupplierInvoiceLineId { get; set; }
    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;

    public decimal PoQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal InvoicedQuantity { get; set; }
    public decimal QuantityVariance { get; set; }

    public decimal PoUnitPrice { get; set; }
    public decimal InvoicedUnitPrice { get; set; }
    public decimal PriceVariance { get; set; }
    public decimal PriceVariancePercent { get; set; }

    public decimal PoLineTotal { get; set; }
    public decimal InvoicedLineTotal { get; set; }
    public decimal TotalVariance { get; set; }

    public string MismatchType { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ResolveMatchingRequest
{
    public string ResolutionAction { get; set; } = string.Empty; // ApprovedWithVariance, CreditNoteRequested, InvoiceRejected, Corrected
    public string ResolutionNotes { get; set; } = string.Empty;
}

// ================= Sepidar Financial Reconciliation DTOs =================
public class SepidarCandidateInvoiceDto
{
    public string SepidarInvoiceId { get; set; } = string.Empty;
    public string SepidarVoucherNumber { get; set; } = string.Empty;
    public DateTime VoucherDateUtc { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierNationalCode { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "IRR";
    public decimal ConfidenceScore { get; set; } // 0.0 to 1.0
    public string MatchConfidenceLevel { get; set; } = "Medium"; // High, Medium, Low
    public string MatchReason { get; set; } = string.Empty;
}

public class SepidarFinancialReconciliationDto
{
    public Guid Id { get; set; }
    public Guid SupplierInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string SepidarInvoiceId { get; set; } = string.Empty;
    public string SepidarVoucherNumber { get; set; } = string.Empty;
    public DateTime SepidarVoucherDateUtc { get; set; }
    public decimal SepidarAmount { get; set; }
    public decimal CandidateConfidenceScore { get; set; }
    public string LinkStatus { get; set; } = string.Empty;
    public string? LinkedBy { get; set; }
    public DateTime? LinkedAtUtc { get; set; }
    public string? AuditNotes { get; set; }
}

public class LinkSepidarInvoiceRequest
{
    public Guid SupplierInvoiceId { get; set; }
    public string SepidarInvoiceId { get; set; } = string.Empty;
    public string SepidarVoucherNumber { get; set; } = string.Empty;
    public DateTime SepidarVoucherDateUtc { get; set; }
    public decimal SepidarAmount { get; set; }
    public string? AuditNotes { get; set; }
}

public class UnlinkPayload
{
    public string? AuditNotes { get; set; }
}
