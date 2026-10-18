using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class SupplierMaterialConfiguration : IEntityTypeConfiguration<SupplierMaterial>
{
    public void Configure(EntityTypeBuilder<SupplierMaterial> builder)
    {
        builder.ToTable("SupplierMaterials");

        builder.HasKey(sm => sm.Id);

        builder.Property(sm => sm.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(sm => sm.MinimumOrderQty)
            .HasPrecision(18, 4);

        builder.Property(sm => sm.Notes)
            .HasMaxLength(1000);

        builder.Property(sm => sm.RowVersion)
            .IsRowVersion();

        builder.HasOne(sm => sm.Party)
            .WithMany(p => p.SupplierMaterials)
            .HasForeignKey(sm => sm.PartyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sm => sm.Material)
            .WithMany(m => m.SupplierMaterials)
            .HasForeignKey(sm => sm.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sm => sm.PurchaseUnit)
            .WithMany()
            .HasForeignKey(sm => sm.PurchaseUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(sm => new { sm.PartyId, sm.MaterialId })
            .IsUnique();
    }
}
