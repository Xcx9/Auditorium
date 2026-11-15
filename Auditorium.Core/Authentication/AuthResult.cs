namespace Auditorium.Core.Authentication;
public enum AuthError { None, InvalidLogin, InvalidPassword, LoginTaken, InvalidCredentials }
// Web получает только необходимые поля, без хеша пароля.
public sealed record AuthUser(int Id, string Login);
public sealed record AuthResult(AuthUser? User, AuthError Error)
{
    public bool Succeeded => Error == AuthError.None && User is not null;
    public static AuthResult Failure(AuthError error) => new(null, error);
}
