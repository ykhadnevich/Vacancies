using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class CandidateListConfiguration : IEntityTypeConfiguration<CandidateList>
{
    public void Configure(EntityTypeBuilder<CandidateList> b)
    {
        b.ToTable("CandidateLists");

        b.HasKey(x => x.Id);

        b.Property(x => x.RecruiterUserId)
            .HasColumnType("uuid")
            .IsRequired();

        b.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        b.Property(x => x.Description)
            .HasColumnType("text");

        b.Property(x => x.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.Property(x => x.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.HasIndex(x => new { x.RecruiterUserId, x.CreatedAt })
            .HasDatabaseName("ix_candidate_lists_recruiter_created");
    }
}
