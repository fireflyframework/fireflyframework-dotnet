using System.Collections.Concurrent;
using FireflyFramework.Idp;
using FireflyFramework.Idp.InternalDb;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class InMemoryUserRepository : IInternalUserRepository
{
    private readonly ConcurrentDictionary<string, InternalUser> _byId = new();
    private readonly ConcurrentDictionary<string, InternalUser> _byUsername = new();

    public Task<InternalUser?> FindByUsernameAsync(string username, CancellationToken ct = default) =>
        Task.FromResult(_byUsername.TryGetValue(username, out var u) ? u : null);

    public Task<InternalUser?> FindByIdAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult(_byId.TryGetValue(userId, out var u) ? u : null);

    public Task<InternalUser> CreateAsync(InternalUser user, CancellationToken ct = default)
    {
        _byId[user.Id] = user;
        _byUsername[user.Username] = user;
        return Task.FromResult(user);
    }

    public Task UpdateAsync(InternalUser user, CancellationToken ct = default)
    {
        _byId[user.Id] = user;
        _byUsername[user.Username] = user;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string userId, CancellationToken ct = default)
    {
        if (_byId.TryRemove(userId, out var u))
        {
            _byUsername.TryRemove(u.Username, out _);
        }

        return Task.CompletedTask;
    }
}

public class InternalDbIdpTests
{
    private static InternalDbIdpAdapter Build(out InMemoryUserRepository repo)
    {
        repo = new InMemoryUserRepository();
        var options = Options.Create(new InternalDbOptions
        {
            Issuer = "firefly-tests",
            Audience = "firefly-tests",
            SigningKey = "test-signing-key-must-be-at-least-32-chars-long",
        });
        return new InternalDbIdpAdapter(repo, options);
    }

    [Fact]
    public async Task Create_then_login_returns_jwt_with_roles()
    {
        var idp = Build(out var repo);
        await idp.CreateUserAsync(new CreateUserRequest("alice", "alice@example.com", "Sup3r$ecret!", "Alice", null, new[] { "admin" }, null));
        var token = await idp.LoginAsync(new LoginRequest("alice", "Sup3r$ecret!"));
        token.AccessToken.Should().NotBeNullOrEmpty();

        var introspection = await idp.IntrospectAsync(token.AccessToken);
        introspection.Active.Should().BeTrue();
        introspection.Roles.Should().Contain("admin");
    }

    [Fact]
    public async Task Wrong_password_throws()
    {
        var idp = Build(out var repo);
        await idp.CreateUserAsync(new CreateUserRequest("bob", "bob@example.com", "Right!", null, null, null, null));
        await FluentActions.Invoking(() => idp.LoginAsync(new LoginRequest("bob", "wrong"))).Should()
            .ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Refresh_returns_a_fresh_access_token()
    {
        var idp = Build(out var _);
        await idp.CreateUserAsync(new CreateUserRequest("charlie", "c@example.com", "Pass!1234", null, null, null, null));
        var token = await idp.LoginAsync(new LoginRequest("charlie", "Pass!1234"));
        var refreshed = await idp.RefreshAsync(new RefreshRequest(token.RefreshToken!));
        refreshed.AccessToken.Should().NotBeNullOrEmpty();
        refreshed.AccessToken.Should().NotBe(token.AccessToken);
    }
}
