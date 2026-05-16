using GitHubScraper.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace GitHubScraper.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ScrapingOperation> Operations => Set<ScrapingOperation>();
    public DbSet<DeveloperResult> DeveloperResults => Set<DeveloperResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── ScrapingOperation ──────────────────────────────────────────────
        modelBuilder.Entity<ScrapingOperation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.GitHubToken).HasMaxLength(500);
            e.Property(x => x.KeywordsJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.LocationsJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.CurrentUser).HasMaxLength(200);
            e.Property(x => x.CurrentQuery).HasMaxLength(1000);
            e.Property(x => x.CheckpointJson).HasColumnType("nvarchar(max)");
        });

        // ── DeveloperResult ────────────────────────────────────────────────
        modelBuilder.Entity<DeveloperResult>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(200).IsRequired();
            e.Property(x => x.Name).HasMaxLength(300);
            e.Property(x => x.Location).HasMaxLength(500);
            e.Property(x => x.Bio).HasColumnType("nvarchar(max)");
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.Property(x => x.EmailSource).HasMaxLength(100);
            e.Property(x => x.EmailConfidence).HasMaxLength(50);
            e.Property(x => x.Website).HasMaxLength(500);
            e.Property(x => x.ProfileUrl).HasMaxLength(500);

            // Unique per operation: no two rows with same username for the same operation
            e.HasIndex(x => new { x.OperationId, x.Username }).IsUnique();
            // Fast email lookups / export filtering
            e.HasIndex(x => new { x.OperationId, x.Email });

            e.HasOne(x => x.Operation)
             .WithMany(o => o.Results)
             .HasForeignKey(x => x.OperationId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
