using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using FireflyFramework.Ecm.Adapters;
using FireflyFramework.Ecm.Domain;
using FireflyFramework.Ecm.Ports;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FireflyFramework.Ecm.ESignature.DocuSign;

public sealed class DocuSignOptions
{
    public const string SectionName = "Firefly:Ecm:ESignature:DocuSign";
    public string IntegrationKey { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://demo.docusign.net/restapi";
    public string AuthServer { get; set; } = "account-d.docusign.com";
    public bool SandboxMode { get; set; } = true;
    public string? ReturnUrl { get; set; }
}

/// <summary>
/// DocuSign envelope adapter using JWT-grant authentication and the DocuSign REST v2.1 API.
/// Mirrors Java <c>DocuSignSignatureEnvelopeAdapter</c>.
/// </summary>
[EcmAdapter("docusign",
    Description = "DocuSign eSignature Envelope Adapter",
    SupportedFeatures = AdapterFeature.ESignatureEnvelopes | AdapterFeature.ESignatureRequests | AdapterFeature.SignatureValidation,
    RequiredProperties = new[] { "IntegrationKey", "UserId", "AccountId", "PrivateKey" },
    OptionalProperties = new[] { "BaseUrl", "AuthServer", "SandboxMode", "ReturnUrl" })]
public sealed class DocuSignSignatureEnvelopeAdapter : ISignatureEnvelopePort
{
    private readonly HttpClient _http;
    private readonly DocuSignOptions _opt;
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt;

    public DocuSignSignatureEnvelopeAdapter(HttpClient http, IOptions<DocuSignOptions> options)
    {
        _http = http;
        _opt = options.Value;
    }

