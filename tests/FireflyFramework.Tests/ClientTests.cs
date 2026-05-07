using FireflyFramework.Client;
using FireflyFramework.Client.Rest;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public class ClientTests
{
    [Fact]
    public void RestClientBuilder_constructs_HttpClient_with_base_url_and_default_header()
    {
        var http = ServiceClient.Rest()
            .WithBaseUrl("https://api.example.com/")
            .WithTimeout(TimeSpan.FromSeconds(5))
            .WithDefaultHeader("X-Tenant", "alpha")
            .Build();

        http.BaseAddress.Should().Be(new Uri("https://api.example.com/"));
        http.Timeout.Should().Be(TimeSpan.FromSeconds(5));
        http.DefaultRequestHeaders.GetValues("X-Tenant").Single().Should().Be("alpha");
    }

    [Fact]
    public void RestClientBuilder_applies_bearer_token()
    {
        var http = ServiceClient.Rest()
            .WithBaseUrl("https://api.example.com/")
            .WithAuth(a => { a.Scheme = AuthScheme.Bearer; a.BearerToken = "deadbeef"; })
            .Build();

        http.DefaultRequestHeaders.Authorization!.Scheme.Should().Be("Bearer");
        http.DefaultRequestHeaders.Authorization.Parameter.Should().Be("deadbeef");
    }

    [Fact]
    public void RestClientBuilder_applies_api_key_header()
    {
        var http = ServiceClient.Rest()
            .WithBaseUrl("https://api.example.com/")
            .WithAuth(a => { a.Scheme = AuthScheme.ApiKey; a.ApiKey = "abc"; a.ApiKeyHeader = "X-Api-Key"; })
            .Build();

        http.DefaultRequestHeaders.GetValues("X-Api-Key").Single().Should().Be("abc");
    }
}
