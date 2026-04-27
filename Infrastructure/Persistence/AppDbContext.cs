using Microsoft.EntityFrameworkCore;
using Domain.Entities;

namespace Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<JobVacancy> JobVacancies => Set<JobVacancy>();
    public DbSet<ApplicationTracker> Applications => Set<ApplicationTracker>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<SavedUrl> SavedUrls => Set<SavedUrl>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}