using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders", "crm");
        builder.HasKey(po => po.Id);

        builder.Property(po => po.OrderNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(po => po.OrderNumber).IsUnique();

        builder.Property(po => po.Currency).HasMaxLength(10).IsRequired();
        builder.Property(po => po.ExchangeRate).HasPrecision(18, 4);

        builder.Property(po => po.SubTotal).HasPrecision(18, 2);
        builder.Property(po => po.TaxTotal).HasPrecision(18, 2);
        builder.Property(po => po.DiscountTotal).HasPrecision(18, 2);
        builder.Property(po => po.GrandTotal).HasPrecision(18, 2);

        builder.Property(po => po.IssuedByUsername).HasMaxLength(100);
        builder.Property(po => po.PaymentTerms).HasMaxLength(200);
        builder.Property(po => po.DeliveryTerms).HasMaxLength(200);
        builder.Property(po => po.Notes).HasMaxLength(1000);

        builder.Property(po => po.ConcurrencyToken).HasMaxLength(100).IsConcurrencyToken();

        builder.Property(po => po.CancelledBy).HasMaxLength(100);
        builder.Property(po => po.CancellationReason).HasMaxLength(500);
        builder.Property(po => po.CreatedBy).HasMaxLength(100);
        builder.Property(po => po.UpdatedBy).HasMaxLength(100);

        builder.HasOne(po => po.Supplier)
            .WithMany()
            .HasForeignKey(po => po.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(po => po.Warehouse)
            .WithMany()
            .HasForeignKey(po => po.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(po => po.Lines)
            .WithOne(l => l.PurchaseOrder)
            .HasForeignKey(l => l.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(po => po.Audits)
            .WithOne(a => a.PurchaseOrder)
            .HasForeignKey(a => a.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("PurchaseOrderLines", "crm");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.OrderedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.ReceivedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.InvoicedQuantity).HasPrecision(18, 4);

        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.TaxRate).HasPrecision(18, 4);
        builder.Property(l => l.TaxAmount).HasPrecision(18, 2);
        builder.Property(l => l.DiscountAmount).HasPrecision(18, 2);
        builder.Property(l => l.LineTotal).HasPrecision(18, 2);

        builder.Property(l => l.MaterialCodeSnapshot).HasMaxLength(50);
        builder.Property(l => l.MaterialNameSnapshot).HasMaxLength(200);
        builder.Property(l => l.UnitCodeSnapshot).HasMaxLength(20);
        builder.Property(l => l.UnitNameSnapshot).HasMaxLength(50);
        builder.Property(l => l.SupplierNameSnapshot).HasMaxLength(200);
        builder.Property(l => l.Notes).HasMaxLength(500);

        builder.HasOne(l => l.Material)
            .WithMany()
            .HasForeignKey(l => l.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Unit)
            .WithMany()
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseOrderAuditConfiguration : IEntityTypeConfiguration<PurchaseOrderAudit>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderAudit> builder)
    {
        builder.ToTable("PurchaseOrderAudits", "crm");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.PerformedBy).HasMaxLength(100).IsRequired();
        builder.Property(a => a.DetailsJson).HasMaxLength(4000);
    }
}
