using System.Text.RegularExpressions;
namespace Auditorium.Core.Authentication;

/// <summary>Общие правила сервиса и форм. Пароль не обрезаем и не меняем его регистр.</summary>
public static partial class AuthRules
{
    public const int LoginMaxLength = 100;
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;
    public const string LoginMessage = "Логин: 3–100 символов — латинские буквы, цифры, точка, дефис или подчёркивание.";
    public const string PasswordMessage = "Пароль: 8–128 символов, не только пробелы и без управляющих символов.";
    public static string NormalizeLogin(string? login) => (login ?? "").Trim().ToLowerInvariant();
    public static bool IsValidLogin(string? login) => LoginPattern().IsMatch(NormalizeLogin(login));
    public static bool IsValidPassword(string? password) =>
        password is { Length: >= PasswordMinLength and <= PasswordMaxLength } &&
        !string.IsNullOrWhiteSpace(password) && !password.Any(char.IsControl);
    // ASCII-логины одинаково сравниваются без учёта регистра в .NET и SQLite NOCASE.
    [GeneratedRegex(@"\A[a-z0-9_.-]{3,100}\z", RegexOptions.CultureInvariant)]
    private static partial Regex LoginPattern();
}
