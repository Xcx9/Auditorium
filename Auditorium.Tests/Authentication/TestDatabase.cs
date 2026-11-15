using Auditorium.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Auditorium.Tests.Authentication;

/// <summary>Каждый тест получает отдельный SQLite-файл, без внешних сайтов и рабочей БД.</summary>
public sealed class TestDatabase : IDisposable
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "Auditorium.Auth.Tests", Guid.NewGuid().ToString("N"));
    public string ConnectionString => $"Data Source={Path.Combine(DirectoryPath, "test.db")};Pooling=False;Default Timeout=15";
    public TestDatabase() => Directory.CreateDirectory(DirectoryPath);
    public AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(ConnectionString).Options);
    public void Dispose()
    {
        // WebApplicationFactory может вызвать освобождение повторно.
        if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, recursive: true);
    }
}
