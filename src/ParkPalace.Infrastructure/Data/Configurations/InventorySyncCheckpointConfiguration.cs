using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class InventorySyncCheckpointConfiguration : IEntityTypeConfiguration<InventorySyncCheckpoint>
{
    public void Configure(EntityTypeBuilder<InventorySyncCheckpoint> builder)
    {
        builder.ToTable("InventorySyncCheckpoints");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.LastVoucherId)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(c => c.WarehouseId)
            .IsUnique();

        builder.Property(c => c.RowVersion)
            .IsRowVersion();

        builder.HasOne(c => c.Warehouse)
            .WithMany()
            .HasForeignKey(c => c.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
