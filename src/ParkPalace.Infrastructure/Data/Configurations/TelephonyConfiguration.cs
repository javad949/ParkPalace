using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data.Configurations;

public class CallRecordConfiguration : IEntityTypeConfiguration<CallRecord>
{
    public void Configure(EntityTypeBuilder<CallRecord> builder)
    {
        builder.ToTable("CallRecords", "crm");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.UniqueId).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => c.UniqueId).IsUnique();

        builder.Property(c => c.LinkedId).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => c.LinkedId);

        builder.Property(c => c.CallerNumber).HasMaxLength(50).IsRequired();
        builder.Property(c => c.NormalizedCallerNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => c.NormalizedCallerNumber);

        builder.Property(c => c.CallerName).HasMaxLength(200);
        builder.Property(c => c.DestinationNumber).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Extension).HasMaxLength(50);
        builder.HasIndex(c => c.Extension);

        builder.Property(c => c.Channel).HasMaxLength(200);
        builder.Property(c => c.DestinationChannel).HasMaxLength(200);

        builder.HasIndex(c => c.StartTimeUtc);
        builder.HasIndex(c => c.Status);

        builder.Property(c => c.MatchConfidence).HasMaxLength(20).IsRequired();

        builder.Property(c => c.RecordingFileName).HasMaxLength(250);
        builder.Property(c => c.RecordingFilePath).HasMaxLength(500);

        builder.Property(c => c.CdrId).HasMaxLength(100);
        builder.Property(c => c.DisconnectionCause).HasMaxLength(100);

        builder.HasOne(c => c.Party)
            .WithMany()
            .HasForeignKey(c => c.PartyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Contact)
            .WithMany()
            .HasForeignKey(c => c.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Notes)
            .WithOne(n => n.CallRecord)
            .HasForeignKey(n => n.CallRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Audits)
            .WithOne(a => a.CallRecord)
            .HasForeignKey(a => a.CallRecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CallNoteConfiguration : IEntityTypeConfiguration<CallNote>
{
    public void Configure(EntityTypeBuilder<CallNote> builder)
    {
        builder.ToTable("CallNotes", "crm");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Username).HasMaxLength(100).IsRequired();
        builder.Property(n => n.NoteText).HasMaxLength(4000).IsRequired();
        builder.Property(n => n.Outcome).HasMaxLength(50).IsRequired();
        builder.Property(n => n.FollowUpAssignedTo).HasMaxLength(100);

        builder.HasIndex(n => n.CallRecordId);
        builder.HasIndex(n => n.FollowUpStatus);
    }
}

public class CallAuditConfiguration : IEntityTypeConfiguration<CallAudit>
{
    public void Configure(EntityTypeBuilder<CallAudit> builder)
    {
        builder.ToTable("CallAudits", "crm");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).HasMaxLength(50).IsRequired();
        builder.Property(a => a.PerformedBy).HasMaxLength(100).IsRequired();
        builder.Property(a => a.DetailsJson).HasMaxLength(4000);

        builder.HasIndex(a => a.CallRecordId);
        builder.HasIndex(a => a.TimestampUtc);
    }
}

public class UserExtensionConfiguration : IEntityTypeConfiguration<UserExtension>
{
    public void Configure(EntityTypeBuilder<UserExtension> builder)
    {
        builder.ToTable("UserExtensions", "crm");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Username).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Extension).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.Extension).IsUnique();

        builder.Property(e => e.DeviceType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(250);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class TelephonyGroupConfiguration : IEntityTypeConfiguration<TelephonyGroup>
{
    public void Configure(EntityTypeBuilder<TelephonyGroup> builder)
    {
        builder.ToTable("TelephonyGroups", "crm");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name).HasMaxLength(100).IsRequired();
        builder.Property(g => g.Description).HasMaxLength(500);
        builder.Property(g => g.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(g => g.SupervisorExtension).HasMaxLength(50);
        builder.Property(g => g.SupervisorDisplayName).HasMaxLength(200);

        builder.HasOne(g => g.SupervisorUser)
            .WithMany()
            .HasForeignKey(g => g.SupervisorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(g => g.Members)
            .WithOne(m => m.Group)
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TelephonyGroupMemberConfiguration : IEntityTypeConfiguration<TelephonyGroupMember>
{
    public void Configure(EntityTypeBuilder<TelephonyGroupMember> builder)
    {
        builder.ToTable("TelephonyGroupMembers", "crm");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Extension).HasMaxLength(50).IsRequired();
        builder.Property(m => m.DisplayName).HasMaxLength(200);

        builder.HasIndex(m => new { m.GroupId, m.Extension }).IsUnique();

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
