using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> b)
    {
        b.ToTable("AuditEntries");

        b.HasKey(x => x.Id);

        b.Property(x => x.UserId).HasColumnType("uuid");
        b.Property(x => x.Action).HasColumnType("varchar(128)").HasMaxLength(128).IsRequired();
        b.Property(x => x.EntityType).HasColumnType("varchar(64)").HasMaxLength(64);
        b.Property(x => x.EntityId).HasColumnType("uuid");
        b.Property(x => x.PayloadJson).HasColumnType("jsonb");
        b.Property(x => x.Outcome).HasColumnType("varchar(32)").HasMaxLength(32).IsRequired();
        b.Property(x => x.Timestamp).HasColumnType("timestamp with time zone").IsRequired();
        b.Property(x => x.IpAddress).HasColumnType("varchar(64)").HasMaxLength(64);
        b.Property(x => x.UserAgent).HasColumnType("varchar(512)").HasMaxLength(512);

        b.HasIndex(x => new { x.UserId, x.Timestamp })
            .HasDatabaseName("ix_audit_entries_user_timestamp");

        b.HasIndex(x => new { x.EntityType, x.EntityId, x.Timestamp })
            .HasDatabaseName("ix_audit_entries_entity_timestamp");

        b.HasIndex(x => x.Timestamp)
            .HasDatabaseName("ix_audit_entries_timestamp");
    }
}
