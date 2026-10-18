using System;
using System.Collections.Generic;

namespace ParkPalace.Domain.Entities;

public enum PurchaseOrderStatus
{
    Draft = 0,
    Issued = 1,
    PartiallyReceived = 2,
    FullyReceived = 3,
    Cancelled = 4,
    Closed = 5
}

public class PurchaseOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderNumber { get; set; } = string.Empty; // e.g. PO-202610-0001
    public Guid SupplierId { get; set; }
    public Party? Supplier { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    public string Currency { get; set; } = "IRR";
    public decimal ExchangeRate { get; set; } = 1.0m;

    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal GrandTotal { get; set; }

    // Direct PO issuance details
    public Guid? IssuedByUserId { get; set; }
    public string? IssuedByUsername { get; set; }
    public DateTime? IssuedAtUtc { get; set; }

    public DateTime? ExpectedDeliveryDateUtc { get; set; }
    public string? PaymentTerms { get; set; }
    public string? DeliveryTerms { get; set; }
    public string? Notes { get; set; }

    // Concurrency control
    public string ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();

    // Cancellation details
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }

    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
    public ICollection<PurchaseOrderAudit> Audits { get; set; } = new List<PurchaseOrderAudit>();
    public ICollection<GoodsReceipt> Receipts { get; set; } = new List<GoodsReceipt>();
    public ICollection<SupplierInvoice> Invoices { get; set; } = new List<SupplierInvoice>();
}

public class PurchaseOrderLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public int LineNumber { get; set; }
    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }

    public decimal OrderedQuantity { get; set; }
    public Guid UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }

    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }

    public decimal ReceivedQuantity { get; set; }
    public decimal InvoicedQuantity { get; set; }

    // Immutable Snapshots frozen at issuance
    public string? MaterialCodeSnapshot { get; set; }
    public string? MaterialNameSnapshot { get; set; }
    public string? UnitCodeSnapshot { get; set; }
    public string? UnitNameSnapshot { get; set; }
    public string? SupplierNameSnapshot { get; set; }

    public string? Notes { get; set; }

    public ICollection<GoodsReceiptLine> ReceiptLines { get; set; } = new List<GoodsReceiptLine>();
    public ICollection<SupplierInvoiceLine> InvoiceLines { get; set; } = new List<SupplierInvoiceLine>();
}

public class PurchaseOrderAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public string Action { get; set; } = string.Empty; // Created, Issued, LineAdded, Received, Cancelled
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string? DetailsJson { get; set; }
}
