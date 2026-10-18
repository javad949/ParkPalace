using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class PartyAddressConfiguration : IEntityTypeConfiguration<PartyAddress>
{
    public void Configure(EntityTypeBuilder<PartyAddress> builder)
    {
        builder.ToTable("PartyAddresses");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.AddressType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Province)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.City)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.AddressText)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.PostalCode)
            .HasMaxLength(20);

        builder.Property(a => a.RowVersion)
            .IsRowVersion();

        builder.HasOne(a => a.Party)
            .WithMany(p => p.Addresses)
            .HasForeignKey(a => a.PartyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
