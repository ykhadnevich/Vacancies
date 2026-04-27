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

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Status);
    }
}