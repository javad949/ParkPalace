using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class MaterialUnitConversionConfiguration : IEntityTypeConfiguration<MaterialUnitConversion>
{
    public void Configure(EntityTypeBuilder<MaterialUnitConversion> builder)
    {
        builder.ToTable("MaterialUnitConversions");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Factor)
            .IsRequired()
            .HasPrecision(18, 6);

        builder.Property(c => c.RowVersion)
            .IsRowVersion();

        builder.HasOne(c => c.Material)
            .WithMany(m => m.UnitConversions)
            .HasForeignKey(c => c.MaterialId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.FromUnit)
            .WithMany()
            .HasForeignKey(c => c.FromUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ToUnit)
            .WithMany()
            .HasForeignKey(c => c.ToUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.MaterialId, c.FromUnitId, c.ToUnitId })
            .IsUnique();
    }
}
