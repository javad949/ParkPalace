using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class PurchaseRecommendationConfiguration : IEntityTypeConfiguration<PurchaseRecommendation>
{
    public void Configure(EntityTypeBuilder<PurchaseRecommendation> builder)
    {
        builder.ToTable("PurchaseRecommendations");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.CurrentOnHand)
            .HasPrecision(18, 4);

        builder.Property(r => r.ReservableOnHand)
            .HasPrecision(18, 4);

        builder.Property(r => r.IncomingOrders)
            .HasPrecision(18, 4);

        builder.Property(r => r.ReservedQuantity)
            .HasPrecision(18, 4);

        builder.Property(r => r.AverageDailyConsumption)
            .HasPrecision(18, 4);

        builder.Property(r => r.SafetyStock)
            .HasPrecision(18, 4);

        builder.Property(r => r.TargetStock)
            .HasPrecision(18, 4);

        builder.Property(r => r.InventoryPosition)
            .HasPrecision(18, 4);

        builder.Property(r => r.RawSuggestedQuantity)
            .HasPrecision(18, 4);

        builder.Property(r => r.RoundedSuggestedQuantity)
            .HasPrecision(18, 4);

        builder.Property(r => r.RecommendationExplanation)
            .HasMaxLength(2000);

        builder.Property(r => r.DataWarning)
            .HasMaxLength(1000);

        builder.HasIndex(r => new { r.MaterialId, r.Status });

        builder.Property(r => r.RowVersion)
            .IsRowVersion();

        builder.HasOne(r => r.Material)
            .WithMany()
            .HasForeignKey(r => r.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Warehouse)
            .WithMany()
            .HasForeignKey(r => r.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Unit)
            .WithMany()
            .HasForeignKey(r => r.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.PreferredSupplier)
            .WithMany()
            .HasForeignKey(r => r.PreferredSupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
