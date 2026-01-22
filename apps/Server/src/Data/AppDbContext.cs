using Microsoft.EntityFrameworkCore;
using RePlanted.Server.Models;

namespace RePlanted.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Plant> Plants { get; set; }
    public DbSet<Parameters> Parameters { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Parameters>(entity =>
        {
            entity.OwnsOne(p => p.Humidity);
            entity.OwnsOne(p => p.Temperature);
        });
    }
}
