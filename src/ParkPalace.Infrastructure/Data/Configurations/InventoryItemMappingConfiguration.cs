using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class InventoryItemMappingConfiguration : IEntityTypeConfiguration<InventoryItemMapping>
{
    public void Configure(EntityTypeBuilder<InventoryItemMapping> builder)
    {
        builder.ToTable("InventoryItemMappings");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.SepidarItemId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.SepidarItemCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.SepidarItemTitle)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(m => m.SepidarBaseUnit)
            .HasMaxLength(50);

        builder.HasIndex(m => m.SepidarItemId)
            .IsUnique();

        builder.HasIndex(m => m.MaterialId);

        builder.Property(m => m.RowVersion)
            .IsRowVersion();

        builder.HasOne(m => m.Material)
            .WithMany()
            .HasForeignKey(m => m.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
