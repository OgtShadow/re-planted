using Microsoft.EntityFrameworkCore;
using RePlanted.Server.Models;

namespace RePlanted.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ActuatorDevice> ActuatorDevices { get; set; }
    public DbSet<Plant> Plants { get; set; }
    public DbSet<Parameters> Parameters { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<TelemetryBucket> TelemetryBuckets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Parameters>(entity =>
        {
            entity.OwnsOne(p => p.Humidity);
            entity.OwnsOne(p => p.Temperature);
        });

        modelBuilder.Entity<ActuatorDevice>(entity =>
        {
            entity.Property(d => d.Name).IsRequired();
            entity.Property(d => d.TargetParameter).IsRequired();
            entity.Property(d => d.EffectType).IsRequired();

            entity.HasOne(d => d.User)
                .WithMany(u => u.Devices)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(d => d.Plants)
                .WithMany(p => p.Devices)
                .UsingEntity<Dictionary<string, object>>(
                    "PlantActuatorDevice",
                    join => join
                        .HasOne<Plant>()
                        .WithMany()
                        .HasForeignKey("PlantId")
                        .OnDelete(DeleteBehavior.Cascade),
                    join => join
                        .HasOne<ActuatorDevice>()
                        .WithMany()
                        .HasForeignKey("ActuatorDeviceId")
                        .OnDelete(DeleteBehavior.Cascade),
                    join =>
                    {
                        join.HasKey("PlantId", "ActuatorDeviceId");
                        join.ToTable("PlantActuatorDevices");
                    });
        });

        modelBuilder.Entity<TelemetryBucket>(entity =>
        {
            entity.Property(x => x.DeviceId).HasMaxLength(120).IsRequired();
            entity.Property(x => x.BucketStartUtc).IsRequired();

            entity.HasIndex(x => new { x.DeviceId, x.BucketStartUtc }).IsUnique();
            entity.HasIndex(x => x.BucketStartUtc);
        });
    }
}
