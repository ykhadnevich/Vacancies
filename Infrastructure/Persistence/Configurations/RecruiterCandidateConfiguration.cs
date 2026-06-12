using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class RecruiterCandidateConfiguration : IEntityTypeConfiguration<RecruiterCandidate>
{
    public void Configure(EntityTypeBuilder<RecruiterCandidate> b)
    {
        b.ToTable("RecruiterCandidates");

        b.HasKey(x => x.Id);

        b.Property(x => x.RecruiterUserId)
            .HasColumnType("uuid")
            .IsRequired();

        b.Property(x => x.CandidateName)
            .HasMaxLength(200);

        b.Property(x => x.CvRawText)
            .HasColumnType("text")
            .IsRequired();

        b.Property(x => x.CvNormalizedJson)
            .HasColumnType("jsonb");

        b.Property(x => x.CvHash)
            .HasColumnType("varchar(64)")
            .HasMaxLength(64);

        b.Property(x => x.NormalizationModelVersion)
            .HasColumnType("varchar(64)")
            .HasMaxLength(64);

        b.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.LastError)
            .HasMaxLength(500);

        b.Property(x => x.AddedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.Property(x => x.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.HasIndex(x => new { x.RecruiterUserId, x.AddedAt })
            .HasDatabaseName("ix_recruiter_candidates_recruiter_added");

        // Used by the analyse handler to dedupe by hash within a recruiter's pool.
        b.HasIndex(x => new { x.RecruiterUserId, x.CvHash })
            .HasDatabaseName("ix_recruiter_candidates_recruiter_hash");
    }
}
