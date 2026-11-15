using Auditorium.Core.Entities;
using Auditorium.Core.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
namespace Auditorium.Infrastructure.Data;

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> FindByLoginAsync(string login, CancellationToken cancellationToken = default) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Login == login, cancellationToken);
    public Task<User?> FindByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<bool> TryAddAsync(User user, CancellationToken cancellationToken = default)
    {
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 } &&
            exception.Entries.Count == 1 && exception.Entries[0].Entity == user)
        {
            // Единственный уникальный индекс Users — Login. Другие ошибки не скрываем.
            db.Entry(user).State = EntityState.Detached;
            return false;
        }
    }
    public async Task UpdatePasswordHashAsync(int id, string oldHash, string newHash,
        CancellationToken cancellationToken = default)
    {
        // Не перезаписываем хеш, если другой запрос уже успел изменить пароль.
        await db.Users.Where(u => u.Id == id && u.PasswordHash == oldHash)
            .ExecuteUpdateAsync(update => update.SetProperty(u => u.PasswordHash, newHash), cancellationToken);
    }
}
