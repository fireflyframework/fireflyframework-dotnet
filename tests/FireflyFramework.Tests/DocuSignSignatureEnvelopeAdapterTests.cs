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

using System.Security.Cryptography;
using FireflyFramework.Ecm.Adapters;
using FireflyFramework.Ecm.Domain;
using FireflyFramework.Ecm.ESignature.DocuSign;
using Microsoft.Extensions.Options;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// WireMock-driven tests for <see cref="DocuSignSignatureEnvelopeAdapter"/>. The DocuSign
/// adapter authenticates via JWT-Bearer (signed with a private RSA key) and then talks REST
/// v2.1. Because both flows share the same <see cref="HttpClient"/>, we point the client at
/// a single WireMock server and stub both <c>/oauth/token</c> and the envelopes endpoints.
///
/// A throw-away RSA key is generated per test run so the adapter's <c>RSA.ImportFromPem</c>
/// path is exercised; the JWT itself is treated as opaque by WireMock.
/// </summary>
public sealed class DocuSignSignatureEnvelopeAdapterTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly DocuSignSignatureEnvelopeAdapter _adapter;
    private readonly Guid _accountId = Guid.NewGuid();

    public DocuSignSignatureEnvelopeAdapterTests()
    {
        _server = WireMockServer.Start();

        // JWT-bearer token endpoint — production uses https://{AuthServer}/oauth/token, but for
        // testing we override the full URL via AuthTokenUrl so the adapter hits our WireMock
        // instead of attempting a TLS handshake.
        _server.Given(Request.Create().UsingPost().WithPath("/oauth/token"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"access_token":"docusign-tok","expires_in":3600}"""));

        var options = Options.Create(new DocuSignOptions
        {
            IntegrationKey = "intkey",
            UserId = "user-1",
            AccountId = _accountId.ToString(),
            PrivateKey = NewPemKey(),
            BaseUrl = _server.Urls[0] + "/restapi",
            AuthTokenUrl = _server.Urls[0] + "/oauth/token",
            SandboxMode = true,
        });
        _adapter = new DocuSignSignatureEnvelopeAdapter(new HttpClient(), options);
    }

    private static string NewPemKey()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportRSAPrivateKeyPem();
    }

    private SignatureEnvelope NewEnvelope() => new(
        Id: Guid.NewGuid(),
        Name: "Loan",
        DocumentIds: new[] { Guid.NewGuid() },
        Signers: new[]
        {
            new SignatureRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "alice@example.com", "Alice", SignatureRequestStatus.Pending, DateTimeOffset.UtcNow),
        },
        Status: SignatureEnvelopeStatus.Draft,
        Provider: "docusign",
        CreatedAt: DateTimeOffset.UtcNow);

    [Fact]
    public async Task CreateEnvelopeAsync_AcquiresJwtToken_AndPostsEnvelope()
    {
        _server.Given(Request.Create()
                .UsingPost()
                .WithPath($"/restapi/v2.1/accounts/{_accountId}/envelopes")
                .WithHeader("Authorization", "Bearer docusign-tok"))
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"envelopeId":"ENV-1","status":"created"}"""));

        var created = await _adapter.CreateEnvelopeAsync(NewEnvelope(), CancellationToken.None);

        Assert.Equal(SignatureEnvelopeStatus.Draft, created.Status);
        Assert.Equal("docusign:ENV-1", created.Provider);
    }

    [Fact]
    public async Task GetEnvelopeAsync_ParsesStatusAndCreatedDate()
    {
        var envelopeId = Guid.NewGuid();
        _server.Given(Request.Create()
                .UsingGet()
                .WithPath($"/restapi/v2.1/accounts/{_accountId}/envelopes/{envelopeId}"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"emailSubject":"Loan","status":"completed","createdDateTime":"2026-01-15T10:00:00Z"}"""));

        var got = await _adapter.GetEnvelopeAsync(envelopeId, CancellationToken.None);

        Assert.NotNull(got);
        Assert.Equal("Loan", got!.Name);
        Assert.Equal(SignatureEnvelopeStatus.Completed, got.Status);
    }

    [Fact]
    public async Task SendEnvelopeAsync_PutsStatus_sent()
    {
        var envelopeId = Guid.NewGuid();
        _server.Given(Request.Create()
                .UsingPut()
                .WithPath($"/restapi/v2.1/accounts/{_accountId}/envelopes/{envelopeId}")
                .WithBody(b => b?.Contains("\"sent\"") == true))
            .RespondWith(Response.Create().WithStatusCode(200));

        await _adapter.SendEnvelopeAsync(envelopeId, null, CancellationToken.None);
    }

    [Fact]
    public async Task VoidEnvelopeAsync_PutsVoidedStatus_AndReason()
    {
        var envelopeId = Guid.NewGuid();
        _server.Given(Request.Create()
                .UsingPut()
                .WithPath($"/restapi/v2.1/accounts/{_accountId}/envelopes/{envelopeId}")
                .WithBody(b => b?.Contains("\"voided\"") == true && b.Contains("\"voidedReason\"")))
            .RespondWith(Response.Create().WithStatusCode(200));

        await _adapter.VoidEnvelopeAsync(envelopeId, "duplicate", null, CancellationToken.None);
    }

    [Fact]
    public void EcmAdapter_AttributeReports_DocuSignCapabilities()
    {
        var info = AdapterIntrospection.GetInfo(typeof(DocuSignSignatureEnvelopeAdapter));

        Assert.NotNull(info);
        Assert.Equal("docusign", info!.Type);
        Assert.True(info.SupportedFeatures.HasFlag(AdapterFeature.ESignatureEnvelopes));
    }

    public void Dispose() => _server.Dispose();
}
