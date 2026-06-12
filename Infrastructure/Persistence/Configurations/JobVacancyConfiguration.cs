using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Pgvector;

namespace Infrastructure.Persistence.Configurations;

public class JobVacancyConfiguration : IEntityTypeConfiguration<JobVacancy>
{
    public void Configure(EntityTypeBuilder<JobVacancy> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(x => x.Company)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Location)
            .HasMaxLength(200);

        builder.Property(x => x.Category)
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasColumnType("text");

        builder.OwnsOne(x => x.Salary, salary =>
        {
            salary.Property(s => s.RawText).HasMaxLength(100).HasColumnName("SalaryRaw");
            salary.Property(s => s.MinAmount).HasColumnName("SalaryMin");
            salary.Property(s => s.MaxAmount).HasColumnName("SalaryMax");
            salary.Property(s => s.Currency).HasMaxLength(10).HasColumnName("SalaryCurrency");
        });

        builder.Navigation(x => x.Salary).IsRequired(false);

        builder.OwnsOne(x => x.RelevanceScore, score =>
        {
            score.Property(s => s.Value).HasColumnName("RelevanceScore");
            score.Property(s => s.Stage).HasColumnName("RelevanceStage");
        });

        builder.Property(x => x.Urls)
            .HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
            .HasColumnType("text")
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (c1, c2) => c1!.SequenceEqual(c2!),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));

        builder.Property(x => x.Source)
            .HasConversion<string>();

        builder.Property(x => x.WorkFormat)
            .HasConversion<string>();

        builder.Property(x => x.SeniorityLevel)
            .HasConversion<string>();


        builder.Property(x => x.Embedding)
            .HasColumnType("vector(768)")
            .HasConversion(
                v => new Pgvector.Vector(v),
                v => v.Memory.ToArray(),
                new ValueComparer<float[]>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    v => v.Aggregate(0, (a, e) => HashCode.Combine(a, e.GetHashCode())),
                    v => v.ToArray()),
                new ValueComparer<Pgvector.Vector>(
                    (a, b) => a != null && b != null && a.Memory.ToArray().SequenceEqual(b.Memory.ToArray()),
                    v => v.Memory.ToArray().Aggregate(0, (a, e) => HashCode.Combine(a, e.GetHashCode())),
                    v => new Pgvector.Vector(v.Memory.ToArray())))
            .IsRequired(false);


        builder.Ignore(x => x.Reason);


        builder.Property(x => x.ApplicantCount).IsRequired(false);
        builder.Property(x => x.RecruiterRespondsQuickly).IsRequired(false);


        builder.Property(x => x.VacancyAnalysisJson)
            .HasColumnType("text")
            .IsRequired(false);
        builder.Property(x => x.VacancyAnalyzedAt).IsRequired(false);
        builder.Property(x => x.VacancyAnalysisModelVersion)
            .HasMaxLength(64)
            .IsRequired(false);

        builder.Property(x => x.OwnerUserId)
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.HasIndex(x => x.Company);
        builder.HasIndex(x => x.PublishedAt);
        builder.HasIndex(x => x.Source);
        builder.HasIndex(x => x.OwnerUserId)
            .HasDatabaseName("ix_job_vacancies_owner")
            .HasFilter("\"OwnerUserId\" IS NOT NULL");
    }
}
