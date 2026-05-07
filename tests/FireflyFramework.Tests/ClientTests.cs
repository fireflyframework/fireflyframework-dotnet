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
