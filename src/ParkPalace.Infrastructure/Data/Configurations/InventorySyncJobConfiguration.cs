using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class InventorySyncJobConfiguration : IEntityTypeConfiguration<InventorySyncJob>
{
    public void Configure(EntityTypeBuilder<InventorySyncJob> builder)
    {
        builder.ToTable("InventorySyncJobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.CorrelationId)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(j => j.CorrelationId);

        builder.Property(j => j.TriggeredBy)
            .HasMaxLength(100);

        builder.Property(j => j.ErrorMessage)
            .HasMaxLength(4000);

        builder.Property(j => j.RowVersion)
            .IsRowVersion();
    }
}
