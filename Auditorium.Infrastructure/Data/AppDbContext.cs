using Auditorium.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Auditorium.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Lesson> ScheduleEntries => Set<Lesson>();
    public DbSet<OccupancyMark> OccupancyMarks => Set<OccupancyMark>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ScheduleImportState> ScheduleImportStates => Set<ScheduleImportState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SQLite сравнивает и сортирует UTC ticks как INTEGER.
        // Это сохраняет порядок моментов времени при разных исходных смещениях.
        var utcTicks = new ValueConverter<DateTimeOffset, long>(
            value => value.UtcTicks,
            value => new DateTimeOffset(value, TimeSpan.Zero));

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Id)
                .HasField("_room_id")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Property(r => r.Number)
                .HasField("_number")
                .HasMaxLength(20)
                .IsRequired()
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.HasIndex(r => r.Number);
            entity.HasIndex(r => r.SourceScheduleCode).IsUnique();
        });

        modelBuilder.Entity<ScheduleImportState>(entity =>
        {
            entity.ToTable("ScheduleImportState", table => table.HasCheckConstraint("CK_ScheduleImportState_Singleton", "Id = 1"));
            entity.Property(state => state.Id).ValueGeneratedNever();
            entity.Property(state => state.LastSuccessfulUpdate).HasConversion(utcTicks);
        });

        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.HasKey(s => s.Id);

            entity.Property(s => s.Id)
                .HasField("_id")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Property(s => s.RoomId)
                .HasField("_roomId")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Property(s => s.StartsAt)
                .HasField("_startsAt")
                .HasConversion(utcTicks)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Property(s => s.EndsAt)
                .HasField("_endsAt")
                .HasConversion(utcTicks)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.HasOne(s => s.Room)
                .WithMany()
                .HasForeignKey(s => s.RoomId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OccupancyMark>(entity =>
        {
            entity.HasKey(m => m.Id);

            entity.Property(m => m.Id).HasField("_id");
            entity.Property(m => m.RoomId).HasField("_roomId");
            entity.Property(m => m.UserId).HasField("_userId");
            entity.Property(m => m.CreatedAt).HasField("_createdAt").HasConversion(utcTicks);
            entity.Property(m => m.ClosedAt).HasField("_closedAt").HasConversion(utcTicks);
            entity.Property(m => m.AutoReleaseAt).HasField("_autoReleaseAt").HasConversion(utcTicks);

            // Закрытые отметки сохраняются в истории и не блокируют новые.
            entity.HasIndex(m => m.UserId).IsUnique().HasFilter("\"ClosedAt\" IS NULL");
            entity.HasIndex(m => m.RoomId).IsUnique().HasFilter("\"ClosedAt\" IS NULL");

            entity.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Room)
                .WithMany()
                .HasForeignKey(m => m.RoomId)
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
                .UseCollation("NOCASE")
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
