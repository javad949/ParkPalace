using System;
using System.Collections.Generic;

namespace ParkPalace.Domain.Entities;

public enum GoodsReceiptStatus
{
    Draft = 0,
    Accepted = 1,
    PartiallyAccepted = 2,
    Rejected = 3,
    Cancelled = 4
}

public enum QcStatus
{
    Pending = 0,
    Passed = 1,
    Conditional = 2,
    Rejected = 3
}

public class GoodsReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ReceiptNumber { get; set; } = string.Empty; // e.g. GRN-202610-0001

    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public Guid? ReceivedByUserId { get; set; }
    public string ReceivedByUsername { get; set; } = string.Empty;
    public DateTime ReceiptDateUtc { get; set; } = DateTime.UtcNow;

    public string? CarrierName { get; set; }
    public string? DeliveryNoteNumber { get; set; } // شماره بارنامه یا حواله راننده
    public string? VehiclePlateNumber { get; set; }

    public GoodsReceiptStatus Status { get; set; } = GoodsReceiptStatus.Accepted;

    // Correlation with official Sepidar inventory movement to prevent double counting
    public string? SepidarMovementRef { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<GoodsReceiptLine> Lines { get; set; } = new List<GoodsReceiptLine>();
}

public class GoodsReceiptLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GoodsReceiptId { get; set; }
    public GoodsReceipt? GoodsReceipt { get; set; }

    public Guid PurchaseOrderLineId { get; set; }
    public PurchaseOrderLine? PurchaseOrderLine { get; set; }

    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }

    public decimal DeliveredQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }

    public Guid UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }

    public QcStatus QcStatus { get; set; } = QcStatus.Passed;
    public string? RejectionReason { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDateUtc { get; set; }
    public string? StorageLocation { get; set; }

    public string? Notes { get; set; }
}
