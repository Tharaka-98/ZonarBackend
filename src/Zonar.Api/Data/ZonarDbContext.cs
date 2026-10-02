using Microsoft.EntityFrameworkCore;
using Zonar.Api.Domain;

namespace Zonar.Api.Data;

public class ZonarDbContext : DbContext
{
    public ZonarDbContext(DbContextOptions<ZonarDbContext> options) : base(options) { }

    public DbSet<Community> Communities => Set<Community>();
    public DbSet<Contributor> Contributors => Set<Contributor>();
    public DbSet<Contribution> Contributions => Set<Contribution>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Community>(e =>
        {
            e.HasIndex(x => x.ExternalId).IsUnique();
            e.Property(x => x.ExternalId).HasMaxLength(100).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Contributor>(e =>
        {
            e.HasIndex(x => x.IdentityHash).IsUnique();
            e.Property(x => x.IdentityHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(64).IsRequired();
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Contribution>(e =>
        {
            e.HasIndex(x => new { x.ContributorId, x.CreatedAtUtc });
            e.HasIndex(x => new { x.CommunityId, x.CreatedAtUtc });
            e.HasIndex(x => new { x.ContributorId, x.ContentHash });
            e.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.ScorerUsed).HasMaxLength(80);
            e.Property(x => x.Label).HasConversion<string>().HasMaxLength(10);

            e.HasOne(x => x.Community).WithMany(x => x.Contributions).HasForeignKey(x => x.CommunityId);
            e.HasOne(x => x.Contributor).WithMany(x => x.Contributions).HasForeignKey(x => x.ContributorId);
        });

        // SQLite has no time-zone type: everything is saved as UTC, so mark values as UTC when reading back.
        var utc = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        foreach (var property in b.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(DateTime)))
            property.SetValueConverter(utc);
    }
}
