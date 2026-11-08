using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> builder)
    {
        builder.ToTable("Parties");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PartyType)
            .IsRequired();

        builder.Property(p => p.Category)
            .IsRequired();

        builder.Property(p => p.SepidarCode)
            .HasMaxLength(50);

        builder.Property(p => p.SepidarTitle)
            .HasMaxLength(300);

        builder.Property(p => p.SepidarPartyRef)
            .HasMaxLength(50);

        builder.Property(p => p.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Notes)
            .HasMaxLength(2000);

        // Individual fields
        builder.Property(p => p.FirstName)
            .HasMaxLength(100);

        builder.Property(p => p.LastName)
            .HasMaxLength(100);

        builder.Property(p => p.ShopName)
            .HasMaxLength(200);

        builder.Property(p => p.NationalId)
            .HasMaxLength(20);

        builder.HasIndex(p => p.NationalId)
            .HasFilter("[NationalId] IS NOT NULL");

        // Legal fields
        builder.Property(p => p.LegalName)
            .HasMaxLength(200);

        builder.Property(p => p.NationalIdentifier)
            .HasMaxLength(30);

        builder.HasIndex(p => p.NationalIdentifier)
            .HasFilter("[NationalIdentifier] IS NOT NULL");

        builder.Property(p => p.RegistrationNumber)
            .HasMaxLength(50);

        builder.Property(p => p.EconomicCode)
            .HasMaxLength(30);

        builder.Property(p => p.RowVersion)
            .IsRowVersion();
    }
}
