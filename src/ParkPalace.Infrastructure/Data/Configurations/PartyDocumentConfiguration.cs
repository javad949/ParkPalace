using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class PartyDocumentConfiguration : IEntityTypeConfiguration<PartyDocument>
{
    public void Configure(EntityTypeBuilder<PartyDocument> builder)
    {
        builder.ToTable("PartyDocuments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.DocumentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.FileExtension)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.StorageReference)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.RowVersion)
            .IsRowVersion();

        builder.HasOne(d => d.Party)
            .WithMany(p => p.Documents)
            .HasForeignKey(d => d.PartyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
