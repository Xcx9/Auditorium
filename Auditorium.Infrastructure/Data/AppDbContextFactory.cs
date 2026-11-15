using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Auditorium.Infrastructure.Data;

// Позволяет создавать миграции независимо от запуска веб-приложения.
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=auditorium.db")
            .Options;
        return new AppDbContext(options);
    }
}
