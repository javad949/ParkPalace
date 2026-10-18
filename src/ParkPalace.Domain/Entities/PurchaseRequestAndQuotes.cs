using System;
using System.Collections.Generic;

namespace ParkPalace.Domain.Entities;

public enum PurchaseRequestStatus
{
    Draft = 0,
    Submitted = 1,
    InReview = 2,
    ConvertedToOrder = 3,
    Cancelled = 4
}

public class PurchaseRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RequestNumber { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public DateTime? NeededByDateUtc { get; set; }
    public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.Draft;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    public ICollection<PurchaseRequestLine> Lines { get; set; } = new List<PurchaseRequestLine>();
}

public class PurchaseRequestLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseRequestId { get; set; }
    public PurchaseRequest? PurchaseRequest { get; set; }

    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }
    public decimal Quantity { get; set; }
    public Guid UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }
    public decimal? EstimatedUnitPrice { get; set; }
    public string? Notes { get; set; }
}

public enum RfqStatus
{
    Open = 0,
    Closed = 1,
    Evaluated = 2,
    Cancelled = 3
}

public class RequestForQuotation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RfqNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime? DeadlineUtc { get; set; }
    public RfqStatus Status { get; set; } = RfqStatus.Open;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    public ICollection<RfqSupplier> Suppliers { get; set; } = new List<RfqSupplier>();
    public ICollection<SupplierQuotation> Quotations { get; set; } = new List<SupplierQuotation>();
}

public class RfqSupplier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestForQuotationId { get; set; }
    public RequestForQuotation? RequestForQuotation { get; set; }

    public Guid SupplierId { get; set; }
    public Party? Supplier { get; set; }
    public DateTime InvitedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum SupplierQuotationStatus
{
    Draft = 0,
    Submitted = 1,
    Selected = 2,
    Rejected = 3
}

public class SupplierQuotation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string QuotationNumber { get; set; } = string.Empty;
    public Guid? RequestForQuotationId { get; set; }
    public RequestForQuotation? RequestForQuotation { get; set; }

    public Guid SupplierId { get; set; }
    public Party? Supplier { get; set; }

    public DateTime QuotationDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ValidUntilUtc { get; set; }
    public string Currency { get; set; } = "IRR";
    public decimal ExchangeRate { get; set; } = 1.0m;
    public string? PaymentTerms { get; set; }
    public int LeadTimeDays { get; set; }
    public decimal TotalAmount { get; set; }

    public SupplierQuotationStatus Status { get; set; } = SupplierQuotationStatus.Draft;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<SupplierQuotationLine> Lines { get; set; } = new List<SupplierQuotationLine>();
}

public class SupplierQuotationLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SupplierQuotationId { get; set; }
    public SupplierQuotation? SupplierQuotation { get; set; }

    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }
    public decimal OfferedQuantity { get; set; }
    public Guid UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }

    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public int? LeadTimeDays { get; set; }
    public string? Notes { get; set; }
}
