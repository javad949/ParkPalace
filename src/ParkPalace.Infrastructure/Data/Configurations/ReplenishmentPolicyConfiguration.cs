using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class ReplenishmentPolicyConfiguration : IEntityTypeConfiguration<ReplenishmentPolicy>
{
    public void Configure(EntityTypeBuilder<ReplenishmentPolicy> builder)
    {
        builder.ToTable("ReplenishmentPolicies");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.SafetyStock)
            .HasPrecision(18, 4);

        builder.Property(p => p.MinOrderQuantity)
            .HasPrecision(18, 4);

        builder.Property(p => p.PackageMultiple)
            .HasPrecision(18, 4);

        builder.Property(p => p.Notes)
            .HasMaxLength(1000);

        builder.Property(p => p.UpdatedBy)
            .HasMaxLength(100);

        builder.HasIndex(p => p.MaterialId)
            .IsUnique();

        builder.Property(p => p.RowVersion)
            .IsRowVersion();

        builder.HasOne(p => p.Material)
            .WithMany()
            .HasForeignKey(p => p.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.PreferredSupplier)
            .WithMany()
            .HasForeignKey(p => p.PreferredSupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
