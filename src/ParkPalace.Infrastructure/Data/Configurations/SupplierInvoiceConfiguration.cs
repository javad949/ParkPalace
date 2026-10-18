using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class SupplierInvoiceConfiguration : IEntityTypeConfiguration<SupplierInvoice>
{
    public void Configure(EntityTypeBuilder<SupplierInvoice> builder)
    {
        builder.ToTable("SupplierInvoices", "crm");
        builder.HasKey(si => si.Id);

        builder.Property(si => si.InvoiceNumber).HasMaxLength(100).IsRequired();
        builder.HasIndex(si => new { si.SupplierId, si.InvoiceNumber }).IsUnique();

        builder.Property(si => si.Currency).HasMaxLength(10).IsRequired();
        builder.Property(si => si.ExchangeRate).HasPrecision(18, 4);

        builder.Property(si => si.SubTotal).HasPrecision(18, 2);
        builder.Property(si => si.TaxTotal).HasPrecision(18, 2);
        builder.Property(si => si.DiscountTotal).HasPrecision(18, 2);
        builder.Property(si => si.TotalAmount).HasPrecision(18, 2);

        builder.Property(si => si.SepidarInvoiceRef).HasMaxLength(100);
        builder.Property(si => si.SepidarVoucherNumber).HasMaxLength(100);
        builder.Property(si => si.AttachmentFileName).HasMaxLength(255);
        builder.Property(si => si.AttachmentStoragePath).HasMaxLength(500);
        builder.Property(si => si.Notes).HasMaxLength(1000);
        builder.Property(si => si.CreatedBy).HasMaxLength(100);

        builder.HasOne(si => si.Supplier)
            .WithMany()
            .HasForeignKey(si => si.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(si => si.PurchaseOrder)
            .WithMany(po => po.Invoices)
            .HasForeignKey(si => si.PurchaseOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(si => si.Lines)
            .WithOne(l => l.SupplierInvoice)
            .HasForeignKey(l => l.SupplierInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SupplierInvoiceLineConfiguration : IEntityTypeConfiguration<SupplierInvoiceLine>
{
    public void Configure(EntityTypeBuilder<SupplierInvoiceLine> builder)
    {
        builder.ToTable("SupplierInvoiceLines", "crm");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.InvoicedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.TaxAmount).HasPrecision(18, 2);
        builder.Property(l => l.DiscountAmount).HasPrecision(18, 2);
        builder.Property(l => l.LineTotal).HasPrecision(18, 2);
        builder.Property(l => l.Description).HasMaxLength(300);

        builder.HasOne(l => l.PurchaseOrderLine)
            .WithMany(pol => pol.InvoiceLines)
            .HasForeignKey(l => l.PurchaseOrderLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.GoodsReceiptLine)
            .WithMany()
            .HasForeignKey(l => l.GoodsReceiptLineId)
            .OnDelete(DeleteBehavior.Restrict);

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
