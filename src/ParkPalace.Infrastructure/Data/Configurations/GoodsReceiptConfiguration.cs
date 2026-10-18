using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> builder)
    {
        builder.ToTable("GoodsReceipts", "crm");
        builder.HasKey(gr => gr.Id);

        builder.Property(gr => gr.ReceiptNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(gr => gr.ReceiptNumber).IsUnique();

        builder.Property(gr => gr.ReceivedByUsername).HasMaxLength(100).IsRequired();
        builder.Property(gr => gr.CarrierName).HasMaxLength(150);
        builder.Property(gr => gr.DeliveryNoteNumber).HasMaxLength(100);
        builder.Property(gr => gr.VehiclePlateNumber).HasMaxLength(50);
        builder.Property(gr => gr.SepidarMovementRef).HasMaxLength(100);
        builder.Property(gr => gr.Notes).HasMaxLength(1000);

        builder.HasOne(gr => gr.PurchaseOrder)
            .WithMany(po => po.Receipts)
            .HasForeignKey(gr => gr.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(gr => gr.Warehouse)
            .WithMany()
            .HasForeignKey(gr => gr.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(gr => gr.Lines)
            .WithOne(l => l.GoodsReceipt)
            .HasForeignKey(l => l.GoodsReceiptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class GoodsReceiptLineConfiguration : IEntityTypeConfiguration<GoodsReceiptLine>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptLine> builder)
    {
        builder.ToTable("GoodsReceiptLines", "crm");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.DeliveredQuantity).HasPrecision(18, 4);
        builder.Property(l => l.AcceptedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.RejectedQuantity).HasPrecision(18, 4);

        builder.Property(l => l.RejectionReason).HasMaxLength(300);
        builder.Property(l => l.BatchNumber).HasMaxLength(100);
        builder.Property(l => l.StorageLocation).HasMaxLength(100);
        builder.Property(l => l.Notes).HasMaxLength(500);

        builder.HasOne(l => l.PurchaseOrderLine)
            .WithMany(pol => pol.ReceiptLines)
            .HasForeignKey(l => l.PurchaseOrderLineId)
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
