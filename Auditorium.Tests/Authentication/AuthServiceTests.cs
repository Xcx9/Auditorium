using NUnit.Framework;
using NUnit.Framework.Legacy;
using Auditorium.Core.Authentication;
using Auditorium.Core.Entities;
using Auditorium.Core.Interfaces;
using Auditorium.Core.Services;
using Auditorium.Infrastructure.Data;
using Auditorium.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
namespace Auditorium.Tests.Authentication;

public sealed class AuthServiceTests
{
    private static readonly IdentityPasswordHasher Hasher = new();

    [Test]
    public async Task Register_persists_hash_and_login_returns_real_id()
    {
        using var database = new TestDatabase();
        await using var db = database.Open();
        await db.Database.MigrateAsync();
        var auth = new AuthService(new UserRepository(db), Hasher);
        var registration = await auth.RegisterAsync("  Alice  ", "correct horse");
        ClassicAssert.IsTrue(registration.Succeeded);
        ClassicAssert.IsTrue(registration.User!.Id > 0);
        var stored = await db.Users.SingleAsync();
        ClassicAssert.AreEqual("alice", stored.Login);
        ClassicAssert.AreNotEqual("correct horse", stored.PasswordHash);
        ClassicAssert.AreEqual(PasswordCheck.Success, Hasher.Verify(stored.PasswordHash, "correct horse"));
        var login = await auth.LoginAsync("ALICE", "correct horse");
        ClassicAssert.AreEqual(registration.User, login.User);
        ClassicAssert.AreEqual(AuthError.InvalidCredentials, (await auth.LoginAsync("alice", "wrong")).Error);
        ClassicAssert.AreEqual(AuthError.InvalidCredentials, (await auth.LoginAsync("missing", "wrong")).Error);
    }

    [TestCase("", "password1", AuthError.InvalidLogin)]
    [TestCase("ab", "password1", AuthError.InvalidLogin)]
    [TestCase("bad login", "password1", AuthError.InvalidLogin)]
    [TestCase("пользователь", "password1", AuthError.InvalidLogin)]
    [TestCase("alice", "short", AuthError.InvalidPassword)]
    [TestCase("alice", "        ", AuthError.InvalidPassword)]
    [TestCase("alice", "long\npassword", AuthError.InvalidPassword)]
    public async Task Invalid_registration_does_not_write(string login, string password, AuthError error)
    {
        using var database = new TestDatabase();
        await using var db = database.Open();
        await db.Database.MigrateAsync();
        ClassicAssert.AreEqual(error, (await new AuthService(new UserRepository(db), Hasher).RegisterAsync(login, password)).Error);
        ClassicAssert.IsEmpty(await db.Users.ToListAsync());
    }

    [Test]
    public void Input_boundaries_and_password_whitespace_are_explicit()
    {
        ClassicAssert.IsTrue(AuthRules.IsValidLogin(new string('a', 100)));
        ClassicAssert.IsFalse(AuthRules.IsValidLogin(new string('a', 101)));
        ClassicAssert.IsTrue(AuthRules.IsValidPassword(new string('a', 8)));
        ClassicAssert.IsTrue(AuthRules.IsValidPassword(new string('a', 128)));
        ClassicAssert.IsFalse(AuthRules.IsValidPassword(new string('a', 129)));
        var hash = Hasher.Hash(" password ");
        ClassicAssert.AreEqual(PasswordCheck.Success, Hasher.Verify(hash, " password "));
        ClassicAssert.AreEqual(PasswordCheck.Failed, Hasher.Verify(hash, "password"));
        ClassicAssert.AreNotEqual(hash, Hasher.Hash(" password "));
    }

    [TestCase((string?)null)]
    [TestCase("")]
    [TestCase("not a hash")]
    [TestCase("AAAA")]
    public void Missing_or_malformed_hash_is_rejected(string? hash) =>
        ClassicAssert.AreEqual(PasswordCheck.Failed, Hasher.Verify(hash, "password1"));

