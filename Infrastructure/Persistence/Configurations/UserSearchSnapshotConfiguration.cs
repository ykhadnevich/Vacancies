using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class UserSearchSnapshotConfiguration : IEntityTypeConfiguration<UserSearchSnapshot>
{
    public void Configure(EntityTypeBuilder<UserSearchSnapshot> b)
    {
        b.ToTable("UserSearchSnapshots");

        b.HasKey(x => x.Id);

        b.Property(x => x.UserId).HasColumnType("uuid").IsRequired();
        b.Property(x => x.QueryHash).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        b.Property(x => x.Keywords).HasColumnType("varchar(512)").HasMaxLength(512).IsRequired();
        b.Property(x => x.ResponseJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.ExecutedAt).HasColumnType("timestamp with time zone").IsRequired();

        b.HasIndex(x => new { x.UserId, x.QueryHash })
            .IsUnique()
            .HasDatabaseName("ux_user_search_snapshots_user_query");

        b.HasIndex(x => x.ExecutedAt)
            .HasDatabaseName("ix_user_search_snapshots_executed");
    }
}
