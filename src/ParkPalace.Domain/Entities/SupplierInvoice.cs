using System;
using System.Collections.Generic;

namespace ParkPalace.Domain.Entities;

public enum SupplierInvoiceStatus
{
    Draft = 0,
    PendingMatching = 1,
    Matched = 2,
    ExceptionPendingResolution = 3,
    ApprovedForPayment = 4,
    Disputed = 5,
    Cancelled = 6
}

public class SupplierInvoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InvoiceNumber { get; set; } = string.Empty; // فاکتور تأمین‌کننده

    public Guid SupplierId { get; set; }
    public Party? Supplier { get; set; }

    public Guid? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public DateTime InvoiceDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DueDateUtc { get; set; }
    public DateTime ReceivedDateUtc { get; set; } = DateTime.UtcNow;

    public string Currency { get; set; } = "IRR";
    public decimal ExchangeRate { get; set; } = 1.0m;

    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TotalAmount { get; set; }

    public SupplierInvoiceStatus Status { get; set; } = SupplierInvoiceStatus.PendingMatching;

    // Official Sepidar Integration tracking
    public bool RegisteredInSepidar { get; set; }
    public string? SepidarInvoiceRef { get; set; }
    public string? SepidarVoucherNumber { get; set; }

    // Attachment
    public string? AttachmentFileName { get; set; }
    public string? AttachmentStoragePath { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    public ICollection<SupplierInvoiceLine> Lines { get; set; } = new List<SupplierInvoiceLine>();
}

public class SupplierInvoiceLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SupplierInvoiceId { get; set; }
    public SupplierInvoice? SupplierInvoice { get; set; }

    public Guid? PurchaseOrderLineId { get; set; }
    public PurchaseOrderLine? PurchaseOrderLine { get; set; }

    public Guid? GoodsReceiptLineId { get; set; }
    public GoodsReceiptLine? GoodsReceiptLine { get; set; }

    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }

    public decimal InvoicedQuantity { get; set; }
    public Guid UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }

    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }

    public string? Description { get; set; }
}
