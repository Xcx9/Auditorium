using System.Security.Cryptography;
using Auditorium.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
namespace Auditorium.Infrastructure.Security;

/// <summary>Адаптер стандартного Microsoft PasswordHasher: алгоритм и соль не пишем вручную.</summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new(Options.Create(new PasswordHasherOptions
    {
        CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
        IterationCount = 210_000
    }));
    private readonly object _context = new();
    private readonly string _dummyHash;
    public IdentityPasswordHasher() =>
        _dummyHash = _hasher.HashPassword(_context, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    public string Hash(string password) => _hasher.HashPassword(_context, password);
    public PasswordCheck Verify(string? hash, string password)
    {
        try
        {
            var result = _hasher.VerifyHashedPassword(_context, hash ?? _dummyHash, password);
            if (hash is null) return PasswordCheck.Failed;
            return result switch
            {
                PasswordVerificationResult.Success => PasswordCheck.Success,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.RehashNeeded,
                _ => PasswordCheck.Failed
            };
        }
        catch (FormatException) { return PasswordCheck.Failed; }
        catch (ArgumentException) { return PasswordCheck.Failed; }
    }
}
