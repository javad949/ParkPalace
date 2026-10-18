using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class PartyPhoneConfiguration : IEntityTypeConfiguration<PartyPhone>
{
    public void Configure(EntityTypeBuilder<PartyPhone> builder)
    {
        builder.ToTable("PartyPhones");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PhoneType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.OriginalValue)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.NormalizedValue)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(p => p.NormalizedValue);

        builder.Property(p => p.RowVersion)
            .IsRowVersion();

        builder.HasOne(p => p.Party)
            .WithMany(party => party.Phones)
            .HasForeignKey(p => p.PartyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Contact)
            .WithMany(c => c.Phones)
            .HasForeignKey(p => p.ContactId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
