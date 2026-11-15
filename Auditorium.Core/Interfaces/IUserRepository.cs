using Auditorium.Core.Entities;
namespace Auditorium.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> FindByLoginAsync(string login, CancellationToken cancellationToken = default);
    Task<User?> FindByIdAsync(int id, CancellationToken cancellationToken = default);
    // false означает конфликт уникального логина, а не любую ошибку БД.
    Task<bool> TryAddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdatePasswordHashAsync(int id, string oldHash, string newHash, CancellationToken cancellationToken = default);
}
