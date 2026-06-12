using Domain.Entities;
using Domain.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;


public sealed class ScoringCacheConfiguration : IEntityTypeConfiguration<ScoringCacheEntry>
{
    public void Configure(EntityTypeBuilder<ScoringCacheEntry> b)
    {
        b.ToTable("ScoringCache");


        b.HasKey(e => new { e.CvHash, e.VacancyId, e.ScoringVersion });

        b.Property(e => e.CvHash)
            .HasColumnType("varchar(64)")
            .HasMaxLength(64)
            .IsRequired();

        b.Property(e => e.VacancyId)
            .HasColumnType("uuid")
            .IsRequired();

        b.Property(e => e.ScoringVersion)
            .HasColumnType("varchar(256)")
            .HasMaxLength(256)
            .IsRequired();


        b.Property(e => e.JudgeScore)
            .HasColumnType("double precision");

        b.Property(e => e.JudgeVerdict)
            .HasConversion<int?>()
            .HasColumnType("int");


        b.Property(e => e.StrengthsEn).HasColumnType("text");
        b.Property(e => e.StrengthsUk).HasColumnType("text");
        b.Property(e => e.GapsEn).HasColumnType("text");
        b.Property(e => e.GapsUk).HasColumnType("text");
        b.Property(e => e.RecommendationEn).HasColumnType("text");
        b.Property(e => e.RecommendationUk).HasColumnType("text");


        b.Property(e => e.MonoResultJson).HasColumnType("jsonb");

        b.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.Property(e => e.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();


        b.HasIndex(e => new { e.VacancyId, e.ScoringVersion })
         .HasDatabaseName("ix_scoring_cache_vacancy_version");
        b.HasIndex(e => e.CreatedAt)
         .HasDatabaseName("ix_scoring_cache_created");
    }
}
