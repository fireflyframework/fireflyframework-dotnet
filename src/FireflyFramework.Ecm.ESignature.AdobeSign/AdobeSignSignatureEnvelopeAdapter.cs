using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FireflyFramework.Ecm.Adapters;
using FireflyFramework.Ecm.Domain;
using FireflyFramework.Ecm.Ports;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Ecm.ESignature.AdobeSign;

public sealed class AdobeSignOptions
{
    public const string SectionName = "Firefly:Ecm:ESignature:AdobeSign";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.na1.adobesign.com";
    public string ApiVersion { get; set; } = "/api/rest/v6";
    public string? WebhookUrl { get; set; }
    public string? WebhookSecret { get; set; }
    public string? ReturnUrl { get; set; }
}

/// <summary>Adobe Sign agreement adapter using OAuth2 refresh-token flow.</summary>
[EcmAdapter("adobe-sign",
    Description = "Adobe Sign Agreement Adapter",
    SupportedFeatures = AdapterFeature.ESignatureEnvelopes | AdapterFeature.ESignatureRequests | AdapterFeature.SignatureValidation,
    RequiredProperties = new[] { "ClientId", "ClientSecret", "RefreshToken" })]
public sealed class AdobeSignSignatureEnvelopeAdapter : ISignatureEnvelopePort
{
    private readonly HttpClient _http;
    private readonly AdobeSignOptions _opt;
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt;

    public AdobeSignSignatureEnvelopeAdapter(HttpClient http, IOptions<AdobeSignOptions> options)
    {
        _http = http;
        _opt = options.Value;
    }

    public async Task<SignatureEnvelope> CreateEnvelopeAsync(SignatureEnvelope envelope, CancellationToken ct = default)
    {
        await EnsureAuthAsync(ct).ConfigureAwait(false);
        var payload = new
        {
            name = envelope.Name,
            participantSetsInfo = envelope.Signers.Select((s, i) => new
            {
                memberInfos = new[] { new { email = s.SignerEmail } },
                order = i + 1,
                role = "SIGNER",
            }).ToArray(),
            signatureType = "ESIGN",
            state = "DRAFT",
        };

        using var resp = await _http.PostAsJsonAsync(Url("/agreements"), payload, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
        var id = doc.GetProperty("id").GetString()!;
        return envelope with { Status = SignatureEnvelopeStatus.Draft, Provider = $"adobe-sign:{id}" };
    }

    public async Task<SignatureEnvelope?> GetEnvelopeAsync(Guid envelopeId, CancellationToken ct = default)
    {
        await EnsureAuthAsync(ct).ConfigureAwait(false);
        using var resp = await _http.GetAsync(Url($"/agreements/{envelopeId}"), ct).ConfigureAwait(false);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
        return new SignatureEnvelope(envelopeId,
            doc.GetProperty("name").GetString() ?? string.Empty,
            Array.Empty<Guid>(), Array.Empty<SignatureRequest>(),
            ParseStatus(doc.GetProperty("status").GetString()),
            "adobe-sign",
            doc.TryGetProperty("createdDate", out var cd) ? cd.GetDateTimeOffset() : DateTimeOffset.UtcNow);
    }

    public async Task<SignatureEnvelope> UpdateEnvelopeAsync(SignatureEnvelope envelope, CancellationToken ct = default)
    {
        await EnsureAuthAsync(ct).ConfigureAwait(false);
        using var resp = await _http.PutAsJsonAsync(Url($"/agreements/{envelope.Id}"),
            new { name = envelope.Name }, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return envelope;
    }

    public async Task SendEnvelopeAsync(Guid envelopeId, string? sentBy, CancellationToken ct = default)
    {
        await EnsureAuthAsync(ct).ConfigureAwait(false);
        using var resp = await _http.PutAsJsonAsync(Url($"/agreements/{envelopeId}/state"),
            new { state = "IN_PROCESS" }, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    public async Task VoidEnvelopeAsync(Guid envelopeId, string reason, string? voidedBy, CancellationToken ct = default)
    {
        await EnsureAuthAsync(ct).ConfigureAwait(false);
        using var resp = await _http.PutAsJsonAsync(Url($"/agreements/{envelopeId}/state"),
            new { state = "CANCELLED", agreementCancellationInfo = new { comment = reason, notifyOthers = false } }, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    public Task CancelEnvelopeAsync(Guid envelopeId, CancellationToken ct = default) =>
        VoidEnvelopeAsync(envelopeId, "Cancelled", null, ct);

    public async Task<IReadOnlyList<SignatureEnvelope>> GetEnvelopesByStatusAsync(SignatureEnvelopeStatus status, CancellationToken ct = default)
    {
        await EnsureAuthAsync(ct).ConfigureAwait(false);
        using var resp = await _http.GetAsync(Url("/agreements"), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
        var list = new List<SignatureEnvelope>();
        if (doc.TryGetProperty("userAgreementList", out var agreements))
        {
            foreach (var a in agreements.EnumerateArray())
            {
                var s = ParseStatus(a.GetProperty("status").GetString());
                if (s != status) continue;
                list.Add(new SignatureEnvelope(
                    Guid.TryParse(a.GetProperty("agreementId").GetString(), out var g) ? g : Guid.NewGuid(),
                    a.GetProperty("name").GetString() ?? string.Empty,
                    Array.Empty<Guid>(), Array.Empty<SignatureRequest>(), s, "adobe-sign", DateTimeOffset.UtcNow));
            }
        }

        return list;
    }

    private string Url(string suffix) => $"{_opt.BaseUrl.TrimEnd('/')}{_opt.ApiVersion}{suffix}";

    private async Task EnsureAuthAsync(CancellationToken ct)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt - TimeSpan.FromSeconds(30))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken);
            return;
        }

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _opt.ClientId,
            ["client_secret"] = _opt.ClientSecret,
            ["refresh_token"] = _opt.RefreshToken,
        };

        using var resp = await _http.PostAsync($"{_opt.BaseUrl.TrimEnd('/')}/oauth/v2/refresh",
            new FormUrlEncodedContent(form), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
        _cachedToken = doc.GetProperty("access_token").GetString();
        _tokenExpiresAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(doc.GetProperty("expires_in").GetInt32());
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken);
    }

    private static SignatureEnvelopeStatus ParseStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "DRAFT" => SignatureEnvelopeStatus.Draft,
        "AUTHORING" => SignatureEnvelopeStatus.Draft,
        "OUT_FOR_SIGNATURE" or "IN_PROCESS" => SignatureEnvelopeStatus.Sent,
        "SIGNED" => SignatureEnvelopeStatus.Signed,
        "COMPLETED" or "ARCHIVED" => SignatureEnvelopeStatus.Completed,
        "CANCELLED" => SignatureEnvelopeStatus.Cancelled,
        "EXPIRED" => SignatureEnvelopeStatus.Voided,
        _ => SignatureEnvelopeStatus.Draft,
    };
}
