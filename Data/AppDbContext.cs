using MeBio.Models;
using Microsoft.EntityFrameworkCore;

namespace MeBio.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<FaceTemplate> FaceTemplates => Set<FaceTemplate>();
    public DbSet<FingerprintTemplate> FingerprintTemplates => Set<FingerprintTemplate>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();

    private readonly string _dbPath;

    public AppDbContext()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "mebio_v6.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.HasOne(u => u.FaceTemplate)
                .WithOne(f => f.User)
                .HasForeignKey<FaceTemplate>(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(u => u.FingerprintTemplate)
                .WithOne(f => f.User)
                .HasForeignKey<FingerprintTemplate>(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LoginAttempt>(e =>
        {
            e.HasOne(l => l.User)
                .WithMany(u => u.LoginAttempts)
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
