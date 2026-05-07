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

using FireflyFramework.Client.GraphQL;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// WireMock-driven tests for <see cref="GraphQLClient"/>. Stand up a fake GraphQL endpoint,
/// post the wire-format <c>{query, variables, operationName}</c> envelope, and assert both
/// happy-path data extraction and the GraphQL <c>errors</c> array surfacing.
/// </summary>
public sealed class GraphQLClientTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly HttpClient _http;
    private readonly GraphQLClient _client;

    public GraphQLClientTests()
    {
        _server = WireMockServer.Start();
        _http = new HttpClient { BaseAddress = new Uri($"{_server.Urls[0]}/graphql") };
        _client = new GraphQLClient(_http);
    }

    private sealed record User(string Id, string Name);
    private sealed record GetUserData(User User);

    [Fact]
    public async Task ExecuteAsync_HappyPath_ParsesData()
    {
        _server.Given(Request.Create()
                .UsingPost()
                .WithPath("/graphql")
                .WithBody(b => b?.Contains("getUser") == true && b.Contains("\"id\":\"42\"") == true))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"data":{"user":{"id":"42","name":"Alice"}}}"""));

        var resp = await _client.ExecuteAsync<GetUserData>(
            query: "query getUser($id: ID!) { user(id: $id) { id name } }",
            variables: new { id = "42" },
            operationName: "getUser");

        Assert.NotNull(resp.Data);
        Assert.Equal("42", resp.Data!.User.Id);
        Assert.Equal("Alice", resp.Data.User.Name);
        Assert.Null(resp.Errors);
    }

    [Fact]
    public async Task ExecuteAsync_ErrorsArray_SurfacesAlongsideData()
    {
        _server.Given(Request.Create().UsingPost().WithPath("/graphql"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"data":null,"errors":[{"message":"Field 'foo' not found","locations":[{"line":1,"column":10}]}]}"""));

        var resp = await _client.ExecuteAsync<GetUserData>("query { foo }");

        Assert.Null(resp.Data);
        Assert.True(resp.HasErrors);
        Assert.Single(resp.Errors!);
        Assert.Equal("Field 'foo' not found", resp.Errors![0].Message);
        var loc = Assert.Single(resp.Errors[0].Locations!);
        Assert.Equal(1, loc.Line);
        Assert.Equal(10, loc.Column);
    }

    [Fact]
    public async Task ExecuteOrThrowAsync_HappyPath_ReturnsData()
    {
        _server.Given(Request.Create().UsingPost().WithPath("/graphql"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"data":{"user":{"id":"1","name":"Bob"}}}"""));

        var data = await _client.ExecuteOrThrowAsync<GetUserData>("query { user { id name } }");
        Assert.Equal("Bob", data.User.Name);
    }

    [Fact]
    public async Task ExecuteOrThrowAsync_OnErrors_Throws()
    {
        _server.Given(Request.Create().UsingPost().WithPath("/graphql"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"errors":[{"message":"Unauthenticated"}]}"""));

        var ex = await Assert.ThrowsAsync<GraphQLException>(() => _client.ExecuteOrThrowAsync<GetUserData>("query {}"));
        Assert.Single(ex.Errors);
        Assert.Equal("Unauthenticated", ex.Errors[0].Message);
    }

    [Fact]
    public async Task ExecuteAsync_TransportFailure_PropagatesAsHttpRequestException()
    {
        _server.Given(Request.Create().UsingPost().WithPath("/graphql"))
            .RespondWith(Response.Create().WithStatusCode(503));

        await Assert.ThrowsAsync<HttpRequestException>(() => _client.ExecuteAsync<GetUserData>("query {}"));
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Dispose();
    }
}
