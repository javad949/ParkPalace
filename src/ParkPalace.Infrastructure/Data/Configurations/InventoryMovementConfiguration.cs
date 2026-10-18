using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("InventoryMovements");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.VoucherNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.Quantity)
            .HasPrecision(18, 4);

        builder.Property(m => m.ReferenceDocument)
            .HasMaxLength(150);

        builder.Property(m => m.SourceMovementKey)
            .HasMaxLength(150);

        builder.HasIndex(m => m.SourceMovementKey)
            .IsUnique()
            .HasFilter("[SourceMovementKey] IS NOT NULL");

        builder.HasIndex(m => new { m.MaterialId, m.TransactionDateUtc });
        builder.HasIndex(m => new { m.WarehouseId, m.MovementType, m.PostingStatus });

        builder.Property(m => m.RowVersion)
            .IsRowVersion();

        builder.HasOne(m => m.Warehouse)
            .WithMany(w => w.Movements)
            .HasForeignKey(m => m.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Material)
            .WithMany()
            .HasForeignKey(m => m.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Unit)
            .WithMany()
            .HasForeignKey(m => m.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
