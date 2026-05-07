using FireflyFramework.Idp;
using FireflyFramework.Idp.AzureAd;
using Microsoft.Extensions.Options;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// MSAL is a public-cloud client that cannot be safely exercised in-process. These
/// tests pin the documented upstream-limitation behaviour of <see cref="AzureAdIdpAdapter"/>
/// — every <see cref="NotSupportedException"/> path the adapter advertises in its
/// XML doc and the README.
/// </summary>
public sealed class AzureAdIdpAdapterTests
{
    private static AzureAdIdpAdapter NewAdapter() => new(
        Options.Create(new AzureAdOptions
        {
            TenantId = "tenant-1",
            ClientId = "client-1",
            Authority = "https://login.microsoftonline.com",
            Scopes = new[] { "user.read" },
        }),
        admin: null);

    [Fact]
    public async Task RefreshAsync_DocumentsMsalSilentTokenCache()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            NewAdapter().RefreshAsync(new RefreshRequest("ref"), CancellationToken.None));
        Assert.Contains("MSAL", ex.Message);
        Assert.Contains("AcquireTokenSilentAsync", ex.Message);
    }

    [Fact]
    public async Task GetUserInfoAsync_DocumentsTokenClaimDecoding()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            NewAdapter().GetUserInfoAsync("acc", CancellationToken.None));
        Assert.Contains("oid", ex.Message);
    }

    [Fact]
    public async Task MfaVerifyAsync_DocumentsAuthCodeFlow()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            NewAdapter().MfaVerifyAsync(new MfaVerifyRequest("challenge-id", "123456"), CancellationToken.None));
        Assert.Contains("MFA", ex.Message);
    }

    [Fact]
    public async Task ListSessionsAsync_DocumentsAuditLogsRoute()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            NewAdapter().ListSessionsAsync("u", CancellationToken.None));
        Assert.Contains("auditLogs", ex.Message);
    }

    [Fact]
    public async Task CreateScopeAsync_DocumentsAppRegistrationLimitation()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            NewAdapter().CreateScopeAsync(new CreateScopeRequest("s", null), CancellationToken.None));
        Assert.Contains("app registration", ex.Message);
    }

    [Fact]
    public async Task AdminOperations_WithoutGraphAdmin_ThrowInvalidOperation()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewAdapter().DeleteUserAsync("u", CancellationToken.None));
    }
}
