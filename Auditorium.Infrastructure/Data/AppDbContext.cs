
using Auditorium.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auditorium.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<ScheduleEntry> ScheduleEntries => Set<ScheduleEntry>();
    public DbSet<OccupancyMark> OccupancyMarks => Set<OccupancyMark>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Id)
                .HasField("_id")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Property(r => r.Number)
                .HasField("_number")
                .HasMaxLength(20)
                .IsRequired()
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Property(r => r.IsEnabled)
                .HasField("_isEnabled")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Property(r => r.State)
                .HasField("_state")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.HasIndex(r => r.Number).IsUnique();
        });

        modelBuilder.Entity<ScheduleEntry>(entity =>
        {
            entity.HasKey(s => s.Id);

            entity.Property(s => s.Id)
                .HasField("_id")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Property(s => s.RoomId)
                .HasField("_roomId")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Property(s => s.Start)
                .HasField("_start")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Property(s => s.End)
                .HasField("_end")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.HasOne(s => s.Room)
                .WithMany()
                .HasForeignKey(s => s.RoomId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OccupancyMark>(entity =>
        {
            entity.HasKey(m => m.Id);

            entity.Property(m => m.Id)
                .HasField("_id")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Property(m => m.RoomId)
                .HasField("_roomId")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Property(m => m.UserId)
                .HasField("_userId")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Property(m => m.CreatedAt)
                .HasField("_createdAt")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Property(m => m.IsCancelled)
                .HasField("_isCancelled")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.HasOne(m => m.Room)
                .WithMany()
                .HasForeignKey(m => m.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.Id)
                .HasField("_id")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Property(u => u.Login)
                .HasField("_login")
                .HasMaxLength(100)
                .IsRequired()
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Property(u => u.PasswordHash)
                .HasField("_passwordHash")
                .IsRequired()
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.HasIndex(u => u.Login).IsUnique();
        });
    }
}
