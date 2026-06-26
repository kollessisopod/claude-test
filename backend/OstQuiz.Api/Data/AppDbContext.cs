using Microsoft.EntityFrameworkCore;
using OstQuiz.Api.Domain;

namespace OstQuiz.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Puzzle> Puzzles => Set<Puzzle>();
    public DbSet<PuzzleClip> PuzzleClips => Set<PuzzleClip>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Game>(e =>
        {
            e.Property(g => g.Name).HasMaxLength(400).IsRequired();
            e.Property(g => g.Slug).HasMaxLength(400);
            e.Property(g => g.Publisher).HasMaxLength(400);
            e.Property(g => g.Developer).HasMaxLength(400);
            e.Property(g => g.Franchise).HasMaxLength(400);
            e.HasIndex(g => g.Franchise);
            e.Property(g => g.CoverImageKey).HasMaxLength(1024);
            // Stored as a Postgres text[] by Npgsql.
            e.Property(g => g.Genres).HasColumnType("text[]");
            e.HasIndex(g => g.RawgId).IsUnique().HasFilter("\"RawgId\" IS NOT NULL");
            e.HasIndex(g => g.Name);
        });

        b.Entity<Puzzle>(e =>
        {
            e.Property(p => p.AudioKey).HasMaxLength(1024).IsRequired();
            e.Property(p => p.AlbumCoverKey).HasMaxLength(1024);
            e.HasIndex(p => p.PuzzleDate).IsUnique();
            e.HasOne(p => p.Game)
                .WithMany(g => g.Puzzles)
                .HasForeignKey(p => p.GameId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PuzzleClip>(e =>
        {
            e.Property(c => c.ObjectKey).HasMaxLength(1024).IsRequired();
            e.HasOne(c => c.Puzzle)
                .WithMany(p => p.Clips)
                .HasForeignKey(c => c.PuzzleId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => new { c.PuzzleId, c.Step }).IsUnique();
        });
    }
}
