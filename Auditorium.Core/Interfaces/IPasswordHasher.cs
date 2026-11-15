namespace Auditorium.Core.Interfaces;
public enum PasswordCheck { Failed, Success, RehashNeeded }
public interface IPasswordHasher
{
    string Hash(string password);
    // Для отсутствующего пользователя адаптер тоже выполняет дорогую проверку.
    PasswordCheck Verify(string? hash, string password);
}
