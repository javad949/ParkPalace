using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class InventorySyncErrorConfiguration : IEntityTypeConfiguration<InventorySyncError>
{
    public void Configure(EntityTypeBuilder<InventorySyncError> builder)
    {
        builder.ToTable("InventorySyncErrors");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ErrorCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.ErrorMessage)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(e => e.ItemKey)
            .HasMaxLength(50);

        builder.Property(e => e.VoucherNumber)
            .HasMaxLength(50);

        builder.Property(e => e.RawPayload)
            .HasMaxLength(4000);

        builder.Property(e => e.RowVersion)
            .IsRowVersion();

        builder.HasOne(e => e.Job)
            .WithMany()
            .HasForeignKey(e => e.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
