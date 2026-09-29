using Auditorium.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auditorium.Infrastructure.Data
{
    /// <summary>
    /// Контекст базы данных для работы с расписанием.
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Таблица занятий.
        public DbSet<Lesson> Lessons { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Настройка сущности Lesson.
            modelBuilder.Entity<Lesson>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Subject).HasMaxLength(500);
                entity.Property(e => e.TeacherFull).HasMaxLength(200);
                entity.Property(e => e.RoomName).HasMaxLength(100);
                entity.Property(e => e.GroupName).HasMaxLength(50);
            });
        }
    }
}