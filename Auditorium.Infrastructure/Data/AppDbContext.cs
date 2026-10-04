using Auditorium.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auditorium.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Lesson> Lessons { get; set; }
        public DbSet<Audience> Audiences { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Lesson>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Subject).HasMaxLength(500);
                entity.Property(e => e.TeacherFull).HasMaxLength(200);
                entity.Property(e => e.RoomName).HasMaxLength(100);
                entity.Property(e => e.GroupName).HasMaxLength(50);
                entity.Property(e => e.Semester).HasMaxLength(100);

                // —в€зь: много зан€тий Ч одна аудитори€.
                entity.HasOne<Audience>()
                      .WithMany(a => a.Lessons)
                      .HasForeignKey(l => l.AudienceId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Audience>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(100);
            });
        }
    }
}