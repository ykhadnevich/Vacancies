using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class CandidateListMembershipConfiguration : IEntityTypeConfiguration<CandidateListMembership>
{
    public void Configure(EntityTypeBuilder<CandidateListMembership> b)
    {
        b.ToTable("CandidateListMemberships");

        // Composite primary key — duplicate adds rejected at the database level.
        b.HasKey(x => new { x.CandidateListId, x.RecruiterCandidateId });

        b.Property(x => x.CandidateListId)
            .HasColumnType("uuid")
            .IsRequired();

        b.Property(x => x.RecruiterCandidateId)
            .HasColumnType("uuid")
            .IsRequired();

        b.Property(x => x.AddedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Reverse lookup: which lists contain this candidate.
        b.HasIndex(x => x.RecruiterCandidateId)
            .HasDatabaseName("ix_candidate_list_memberships_candidate");
    }
}
