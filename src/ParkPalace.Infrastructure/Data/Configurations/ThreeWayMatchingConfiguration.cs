using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class ThreeWayMatchResultConfiguration : IEntityTypeConfiguration<ThreeWayMatchResult>
{
    public void Configure(EntityTypeBuilder<ThreeWayMatchResult> builder)
    {
        builder.ToTable("ThreeWayMatchResults", "crm");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.EvaluatedBy).HasMaxLength(100).IsRequired();
        builder.Property(r => r.TotalVarianceAmount).HasPrecision(18, 2);
        builder.Property(r => r.TolerancePercentageApplied).HasPrecision(18, 4);

        builder.Property(r => r.ResolutionAction).HasMaxLength(100);
        builder.Property(r => r.ResolvedBy).HasMaxLength(100);
        builder.Property(r => r.ResolutionNotes).HasMaxLength(1000);

        builder.HasOne(r => r.PurchaseOrder)
            .WithMany()
            .HasForeignKey(r => r.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.GoodsReceipt)
            .WithMany()
            .HasForeignKey(r => r.GoodsReceiptId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.SupplierInvoice)
            .WithMany()
            .HasForeignKey(r => r.SupplierInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.LineVariances)
            .WithOne(v => v.ThreeWayMatchResult)
            .HasForeignKey(v => v.ThreeWayMatchResultId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ThreeWayMatchLineVarianceConfiguration : IEntityTypeConfiguration<ThreeWayMatchLineVariance>
{
    public void Configure(EntityTypeBuilder<ThreeWayMatchLineVariance> builder)
    {
        builder.ToTable("ThreeWayMatchLineVariances", "crm");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.PoQuantity).HasPrecision(18, 4);
        builder.Property(v => v.ReceivedQuantity).HasPrecision(18, 4);
        builder.Property(v => v.InvoicedQuantity).HasPrecision(18, 4);
        builder.Property(v => v.QuantityVariance).HasPrecision(18, 4);

        builder.Property(v => v.PoUnitPrice).HasPrecision(18, 2);
        builder.Property(v => v.InvoicedUnitPrice).HasPrecision(18, 2);
        builder.Property(v => v.PriceVariance).HasPrecision(18, 2);
        builder.Property(v => v.PriceVariancePercent).HasPrecision(18, 4);

        builder.Property(v => v.PoLineTotal).HasPrecision(18, 2);
        builder.Property(v => v.InvoicedLineTotal).HasPrecision(18, 2);
        builder.Property(v => v.TotalVariance).HasPrecision(18, 2);

        builder.Property(v => v.MismatchType).HasMaxLength(50).IsRequired();
        builder.Property(v => v.Notes).HasMaxLength(500);

        builder.HasOne(v => v.Material)
            .WithMany()
            .HasForeignKey(v => v.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SepidarFinancialReconciliationConfiguration : IEntityTypeConfiguration<SepidarFinancialReconciliation>
{
    public void Configure(EntityTypeBuilder<SepidarFinancialReconciliation> builder)
    {
        builder.ToTable("SepidarFinancialReconciliations", "crm");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SepidarInvoiceId).HasMaxLength(100).IsRequired();
        builder.Property(s => s.SepidarVoucherNumber).HasMaxLength(100).IsRequired();
        builder.Property(s => s.SepidarAmount).HasPrecision(18, 2);
        builder.Property(s => s.CandidateConfidenceScore).HasPrecision(5, 4);

        builder.Property(s => s.LinkedBy).HasMaxLength(100);
        builder.Property(s => s.AuditNotes).HasMaxLength(1000);

        builder.HasOne(s => s.SupplierInvoice)
            .WithMany()
            .HasForeignKey(s => s.SupplierInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
