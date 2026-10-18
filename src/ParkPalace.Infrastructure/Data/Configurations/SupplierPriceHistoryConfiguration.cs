using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class SupplierPriceHistoryConfiguration : IEntityTypeConfiguration<SupplierPriceHistory>
{
    public void Configure(EntityTypeBuilder<SupplierPriceHistory> builder)
    {
        builder.ToTable("SupplierPriceHistories");

        builder.HasKey(ph => ph.Id);

        builder.Property(ph => ph.Price)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(ph => ph.Currency)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(ph => ph.Source)
            .HasMaxLength(200);

        builder.Property(ph => ph.Notes)
            .HasMaxLength(1000);

        builder.Property(ph => ph.RowVersion)
            .IsRowVersion();

        builder.HasOne(ph => ph.SupplierMaterial)
            .WithMany(sm => sm.PriceHistories)
            .HasForeignKey(ph => ph.SupplierMaterialId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ph => ph.Unit)
            .WithMany()
            .HasForeignKey(ph => ph.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ph => new { ph.SupplierMaterialId, ph.EffectiveFrom });
    }
}
