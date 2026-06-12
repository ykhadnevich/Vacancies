using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Infrastructure.Persistence.Entities;
using Pgvector.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseNpgsql(
                "Host=localhost;Database=vakansio;Username=postgres;Password=postgres",
                o => o.UseVector());
    }

    public DbSet<JobVacancy> JobVacancies => Set<JobVacancy>();
    public DbSet<ApplicationTracker> Applications => Set<ApplicationTracker>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<SavedUrl> SavedUrls => Set<SavedUrl>();


    public DbSet<RelevanceExplanation> RelevanceExplanations => Set<RelevanceExplanation>();


    public DbSet<SkillVocabularyEntry> SkillVocabulary => Set<SkillVocabularyEntry>();


    public DbSet<ScoringCacheEntry> ScoringCache => Set<ScoringCacheEntry>();


    public DbSet<GeminiCostLogEntry> GeminiCostLog => Set<GeminiCostLogEntry>();


    public DbSet<CandidateList> CandidateLists => Set<CandidateList>();
    public DbSet<RecruiterCandidate> RecruiterCandidates => Set<RecruiterCandidate>();
    public DbSet<CandidateListMembership> CandidateListMemberships => Set<CandidateListMembership>();
    public DbSet<CandidateScore> CandidateScores => Set<CandidateScore>();

    public DbSet<UserSearchSnapshot> UserSearchSnapshots => Set<UserSearchSnapshot>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.HasPostgresExtension("vector");


        modelBuilder.Entity<RelevanceExplanation>(entity =>
        {
            entity.HasKey(e => new { e.CvVersionId, e.JobId });

            entity.Property(e => e.Reason)
                .IsRequired()
                .HasColumnType("text");

            entity.Property(e => e.ModelVersion)
                .HasMaxLength(50);

            entity.Property(e => e.GeneratedAt)
                .IsRequired();

            entity.HasIndex(e => e.CvVersionId);
            entity.HasIndex(e => e.JobId);

            entity.ToTable("RelevanceExplanations");
        });


        modelBuilder.Entity<GeminiCostLogEntry>(e =>
        {
            e.ToTable("GeminiCostLog");
            e.HasKey(x => x.Id);
            e.Property(x => x.RequestKind).HasMaxLength(64).IsRequired();
            e.Property(x => x.Stage).HasMaxLength(64).IsRequired();
            e.Property(x => x.Keywords).HasMaxLength(256);
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.RequestId);
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
