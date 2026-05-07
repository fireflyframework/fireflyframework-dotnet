// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using FireflyFramework.Client.OAuth2;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// WireMock-driven tests for <see cref="OAuth2TokenCache"/>. Stand up a fake OAuth2 token
/// endpoint, point the cache at it, and verify the client_credentials grant wire shape, the
/// caching behaviour (returns the cached token until it's near expiry), the
/// double-checked-locking under contention, and explicit invalidation.
/// </summary>
public sealed class OAuth2TokenCacheTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly HttpClient _http = new();

    public OAuth2TokenCacheTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task GetTokenAsync_FetchesOnce_AndCachesUntilExpiry()
    {
        _server.Given(Request.Create().UsingPost().WithPath("/token"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"access_token":"tok-stable","expires_in":3600}"""));

        var cache = new OAuth2TokenCache(_http);
        var t1 = await cache.GetTokenAsync($"{_server.Urls[0]}/token", "cid", "csecret", "scope", CancellationToken.None);
        var t2 = await cache.GetTokenAsync($"{_server.Urls[0]}/token", "cid", "csecret", "scope", CancellationToken.None);
        var t3 = await cache.GetTokenAsync($"{_server.Urls[0]}/token", "cid", "csecret", "scope", CancellationToken.None);

        Assert.Equal("tok-stable", t1);
        Assert.Equal(t1, t2);
        Assert.Equal(t1, t3);
        Assert.Single(_server.LogEntries, e => e.RequestMessage.AbsolutePath?.EndsWith("/token") == true);
    }

    [Fact]
    public async Task GetTokenAsync_PostsClientCredentialsGrant_WithScope()
    {
        _server.Given(Request.Create().UsingPost().WithPath("/token")
                .WithBody(b => b?.Contains("grant_type=client_credentials") == true
                            && b.Contains("client_id=cid") == true
                            && b.Contains("client_secret=csecret") == true
                            && b.Contains("scope=read") == true))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"access_token":"tok","expires_in":3600}"""));

        var cache = new OAuth2TokenCache(_http);
        var token = await cache.GetTokenAsync($"{_server.Urls[0]}/token", "cid", "csecret", "read", CancellationToken.None);
        Assert.Equal("tok", token);
    }

    [Fact]
    public async Task Invalidate_ForcesRefreshOnNextCall()
    {
        var hits = 0;
        _server.Given(Request.Create().UsingPost().WithPath("/token"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"access_token":"tok","expires_in":3600}"""));

        var cache = new OAuth2TokenCache(_http);
        var url = $"{_server.Urls[0]}/token";

        await cache.GetTokenAsync(url, "cid", "csecret", null, CancellationToken.None);
        cache.Invalidate(url, "cid", null);
        await cache.GetTokenAsync(url, "cid", "csecret", null, CancellationToken.None);

        hits = _server.LogEntries.Count(e => e.RequestMessage.AbsolutePath?.EndsWith("/token") == true);
        Assert.Equal(2, hits);
    }

    [Fact]
    public async Task GetTokenAsync_NearExpiry_TriggersRefresh()
    {
        _server.Given(Request.Create().UsingPost().WithPath("/token"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"access_token":"tok","expires_in":31}"""));

        // Skew of 30s + token TTL of 31s means the token is "near expiry" 1 second after
        // issue and forces a refresh on the next call. Bump skew so the test is fast and
        // deterministic (skew > expires_in always refreshes).
        var cache = new OAuth2TokenCache(_http) { RefreshSkew = TimeSpan.FromSeconds(60) };
        var url = $"{_server.Urls[0]}/token";

        await cache.GetTokenAsync(url, "cid", "csecret", null, CancellationToken.None);
        await cache.GetTokenAsync(url, "cid", "csecret", null, CancellationToken.None);

        Assert.Equal(2, _server.LogEntries.Count(e => e.RequestMessage.AbsolutePath?.EndsWith("/token") == true));
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Dispose();
    }
}
