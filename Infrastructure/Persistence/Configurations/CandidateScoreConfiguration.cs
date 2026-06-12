using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class CandidateScoreConfiguration : IEntityTypeConfiguration<CandidateScore>
{
    public void Configure(EntityTypeBuilder<CandidateScore> b)
    {
        b.ToTable("CandidateScores");

        b.HasKey(x => x.Id);

        b.Property(x => x.VacancyId)
            .HasColumnType("uuid")
            .IsRequired();

        b.Property(x => x.RecruiterCandidateId)
            .HasColumnType("uuid")
            .IsRequired();

        b.Property(x => x.Score)
            .HasColumnType("double precision")
            .IsRequired();

        b.Property(x => x.ScoringVersion)
            .HasColumnType("varchar(256)")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.ScoringResultJson)
            .HasColumnType("jsonb")
            .IsRequired();

        b.Property(x => x.ScoredAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Re-analyse upserts on this natural key.
        b.HasIndex(x => new { x.VacancyId, x.RecruiterCandidateId })
            .IsUnique()
            .HasDatabaseName("ux_candidate_scores_vacancy_candidate");

        // Ranking query: latest scores for a vacancy ordered by score desc.
        b.HasIndex(x => new { x.VacancyId, x.Score })
            .HasDatabaseName("ix_candidate_scores_vacancy_score");
    }
}
