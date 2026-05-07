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

using FireflyFramework.Ecm.Adapters;
using FireflyFramework.Ecm.Domain;
using FireflyFramework.Ecm.ESignature.Logalty;
using Microsoft.Extensions.Options;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// WireMock-driven tests for <see cref="LogaltySignatureEnvelopeAdapter"/>. The adapter speaks
/// Logalty's REST API guarded by an OAuth2 client-credentials token. We stub both the token
/// endpoint and the envelope verbs and assert on the wire shape the adapter produces.
/// </summary>
public sealed class LogaltySignatureEnvelopeAdapterTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly LogaltySignatureEnvelopeAdapter _adapter;

    public LogaltySignatureEnvelopeAdapterTests()
    {
        _server = WireMockServer.Start();

        _server.Given(Request.Create().UsingPost().WithPath("/oauth/token")
                .WithBody(b => b?.Contains("grant_type=client_credentials") == true))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"access_token":"logalty-tok","expires_in":3600}"""));

        var options = Options.Create(new LogaltyOptions
        {
            ClientId = "cid",
            ClientSecret = "csecret",
            BaseUrl = _server.Urls[0],
        });
        _adapter = new LogaltySignatureEnvelopeAdapter(new HttpClient(), options);
    }

    private static SignatureEnvelope NewEnvelope() => new(
        Id: Guid.NewGuid(),
        Name: "Tenancy",
        DocumentIds: new[] { Guid.NewGuid() },
        Signers: new[]
        {
            new SignatureRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "bob@example.com", "Bob", SignatureRequestStatus.Pending, DateTimeOffset.UtcNow),
        },
        Status: SignatureEnvelopeStatus.Draft,
        Provider: "logalty",
        CreatedAt: DateTimeOffset.UtcNow);

    [Fact]
    public async Task CreateEnvelopeAsync_PostsProcess_AndReturnsProviderId()
    {
        _server.Given(Request.Create()
                .UsingPost()
                .WithPath("/v1/processes")
                .WithHeader("Authorization", "Bearer logalty-tok"))
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"id":"PROC-1"}"""));

        var created = await _adapter.CreateEnvelopeAsync(NewEnvelope(), CancellationToken.None);

        Assert.Equal(SignatureEnvelopeStatus.Draft, created.Status);
        Assert.Equal("logalty:PROC-1", created.Provider);
    }

    [Fact]
    public async Task GetEnvelopeAsync_ParsesStatus()
    {
        var id = Guid.NewGuid();
        _server.Given(Request.Create()
                .UsingGet()
                .WithPath($"/v1/processes/{id}"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"name":"Tenancy","status":"COMPLETED"}"""));

        var got = await _adapter.GetEnvelopeAsync(id, CancellationToken.None);

        Assert.NotNull(got);
        Assert.Equal(SignatureEnvelopeStatus.Completed, got!.Status);
    }

    [Fact]
    public async Task GetEnvelopeAsync_NotFound_ReturnsNull()
    {
        var id = Guid.NewGuid();
        _server.Given(Request.Create()
                .UsingGet()
                .WithPath($"/v1/processes/{id}"))
            .RespondWith(Response.Create().WithStatusCode(404));

        Assert.Null(await _adapter.GetEnvelopeAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task SendEnvelopeAsync_PostsStartEndpoint()
    {
        var id = Guid.NewGuid();
        _server.Given(Request.Create()
                .UsingPost()
                .WithPath($"/v1/processes/{id}/start"))
            .RespondWith(Response.Create().WithStatusCode(200));

        await _adapter.SendEnvelopeAsync(id, null, CancellationToken.None);
    }

    [Fact]
    public async Task VoidEnvelopeAsync_PostsCancelEndpoint_WithReason()
    {
        var id = Guid.NewGuid();
        _server.Given(Request.Create()
                .UsingPost()
                .WithPath($"/v1/processes/{id}/cancel")
                .WithBody(b => b?.Contains("\"signer never showed\"") == true))
            .RespondWith(Response.Create().WithStatusCode(200));

        await _adapter.VoidEnvelopeAsync(id, "signer never showed", null, CancellationToken.None);
    }

    [Fact]
    public async Task UpdateEnvelopeAsync_IsImmutable_NoCallMade()
    {
        // Logalty processes are immutable once created — UpdateEnvelopeAsync returns the input
        // without making a network call. Spin up a fresh server with no stubs to prove no HTTP
        // request is issued.
        var envelope = NewEnvelope();
        var updated = await _adapter.UpdateEnvelopeAsync(envelope, CancellationToken.None);

        Assert.Equal(envelope, updated);
        // No /v1/processes/{id} PUT request should ever have arrived.
        Assert.DoesNotContain(_server.LogEntries, e =>
            e.RequestMessage.AbsolutePath?.Contains("/v1/processes/" + envelope.Id) == true);
    }

    [Fact]
    public void EcmAdapter_AttributeReports_LogaltyCapabilities()
    {
        var info = AdapterIntrospection.GetInfo(typeof(LogaltySignatureEnvelopeAdapter));

        Assert.NotNull(info);
        Assert.Equal("logalty", info!.Type);
        Assert.True(info.SupportedFeatures.HasFlag(AdapterFeature.ESignatureEnvelopes));
    }

    public void Dispose() => _server.Dispose();
}
