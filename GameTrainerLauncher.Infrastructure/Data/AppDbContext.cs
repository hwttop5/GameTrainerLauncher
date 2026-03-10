using GameTrainerLauncher.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameTrainerLauncher.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<Game> Games { get; set; } = null!;
    public DbSet<Trainer> Trainers { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var appPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        
        if (!System.IO.Directory.Exists(appPath))
        {
            System.IO.Directory.CreateDirectory(appPath);
        }

        var dbPath = System.IO.Path.Join(appPath, "game_trainer_launcher.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Trainer>()
            .Ignore(t => t.IsLoading)
            .Ignore(t => t.IsDownloading)
            .Ignore(t => t.DownloadProgress);
    }
}
