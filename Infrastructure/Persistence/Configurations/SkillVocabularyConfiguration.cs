using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;


public sealed class SkillVocabularyConfiguration : IEntityTypeConfiguration<SkillVocabularyEntry>
{
    public void Configure(EntityTypeBuilder<SkillVocabularyEntry> b)
    {
        b.ToTable("SkillVocabulary");


        b.HasKey(e => e.CanonicalLower);

        b.Property(e => e.CanonicalLower)
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
            .IsRequired();

        b.Property(e => e.Canonical)
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
            .IsRequired();

        b.Property(e => e.SynonymsJson)
            .HasColumnType("text")
            .IsRequired();

        b.Property(e => e.Domain)
            .HasColumnType("varchar(32)")
            .HasMaxLength(32)
            .IsRequired();

        b.Property(e => e.Confidence)
            .HasColumnType("numeric(3,2)")
            .IsRequired();

        b.Property(e => e.Source)
            .HasColumnType("varchar(32)")
            .HasMaxLength(32)
            .IsRequired();

        b.Property(e => e.ModelVersion)
            .HasColumnType("varchar(64)")
            .HasMaxLength(64);

        b.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.Property(e => e.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();


        b.HasIndex(e => e.Domain).HasDatabaseName("ix_skill_vocab_domain");
        b.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_skill_vocab_created");
    }
}
