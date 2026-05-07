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
using FireflyFramework.Ecm.ESignature.AdobeSign;
using Microsoft.Extensions.Options;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// WireMock-driven tests for <see cref="AdobeSignSignatureEnvelopeAdapter"/>. Stand up a fake
/// <c>api.na1.adobesign.com</c>, point the adapter at it, and verify (a) the OAuth2
/// refresh-token flow that gates every API call, (b) the agreement create / read / update /
/// send / cancel verbs, and (c) the status mapping between Adobe Sign and
/// <see cref="SignatureEnvelopeStatus"/>.
/// </summary>
public sealed class AdobeSignSignatureEnvelopeAdapterTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly AdobeSignSignatureEnvelopeAdapter _adapter;

    public AdobeSignSignatureEnvelopeAdapterTests()
    {
        _server = WireMockServer.Start();

        // OAuth2 refresh-token mock — every API call in the adapter goes through EnsureAuthAsync first.
        _server.Given(Request.Create().UsingPost().WithPath("/oauth/v2/refresh"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"access_token":"adobe-tok","expires_in":3600}"""));

        var options = Options.Create(new AdobeSignOptions
        {
            ClientId = "cid",
            ClientSecret = "csecret",
            RefreshToken = "rtok",
            BaseUrl = _server.Urls[0],
            ApiVersion = "/api/rest/v6",
        });
        _adapter = new AdobeSignSignatureEnvelopeAdapter(new HttpClient(), options);
    }

    private static SignatureEnvelope NewEnvelope() => new(
        Id: Guid.NewGuid(),
        Name: "Loan agreement",
        DocumentIds: new[] { Guid.NewGuid() },
        Signers: new[]
        {
            new SignatureRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "alice@example.com", "Alice", SignatureRequestStatus.Pending, DateTimeOffset.UtcNow),
        },
        Status: SignatureEnvelopeStatus.Draft,
        Provider: "adobe-sign",
        CreatedAt: DateTimeOffset.UtcNow);

    [Fact]
    public async Task CreateEnvelopeAsync_RefreshesToken_AndReturnsProviderId()
    {
        _server.Given(Request.Create()
                .UsingPost()
                .WithPath("/api/rest/v6/agreements")
                .WithHeader("Authorization", "Bearer adobe-tok"))
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"id":"AGR-1"}"""));

        var created = await _adapter.CreateEnvelopeAsync(NewEnvelope(), CancellationToken.None);

        Assert.Equal(SignatureEnvelopeStatus.Draft, created.Status);
        Assert.Equal("adobe-sign:AGR-1", created.Provider);
    }

    [Fact]
    public async Task GetEnvelopeAsync_ParsesStatusFromAdobeResponse()
    {
        var id = Guid.NewGuid();
        _server.Given(Request.Create()
                .UsingGet()
                .WithPath($"/api/rest/v6/agreements/{id}"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"name":"Loan","status":"OUT_FOR_SIGNATURE"}"""));

        var got = await _adapter.GetEnvelopeAsync(id, CancellationToken.None);

        Assert.NotNull(got);
        Assert.Equal("Loan", got!.Name);
        Assert.Equal(SignatureEnvelopeStatus.Sent, got.Status);
    }

    [Fact]
    public async Task GetEnvelopeAsync_NotFound_ReturnsNull()
    {
        var id = Guid.NewGuid();
        _server.Given(Request.Create()
                .UsingGet()
                .WithPath($"/api/rest/v6/agreements/{id}"))
            .RespondWith(Response.Create().WithStatusCode(404));

        Assert.Null(await _adapter.GetEnvelopeAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task SendEnvelopeAsync_PutsState_IN_PROCESS()
    {
        var id = Guid.NewGuid();
        _server.Given(Request.Create()
                .UsingPut()
                .WithPath($"/api/rest/v6/agreements/{id}/state")
                .WithBody(b => b?.Contains("\"IN_PROCESS\"") == true))
            .RespondWith(Response.Create().WithStatusCode(200));

        await _adapter.SendEnvelopeAsync(id, "alice", CancellationToken.None);
    }

    [Fact]
    public async Task VoidEnvelopeAsync_PutsState_CANCELLED_WithReason()
    {
        var id = Guid.NewGuid();
        _server.Given(Request.Create()
                .UsingPut()
                .WithPath($"/api/rest/v6/agreements/{id}/state")
                .WithBody(b => b?.Contains("\"CANCELLED\"") == true && b.Contains("policy violation")))
            .RespondWith(Response.Create().WithStatusCode(200));

        await _adapter.VoidEnvelopeAsync(id, "policy violation", null, CancellationToken.None);
    }

    [Fact]
    public void EcmAdapter_AttributeReports_ESignatureCapabilities()
    {
        var info = AdapterIntrospection.GetInfo(typeof(AdobeSignSignatureEnvelopeAdapter));

        Assert.NotNull(info);
        Assert.Equal("adobe-sign", info!.Type);
        Assert.True(info.SupportedFeatures.HasFlag(AdapterFeature.ESignatureEnvelopes));
        Assert.True(info.SupportedFeatures.HasFlag(AdapterFeature.SignatureValidation));
    }

    public void Dispose() => _server.Dispose();
}
