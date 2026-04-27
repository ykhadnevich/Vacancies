using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class SavedUrlConfiguration : IEntityTypeConfiguration<SavedUrl>
{
    public void Configure(EntityTypeBuilder<SavedUrl> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Url)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.Alias)
            .HasMaxLength(200);

        builder.HasIndex(x => x.Url)
            .IsUnique();
    }
}