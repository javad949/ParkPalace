using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class ExternalItemMappingConfiguration : IEntityTypeConfiguration<ExternalItemMapping>
{
    public void Configure(EntityTypeBuilder<ExternalItemMapping> builder)
    {
        builder.ToTable("ExternalItemMappings");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.SourceSystem)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.SourceCompany)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.ExternalItemId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.ExternalItemCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.RowVersion)
            .IsRowVersion();

        builder.HasOne(e => e.Material)
            .WithMany(m => m.ExternalMappings)
            .HasForeignKey(e => e.MaterialId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SourceSystem, e.SourceCompany, e.ExternalItemId })
            .IsUnique();
    }
}
