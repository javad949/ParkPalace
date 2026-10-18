using System;
using System.Collections.Generic;

namespace ParkPalace.Domain.Entities;

public enum ThreeWayMatchStatus
{
    ExactMatch = 0,
    WithinTolerance = 1,
    QuantityMismatch = 2,
    PriceMismatch = 3,
    TaxMismatch = 4,
    MissingReceipt = 5,
    OverReceived = 6,
    ManualReviewRequired = 7,
    ApprovedWithVariance = 8,
    Disputed = 9
}

public class ThreeWayMatchResult
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public Guid? GoodsReceiptId { get; set; }
    public GoodsReceipt? GoodsReceipt { get; set; }

    public Guid SupplierInvoiceId { get; set; }
    public SupplierInvoice? SupplierInvoice { get; set; }

    public DateTime EvaluatedAtUtc { get; set; } = DateTime.UtcNow;
    public string EvaluatedBy { get; set; } = string.Empty;

    public ThreeWayMatchStatus MatchStatus { get; set; } = ThreeWayMatchStatus.ExactMatch;
    public decimal TotalVarianceAmount { get; set; }
    public decimal TolerancePercentageApplied { get; set; } = 1.0m; // e.g. 1% default

    // Resolution workflow
    public bool IsResolved { get; set; }
    public string? ResolutionAction { get; set; } // ApprovedWithVariance, CreditNoteRequested, InvoiceRejected, Corrected
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNotes { get; set; }

    public ICollection<ThreeWayMatchLineVariance> LineVariances { get; set; } = new List<ThreeWayMatchLineVariance>();
}

public class ThreeWayMatchLineVariance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ThreeWayMatchResultId { get; set; }
    public ThreeWayMatchResult? ThreeWayMatchResult { get; set; }

    public Guid? PurchaseOrderLineId { get; set; }
    public Guid? GoodsReceiptLineId { get; set; }
    public Guid? SupplierInvoiceLineId { get; set; }
    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }

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

    public string MismatchType { get; set; } = "Exact"; // Exact, QuantityShortage, QuantityOverage, PriceIncrease, PriceDecrease
    public string? Notes { get; set; }
}

public enum ReconciliationLinkStatus
{
    CandidatePendingReview = 0,
    Confirmed = 1,
    Unlinked = 2
}

public class SepidarFinancialReconciliation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SupplierInvoiceId { get; set; }
    public SupplierInvoice? SupplierInvoice { get; set; }

    public string SepidarInvoiceId { get; set; } = string.Empty;
    public string SepidarVoucherNumber { get; set; } = string.Empty;
    public DateTime SepidarVoucherDateUtc { get; set; }
    public decimal SepidarAmount { get; set; }

    public decimal CandidateConfidenceScore { get; set; } // 0.0 to 1.0
    public ReconciliationLinkStatus LinkStatus { get; set; } = ReconciliationLinkStatus.CandidatePendingReview;

    public string? LinkedBy { get; set; }
    public DateTime? LinkedAtUtc { get; set; }
    public string? AuditNotes { get; set; }
}
