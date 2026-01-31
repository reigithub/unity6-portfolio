using Game.Server.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<ScoreEntity> Scores => Set<ScoreEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DisplayName).IsUnique();
            entity.Property(e => e.DisplayName).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<ScoreEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.GameMode, e.StageId, e.Score })
                .IsDescending(false, false, true);
            entity.HasIndex(e => new { e.UserId, e.GameMode, e.StageId });

            entity.HasOne(e => e.User)
                .WithMany(u => u.Scores)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
