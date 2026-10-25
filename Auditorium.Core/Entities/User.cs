namespace Auditorium.Core.Entities;

public class User
{
    private int _id;
    private string _login = string.Empty;
    private string _passwordHash = string.Empty;

    private User() { } // Äëÿ EF Core

    public User(string login, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(login))
            throw new ArgumentException("Login required.", nameof(login));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException(
                "passwordHash required.", nameof(passwordHash));

        _login = login;
        _passwordHash = passwordHash;
    }

    public int Id => _id;
    public string Login => _login;
    public string PasswordHash => _passwordHash;
    public string GetDisplayName() => Login;
}