    [Test]
    public async Task Duplicate_login_is_controlled_and_context_remains_usable()
    {
        using var database = new TestDatabase();
        await using var db = database.Open();
        await db.Database.MigrateAsync();
        var auth = new AuthService(new UserRepository(db), Hasher);
        ClassicAssert.IsTrue((await auth.RegisterAsync("alice", "password1")).Succeeded);
        ClassicAssert.AreEqual(AuthError.LoginTaken, (await auth.RegisterAsync("ALICE", "password2")).Error);
        ClassicAssert.IsTrue((await auth.RegisterAsync("bob", "password2")).Succeeded);
        ClassicAssert.AreEqual(2, await db.Users.CountAsync());
    }

    [Test]
    public async Task Database_index_itself_rejects_case_variant()
    {
        using var database = new TestDatabase();
        await using var db = database.Open();
        await db.Database.MigrateAsync();
        var repo = new UserRepository(db);
        ClassicAssert.IsTrue(await repo.TryAddAsync(new User("Alice", Hasher.Hash("password1"))));
        ClassicAssert.IsFalse(await repo.TryAddAsync(new User("ALICE", Hasher.Hash("password1"))));
    }

    [Test]
    public async Task Parallel_registration_uses_independent_connections_and_has_one_winner()
    {
        using var database = new TestDatabase();
        await using (var init = database.Open()) await init.Database.MigrateAsync();
        // Барьер одновременно выпускает два запроса с разными DbContext.
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int arrivals = 0;
        async Task<AuthResult> Register()
        {
            await using var db = database.Open();
            var repository = new BarrierRepository(new UserRepository(db), async () =>
            {
                if (Interlocked.Increment(ref arrivals) == 2) ready.SetResult();
                await ready.Task;
            });
            return await new AuthService(repository, Hasher).RegisterAsync("same.user", "password1");
        }
        var results = await Task.WhenAll(Task.Run(Register), Task.Run(Register));
        Assert.That(results.Where(result => result.Succeeded), Has.Exactly(1).Items);
        Assert.That(results.Where(result => result.Error == AuthError.LoginTaken), Has.Exactly(1).Items);
        await using var check = database.Open();
        ClassicAssert.AreEqual(1, await check.Users.CountAsync());
    }

    [Test]
    public async Task Login_upgrades_old_identity_hash()
    {
        using var database = new TestDatabase();
        await using var db = database.Open();
        await db.Database.MigrateAsync();
        var oldHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<object>(
            Microsoft.Extensions.Options.Options.Create(new Microsoft.AspNetCore.Identity.PasswordHasherOptions { IterationCount = 10_000 }));
        var oldHash = oldHasher.HashPassword(new object(), "password1");
        var repo = new UserRepository(db);
        await repo.TryAddAsync(new User("alice", oldHash));
        ClassicAssert.IsTrue((await new AuthService(repo, Hasher).LoginAsync("alice", "password1")).Succeeded);
        db.ChangeTracker.Clear();
        var hash = (await db.Users.SingleAsync()).PasswordHash;
        ClassicAssert.AreNotEqual(oldHash, hash);
        ClassicAssert.AreEqual(PasswordCheck.Success, Hasher.Verify(hash, "password1"));
    }

    private sealed class BarrierRepository(IUserRepository inner, Func<Task> barrier) : IUserRepository
    {
        public Task<User?> FindByLoginAsync(string login, CancellationToken ct = default) => inner.FindByLoginAsync(login, ct);
        public Task<User?> FindByIdAsync(int id, CancellationToken ct = default) => inner.FindByIdAsync(id, ct);
        public async Task<bool> TryAddAsync(User user, CancellationToken ct = default)
        { await barrier(); return await inner.TryAddAsync(user, ct); }
        public Task UpdatePasswordHashAsync(int id, string oldHash, string newHash, CancellationToken ct = default) =>
            inner.UpdatePasswordHashAsync(id, oldHash, newHash, ct);
    }
}
