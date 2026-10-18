using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class IntegrationSyncRunConfiguration : IEntityTypeConfiguration<IntegrationSyncRun>
{
    public void Configure(EntityTypeBuilder<IntegrationSyncRun> builder)
    {
        builder.ToTable("IntegrationSyncRuns");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.SourceSystem)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.TargetEntity)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasMaxLength(30);

        builder.HasIndex(s => s.StartedAtUtc);
    }
}
