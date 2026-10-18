using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class MaterialConsumptionSummaryConfiguration : IEntityTypeConfiguration<MaterialConsumptionSummary>
{
    public void Configure(EntityTypeBuilder<MaterialConsumptionSummary> builder)
    {
        builder.ToTable("MaterialConsumptionSummaries");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TotalConsumption)
            .HasPrecision(18, 4);

        builder.Property(s => s.AverageDailyConsumption)
            .HasPrecision(18, 4);

        builder.HasIndex(s => new { s.MaterialId, s.PeriodDays })
            .IsUnique();

        builder.Property(s => s.RowVersion)
            .IsRowVersion();

        builder.HasOne(s => s.Material)
            .WithMany()
            .HasForeignKey(s => s.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
