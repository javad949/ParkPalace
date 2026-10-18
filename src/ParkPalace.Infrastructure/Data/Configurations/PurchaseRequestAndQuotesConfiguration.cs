using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class PurchaseRequestConfiguration : IEntityTypeConfiguration<PurchaseRequest>
{
    public void Configure(EntityTypeBuilder<PurchaseRequest> builder)
    {
        builder.ToTable("PurchaseRequests", "crm");
        builder.HasKey(pr => pr.Id);

        builder.Property(pr => pr.RequestNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(pr => pr.RequestNumber).IsUnique();

        builder.Property(pr => pr.RequesterName).HasMaxLength(100).IsRequired();
        builder.Property(pr => pr.Department).HasMaxLength(100);
        builder.Property(pr => pr.Notes).HasMaxLength(1000);
        builder.Property(pr => pr.CreatedBy).HasMaxLength(100);

        builder.HasMany(pr => pr.Lines)
            .WithOne(l => l.PurchaseRequest)
            .HasForeignKey(l => l.PurchaseRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PurchaseRequestLineConfiguration : IEntityTypeConfiguration<PurchaseRequestLine>
{
    public void Configure(EntityTypeBuilder<PurchaseRequestLine> builder)
    {
        builder.ToTable("PurchaseRequestLines", "crm");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.EstimatedUnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.Notes).HasMaxLength(500);

        builder.HasOne(l => l.Material)
            .WithMany()
            .HasForeignKey(l => l.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Unit)
            .WithMany()
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RequestForQuotationConfiguration : IEntityTypeConfiguration<RequestForQuotation>
{
    public void Configure(EntityTypeBuilder<RequestForQuotation> builder)
    {
        builder.ToTable("RequestForQuotations", "crm");
        builder.HasKey(rfq => rfq.Id);

        builder.Property(rfq => rfq.RfqNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(rfq => rfq.RfqNumber).IsUnique();

        builder.Property(rfq => rfq.Title).HasMaxLength(200).IsRequired();
        builder.Property(rfq => rfq.Notes).HasMaxLength(1000);
        builder.Property(rfq => rfq.CreatedBy).HasMaxLength(100);

        builder.HasMany(rfq => rfq.Suppliers)
            .WithOne(s => s.RequestForQuotation)
            .HasForeignKey(s => s.RequestForQuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(rfq => rfq.Quotations)
            .WithOne(q => q.RequestForQuotation)
            .HasForeignKey(q => q.RequestForQuotationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class RfqSupplierConfiguration : IEntityTypeConfiguration<RfqSupplier>
{
    public void Configure(EntityTypeBuilder<RfqSupplier> builder)
    {
        builder.ToTable("RfqSuppliers", "crm");
        builder.HasKey(s => s.Id);

        builder.HasOne(s => s.Supplier)
            .WithMany()
            .HasForeignKey(s => s.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SupplierQuotationConfiguration : IEntityTypeConfiguration<SupplierQuotation>
{
    public void Configure(EntityTypeBuilder<SupplierQuotation> builder)
    {
        builder.ToTable("SupplierQuotations", "crm");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.QuotationNumber).HasMaxLength(50).IsRequired();
        builder.Property(q => q.Currency).HasMaxLength(10).IsRequired();
        builder.Property(q => q.ExchangeRate).HasPrecision(18, 4);
        builder.Property(q => q.PaymentTerms).HasMaxLength(200);
        builder.Property(q => q.TotalAmount).HasPrecision(18, 2);
        builder.Property(q => q.Notes).HasMaxLength(1000);

        builder.HasOne(q => q.Supplier)
            .WithMany()
            .HasForeignKey(q => q.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(q => q.Lines)
            .WithOne(l => l.SupplierQuotation)
            .HasForeignKey(l => l.SupplierQuotationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SupplierQuotationLineConfiguration : IEntityTypeConfiguration<SupplierQuotationLine>
{
    public void Configure(EntityTypeBuilder<SupplierQuotationLine> builder)
    {
        builder.ToTable("SupplierQuotationLines", "crm");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.OfferedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.TaxRate).HasPrecision(18, 4);
        builder.Property(l => l.TaxAmount).HasPrecision(18, 2);
        builder.Property(l => l.DiscountAmount).HasPrecision(18, 2);
        builder.Property(l => l.LineTotal).HasPrecision(18, 2);
        builder.Property(l => l.Notes).HasMaxLength(500);

        builder.HasOne(l => l.Material)
            .WithMany()
            .HasForeignKey(l => l.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Unit)
            .WithMany()
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
