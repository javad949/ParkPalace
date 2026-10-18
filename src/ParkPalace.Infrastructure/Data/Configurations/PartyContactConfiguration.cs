using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class PartyContactConfiguration : IEntityTypeConfiguration<PartyContact>
{
    public void Configure(EntityTypeBuilder<PartyContact> builder)
    {
        builder.ToTable("PartyContacts");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.PersonName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.Position)
            .HasMaxLength(100);

        builder.Property(c => c.RowVersion)
            .IsRowVersion();

        builder.HasOne(c => c.Party)
            .WithMany(p => p.Contacts)
            .HasForeignKey(c => c.PartyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
