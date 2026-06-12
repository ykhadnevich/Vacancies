using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Infrastructure.Persistence.Configurations;

public class ApplicationTrackerConfiguration : IEntityTypeConfiguration<ApplicationTracker>
{
    public void Configure(EntityTypeBuilder<ApplicationTracker> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(x => x.Company)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Url)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Location)
            .HasMaxLength(200);

        builder.Property(x => x.Salary)
            .HasMaxLength(100);

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        builder.Property(x => x.Status)
            .HasConversion<string>();

        builder.Property(x => x.SeniorityLevel)
            .HasConversion<string>();

        builder.Property<Dictionary<string, bool>>("_pipelineSteps")
            .HasField("_pipelineSteps")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("PipelineSteps")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, bool>>(v,
                    (JsonSerializerOptions?)null) ?? new Dictionary<string, bool>())
            .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, bool>>(
                (c1, c2) => c1!.SequenceEqual(c2!),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value.GetHashCode())),
                c => new Dictionary<string, bool>(c)));


        builder.Property(x => x.Score);
        builder.Property(x => x.Verdict).HasMaxLength(20);
        builder.Property(x => x.ReasonShort).HasMaxLength(2000);
        builder.Property(x => x.StrengthsEn).HasMaxLength(2000);
        builder.Property(x => x.StrengthsUk).HasMaxLength(2000);
        builder.Property(x => x.GapsEn).HasMaxLength(2000);
        builder.Property(x => x.GapsUk).HasMaxLength(2000);
        builder.Property(x => x.RecommendationEn).HasMaxLength(2000);
        builder.Property(x => x.RecommendationUk).HasMaxLength(2000);
        builder.Property(x => x.CvFileName).HasMaxLength(255);
        builder.Property(x => x.PipelineVersion).HasMaxLength(100);
        builder.Property(x => x.AnalyzedAt);

        var dictComparer = new ValueComparer<Dictionary<string, double>?>(
            (c1, c2) => c1 == null ? c2 == null : c2 != null && c1.SequenceEqual(c2),
            c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value.GetHashCode())),
            c => c == null ? null! : new Dictionary<string, double>(c));

        builder.Property<Dictionary<string, double>?>("_subScores")
            .HasField("_subScores")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("SubScores")
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, double>>(v, (JsonSerializerOptions?)null))
            .Metadata.SetValueComparer(dictComparer);

        var listComparer = new ValueComparer<List<string>?>(
            (c1, c2) => c1 == null ? c2 == null : c2 != null && c1.SequenceEqual(c2),
            c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c == null ? null! : new List<string>(c));

        builder.Property<List<string>?>("_matchedSkills")
            .HasField("_matchedSkills")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("MatchedSkills")
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .Metadata.SetValueComparer(listComparer);

        builder.Property<List<string>?>("_missingMustHaves")
            .HasField("_missingMustHaves")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("MissingMustHaves")
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .Metadata.SetValueComparer(listComparer);

        builder.Property<List<string>?>("_triggeredAntiFlags")
            .HasField("_triggeredAntiFlags")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("TriggeredAntiFlags")
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .Metadata.SetValueComparer(listComparer);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Status);
    }
}
