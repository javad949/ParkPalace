using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class InventorySnapshotConfiguration : IEntityTypeConfiguration<InventorySnapshot>
{
    public void Configure(EntityTypeBuilder<InventorySnapshot> builder)
    {
        builder.ToTable("InventorySnapshots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.OnHandQuantity)
            .HasPrecision(18, 4);

        builder.Property(s => s.ReservableQuantity)
            .HasPrecision(18, 4);

        builder.Property(s => s.IncomingQuantity)
            .HasPrecision(18, 4);

        builder.Property(s => s.ReservedQuantity)
            .HasPrecision(18, 4);

        builder.Property(s => s.SourceSnapshotKey)
            .HasMaxLength(150);

        builder.HasIndex(s => new { s.WarehouseId, s.MaterialId })
            .IsUnique();

        builder.Property(s => s.RowVersion)
            .IsRowVersion();

        builder.HasOne(s => s.Warehouse)
            .WithMany(w => w.Snapshots)
            .HasForeignKey(s => s.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Material)
            .WithMany()
            .HasForeignKey(s => s.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Unit)
            .WithMany()
            .HasForeignKey(s => s.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
