using FireflyFramework.Idp;
using FireflyFramework.Idp.Keycloak;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// WireMock-driven tests for <see cref="KeycloakIdpAdapter"/>. Stands up a local
/// WireMock server, points the adapter at it, and verifies the OIDC round-trips
/// (login / refresh / logout / introspect / userinfo) hit the right paths and
/// produce the right shapes from real JSON responses.
/// </summary>
public sealed class KeycloakIdpAdapterTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly KeycloakIdpAdapter _adapter;

    public KeycloakIdpAdapterTests()
    {
        _server = WireMockServer.Start();
        var options = Options.Create(new KeycloakOptions
        {
            ServerUrl = _server.Urls[0],
            Realm = "demo",
            ClientId = "orders",
            ClientSecret = "topsecret",
        });
        _adapter = new KeycloakIdpAdapter(
            new HttpClient(),
            options,
            NullLogger<KeycloakIdpAdapter>.Instance,
            admin: null);
    }

    [Fact]
    public async Task LoginAsync_PostsPasswordGrant_ReturnsToken()
    {
        _server.Given(Request.Create()
                .UsingPost()
                .WithPath("/realms/demo/protocol/openid-connect/token"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"access_token":"acc","refresh_token":"ref","token_type":"Bearer","expires_in":3600,"id_token":"idt"}"""));

        var resp = await _adapter.LoginAsync(new LoginRequest("alice", "pwd"), CancellationToken.None);

        Assert.Equal("acc", resp.AccessToken);
        Assert.Equal("ref", resp.RefreshToken);
        Assert.Equal("Bearer", resp.TokenType);
        Assert.Equal(3600, resp.ExpiresIn);
        Assert.Equal("idt", resp.IdToken);
    }

    [Fact]
    public async Task RefreshAsync_PostsRefreshTokenGrant_ReturnsNewToken()
    {
        _server.Given(Request.Create()
                .UsingPost()
                .WithPath("/realms/demo/protocol/openid-connect/token")
                .WithBody(b => b?.Contains("grant_type=refresh_token") == true))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"access_token":"newAcc","refresh_token":"newRef","token_type":"Bearer","expires_in":900}"""));

        var resp = await _adapter.RefreshAsync(new RefreshRequest("oldRef"), CancellationToken.None);

        Assert.Equal("newAcc", resp.AccessToken);
        Assert.Equal("newRef", resp.RefreshToken);
        Assert.Equal(900, resp.ExpiresIn);
    }

    [Fact]
    public async Task LogoutAsync_PostsRefreshTokenToLogoutEndpoint()
    {
        _server.Given(Request.Create()
                .UsingPost()
                .WithPath("/realms/demo/protocol/openid-connect/logout"))
            .RespondWith(Response.Create().WithStatusCode(204));

        await _adapter.LogoutAsync(new LogoutRequest("ref"), CancellationToken.None);

        var calls = _server.LogEntries
            .Where(e => e.RequestMessage.AbsolutePath.EndsWith("/logout"))
            .ToList();
        Assert.Single(calls);
    }

    [Fact]
    public async Task IntrospectAsync_ActiveTrue_ParsesUsernameAndRoles()
    {
        _server.Given(Request.Create()
                .UsingPost()
                .WithPath("/realms/demo/protocol/openid-connect/token/introspect"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"active":true,"username":"alice","sub":"u-1","realm_access":{"roles":["admin","user"]}}"""));

        var resp = await _adapter.IntrospectAsync("acc", CancellationToken.None);

        Assert.True(resp.Active);
        Assert.Equal("alice", resp.Username);
        Assert.Equal("u-1", resp.Sub);
        Assert.NotNull(resp.Roles);
        Assert.Contains("admin", resp.Roles!);
        Assert.Contains("user", resp.Roles!);
    }

    [Fact]
    public async Task IntrospectAsync_ActiveFalse_ReturnsInactive()
    {
        _server.Given(Request.Create()
                .UsingPost()
                .WithPath("/realms/demo/protocol/openid-connect/token/introspect"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"active":false}"""));

        var resp = await _adapter.IntrospectAsync("expired", CancellationToken.None);

        Assert.False(resp.Active);
    }

    [Fact]
    public async Task GetUserInfoAsync_BearerHeader_ParsesProfileFields()
    {
        _server.Given(Request.Create()
                .UsingGet()
                .WithPath("/realms/demo/protocol/openid-connect/userinfo")
                .WithHeader("Authorization", "Bearer acc"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"sub":"u-1","email":"a@x.com","given_name":"Alice","family_name":"Smith"}"""));

        var resp = await _adapter.GetUserInfoAsync("acc", CancellationToken.None);

        Assert.Equal("u-1", resp.Sub);
        Assert.Equal("a@x.com", resp.Email);
        Assert.Equal("Alice", resp.GivenName);
        Assert.Equal("Smith", resp.FamilyName);
    }

    [Fact]
    public async Task MfaVerifyAsync_DocumentsUpstreamLimitation()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            _adapter.MfaVerifyAsync(new MfaVerifyRequest("challenge-id", "123456"), CancellationToken.None));
        Assert.Contains("MfaCode", ex.Message);
    }

    [Fact]
    public async Task CreateScopeAsync_DocumentsUpstreamLimitation()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            _adapter.CreateScopeAsync(new CreateScopeRequest("scope", null), CancellationToken.None));
        Assert.Contains("realm", ex.Message);
    }

    [Fact]
    public async Task AdminOperations_WithoutAdminClient_ThrowInvalidOperation()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _adapter.DeleteUserAsync("u-1", CancellationToken.None));
    }

    public void Dispose() => _server.Dispose();
}