    public async Task<SignatureEnvelope> CreateEnvelopeAsync(SignatureEnvelope envelope, CancellationToken ct = default)
    {
        await EnsureAuthAsync(ct).ConfigureAwait(false);
        var payload = new
        {
            emailSubject = envelope.Name,
            status = "created",
            recipients = new
            {
                signers = envelope.Signers.Select((s, i) => new
                {
                    email = s.SignerEmail,
                    name = s.SignerName,
                    recipientId = (i + 1).ToString(),
                    routingOrder = (i + 1).ToString(),
                }).ToArray(),
            },
        };

        using var resp = await _http.PostAsJsonAsync(EnvelopesUrl(), payload, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
        var docusignId = doc.GetProperty("envelopeId").GetString()!;
        return envelope with { Status = SignatureEnvelopeStatus.Draft, Provider = $"docusign:{docusignId}" };
    }

    public async Task<SignatureEnvelope?> GetEnvelopeAsync(Guid envelopeId, CancellationToken ct = default)
    {
        await EnsureAuthAsync(ct).ConfigureAwait(false);
        using var resp = await _http.GetAsync($"{EnvelopesUrl()}/{envelopeId}", ct).ConfigureAwait(false);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
        return new SignatureEnvelope(envelopeId,
            doc.GetProperty("emailSubject").GetString() ?? string.Empty,
            Array.Empty<Guid>(),
            Array.Empty<SignatureRequest>(),
            ParseStatus(doc.GetProperty("status").GetString()),
            "docusign",
            doc.GetProperty("createdDateTime").GetDateTimeOffset());
    }

    public async Task<SignatureEnvelope> UpdateEnvelopeAsync(SignatureEnvelope envelope, CancellationToken ct = default)
    {
        await EnsureAuthAsync(ct).ConfigureAwait(false);
        using var resp = await _http.PutAsJsonAsync($"{EnvelopesUrl()}/{envelope.Id}",
            new { emailSubject = envelope.Name }, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return envelope;
    }

    public async Task SendEnvelopeAsync(Guid envelopeId, string? sentBy, CancellationToken ct = default)
    {
        await EnsureAuthAsync(ct).ConfigureAwait(false);
        using var resp = await _http.PutAsJsonAsync($"{EnvelopesUrl()}/{envelopeId}",
            new { status = "sent" }, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    public async Task VoidEnvelopeAsync(Guid envelopeId, string reason, string? voidedBy, CancellationToken ct = default)
    {
        await EnsureAuthAsync(ct).ConfigureAwait(false);
        using var resp = await _http.PutAsJsonAsync($"{EnvelopesUrl()}/{envelopeId}",
            new { status = "voided", voidedReason = reason }, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    public Task CancelEnvelopeAsync(Guid envelopeId, CancellationToken ct = default) =>
        VoidEnvelopeAsync(envelopeId, "Cancelled", null, ct);

    public async Task<IReadOnlyList<SignatureEnvelope>> GetEnvelopesByStatusAsync(SignatureEnvelopeStatus status, CancellationToken ct = default)
    {
        await EnsureAuthAsync(ct).ConfigureAwait(false);
        using var resp = await _http.GetAsync($"{EnvelopesUrl()}?status={MapStatus(status)}", ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
        var list = new List<SignatureEnvelope>();
        if (doc.TryGetProperty("envelopes", out var envelopes))
        {
            foreach (var e in envelopes.EnumerateArray())
            {
                list.Add(new SignatureEnvelope(
                    Guid.Parse(e.GetProperty("envelopeId").GetString()!),
                    e.GetProperty("emailSubject").GetString() ?? string.Empty,
                    Array.Empty<Guid>(),
                    Array.Empty<SignatureRequest>(),
                    ParseStatus(e.GetProperty("status").GetString()),
                    "docusign",
                    e.GetProperty("createdDateTime").GetDateTimeOffset()));
            }
        }

        return list;
    }

    private string EnvelopesUrl() => $"{_opt.BaseUrl.TrimEnd('/')}/v2.1/accounts/{_opt.AccountId}/envelopes";

    private async Task EnsureAuthAsync(CancellationToken ct)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt - TimeSpan.FromSeconds(30))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken);
            return;
        }

        using var rsa = RSA.Create();
        rsa.ImportFromPem(_opt.PrivateKey);
        var creds = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        var now = DateTime.UtcNow;
        var jwt = new JwtSecurityToken(
            issuer: _opt.IntegrationKey,
            audience: _opt.AuthServer,
            claims: new[]
            {
                new Claim("sub", _opt.UserId),
                new Claim("scope", "signature impersonation"),
            },
            notBefore: now,
            expires: now.AddHours(1),
            signingCredentials: creds);

        var assertion = new JwtSecurityTokenHandler().WriteToken(jwt);
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = assertion,
        };

        using var resp = await _http.PostAsync($"https://{_opt.AuthServer}/oauth/token",
            new FormUrlEncodedContent(form), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
        _cachedToken = doc.GetProperty("access_token").GetString();
        _tokenExpiresAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(doc.GetProperty("expires_in").GetInt32());
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken);
    }

    private static SignatureEnvelopeStatus ParseStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "created" => SignatureEnvelopeStatus.Draft,
        "sent" => SignatureEnvelopeStatus.Sent,
        "delivered" => SignatureEnvelopeStatus.Sent,
        "completed" => SignatureEnvelopeStatus.Completed,
        "signed" => SignatureEnvelopeStatus.Signed,
        "voided" => SignatureEnvelopeStatus.Voided,
        "declined" => SignatureEnvelopeStatus.Cancelled,
        _ => SignatureEnvelopeStatus.Draft,
    };

    private static string MapStatus(SignatureEnvelopeStatus status) => status switch
    {
        SignatureEnvelopeStatus.Draft => "created",
        SignatureEnvelopeStatus.Sent => "sent",
        SignatureEnvelopeStatus.Completed => "completed",
        SignatureEnvelopeStatus.Signed => "signed",
        SignatureEnvelopeStatus.Voided => "voided",
        SignatureEnvelopeStatus.Cancelled => "declined",
        _ => "any",
    };
}
