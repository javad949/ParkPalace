using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.AdObjectSid)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(u => u.AdObjectSid)
            .IsUnique();

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(u => u.Username);

        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(u => u.Email)
            .HasMaxLength(150);

        builder.Property(u => u.RowVersion)
            .IsRowVersion();
    }
}
