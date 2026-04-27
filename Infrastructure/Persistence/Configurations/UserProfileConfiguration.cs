using System.Text.Json;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.PasswordHash)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.DisplayName)
            .HasMaxLength(100);

        builder.Property(x => x.Category)
            .HasMaxLength(100);

        builder.Property(x => x.PreferredLocation)
            .HasMaxLength(200);

        builder.Property(x => x.CvFileUrl)
            .HasMaxLength(500);

        builder.Property(x => x.Skills)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v,
                    (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (c1, c2) => c1!.SequenceEqual(c2!),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));

        builder.Property(x => x.PreferredWorkFormat)
            .HasConversion<string>();

        builder.Property(x => x.SeniorityLevel)
            .HasConversion<string>();

        builder.HasIndex(x => x.Email).IsUnique();
    }
}