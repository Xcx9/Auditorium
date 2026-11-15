using Auditorium.Core.Authentication;
using Auditorium.Core.Entities;
using Auditorium.Core.Interfaces;
namespace Auditorium.Core.Services;

/// <summary>Регистрация и проверка паролей, без зависимости от HTTP и cookies.</summary>
public sealed class AuthService(IUserRepository users, IPasswordHasher passwords)
{
    public async Task<AuthResult> RegisterAsync(string? login, string? password,
        CancellationToken cancellationToken = default)
    {
        if (!AuthRules.IsValidLogin(login)) return AuthResult.Failure(AuthError.InvalidLogin);
        if (!AuthRules.IsValidPassword(password)) return AuthResult.Failure(AuthError.InvalidPassword);
        var user = new User(AuthRules.NormalizeLogin(login), passwords.Hash(password!));
        // Уникальный индекс защищает и от двух одновременных регистраций.
        if (!await users.TryAddAsync(user, cancellationToken))
            return AuthResult.Failure(AuthError.LoginTaken);
        return new(new AuthUser(user.Id, user.Login), AuthError.None);
    }

    public async Task<AuthResult> LoginAsync(string? login, string? password,
        CancellationToken cancellationToken = default)
    {
        // Старые корректные пароли проверяем независимо от новой минимальной длины.
        if (!AuthRules.IsValidLogin(login) || string.IsNullOrEmpty(password) ||
            password.Length > AuthRules.PasswordMaxLength)
            return AuthResult.Failure(AuthError.InvalidCredentials);
        var user = await users.FindByLoginAsync(AuthRules.NormalizeLogin(login), cancellationToken);
        var check = passwords.Verify(user?.PasswordHash, password);
        // Одна ошибка для неверного пароля и отсутствующего логина.
        if (user is null || check == PasswordCheck.Failed)
            return AuthResult.Failure(AuthError.InvalidCredentials);
        if (check == PasswordCheck.RehashNeeded)
            await users.UpdatePasswordHashAsync(user.Id, user.PasswordHash, passwords.Hash(password), cancellationToken);
        return new(new AuthUser(user.Id, user.Login), AuthError.None);
    }
}
