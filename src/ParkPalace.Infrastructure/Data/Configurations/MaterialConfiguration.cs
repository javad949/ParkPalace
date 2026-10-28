using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.ToTable("Materials");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.InternalCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(m => m.InternalCode)
            .IsUnique();

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.Description)
            .HasMaxLength(2000);

        builder.Property(m => m.Specifications)
            .HasMaxLength(4000);

        builder.Property(m => m.Brand)
            .HasMaxLength(100);

        builder.Property(m => m.Packaging)
            .HasMaxLength(100);

        builder.Property(m => m.PackageSize)
            .HasPrecision(18, 4);

        builder.Property(m => m.ProductFamily)
            .HasMaxLength(150);

        builder.Property(m => m.CategorizationStatus)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("NeedsReview");

        builder.Property(m => m.IsCustomizedByUser)
            .HasDefaultValue(false);

        builder.Property(m => m.StandardizationNotes)
            .HasMaxLength(1000);

        builder.Property(m => m.MinStock)
            .HasPrecision(18, 4);

        builder.Property(m => m.SafetyStock)
            .HasPrecision(18, 4);

        builder.Property(m => m.RowVersion)
            .IsRowVersion();

        builder.HasOne(m => m.Category)
            .WithMany(c => c.Materials)
            .HasForeignKey(m => m.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.BaseUnit)
            .WithMany()
            .HasForeignKey(m => m.BaseUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.PackageSizeUnit)
            .WithMany()
            .HasForeignKey(m => m.PackageSizeUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.PurchaseUnit)
            .WithMany()
            .HasForeignKey(m => m.PurchaseUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.ConsumptionUnit)
            .WithMany()
            .HasForeignKey(m => m.ConsumptionUnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
