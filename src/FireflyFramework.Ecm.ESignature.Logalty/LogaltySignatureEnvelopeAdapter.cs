using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FireflyFramework.Ecm.Adapters;
using FireflyFramework.Ecm.Domain;
using FireflyFramework.Ecm.Ports;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Ecm.ESignature.Logalty;

public sealed class LogaltyOptions
{
    public const string SectionName = "Firefly:Ecm:ESignature:Logalty";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.logalty.com";
}

/// <summary>
/// Logalty (EU qualified e-signature) adapter using OAuth2 client-credentials. Mirrors
/// Java <c>LogaltySignatureEnvelopeAdapter</c>.
/// </summary>
[EcmAdapter("logalty",
    Description = "Logalty Qualified Signature Adapter (EU)",
    SupportedFeatures = AdapterFeature.ESignatureEnvelopes | AdapterFeature.ESignatureRequests | AdapterFeature.SignatureValidation,
    RequiredProperties = new[] { "ClientId", "ClientSecret" })]
public sealed class LogaltySignatureEnvelopeAdapter : ISignatureEnvelopePort
{
    private readonly HttpClient _http;
    private readonly LogaltyOptions _opt;
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt;

    public LogaltySignatureEnvelopeAdapter(HttpClient http, IOptions<LogaltyOptions> options)
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
            signers = envelope.Signers.Select(s => new { email = s.SignerEmail, name = s.SignerName }).ToArray(),
            documents = envelope.DocumentIds.Select(id => new { id }).ToArray(),
            status = "DRAFT",
        };

        using var resp = await _http.PostAsJsonAsync(Url("/v1/processes"), payload, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
        var id = doc.GetProperty("id").GetString()!;
        return envelope with { Status = SignatureEnvelopeStatus.Draft, Provider = $"logalty:{id}" };
    }

    public async Task<SignatureEnvelope?> GetEnvelopeAsync(Guid envelopeId, CancellationToken ct = default)
    {
        await EnsureAuthAsync(ct).ConfigureAwait(false);
        using var resp = await _http.GetAsync(Url($"/v1/processes/{envelopeId}"), ct).ConfigureAwait(false);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
        return new SignatureEnvelope(envelopeId,
            doc.GetProperty("name").GetString() ?? string.Empty,
            Array.Empty<Guid>(), Array.Empty<SignatureRequest>(),
            ParseStatus(doc.GetProperty("status").GetString()),
            "logalty",
            doc.TryGetProperty("createdAt", out var c) ? c.GetDateTimeOffset() : DateTimeOffset.UtcNow);
    }

    public Task<SignatureEnvelope> UpdateEnvelopeAsync(SignatureEnvelope envelope, CancellationToken ct = default) =>
        Task.FromResult(envelope); // Logalty processes are immutable once created

    public async Task SendEnvelopeAsync(Guid envelopeId, string? sentBy, CancellationToken ct = default)
    {
        await EnsureAuthAsync(ct).ConfigureAwait(false);
        using var resp = await _http.PostAsync(Url($"/v1/processes/{envelopeId}/start"), content: null, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    public async Task VoidEnvelopeAsync(Guid envelopeId, string reason, string? voidedBy, CancellationToken ct = default)
    {
        await EnsureAuthAsync(ct).ConfigureAwait(false);
        using var resp = await _http.PostAsJsonAsync(Url($"/v1/processes/{envelopeId}/cancel"),
            new { reason }, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    public Task CancelEnvelopeAsync(Guid envelopeId, CancellationToken ct = default) =>
        VoidEnvelopeAsync(envelopeId, "Cancelled", null, ct);

    public async Task<IReadOnlyList<SignatureEnvelope>> GetEnvelopesByStatusAsync(SignatureEnvelopeStatus status, CancellationToken ct = default)
    {
        await EnsureAuthAsync(ct).ConfigureAwait(false);
        using var resp = await _http.GetAsync(Url($"/v1/processes?status={MapStatus(status)}"), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
        var list = new List<SignatureEnvelope>();
        if (doc.TryGetProperty("items", out var items))
        {
            foreach (var i in items.EnumerateArray())
            {
                list.Add(new SignatureEnvelope(
                    Guid.TryParse(i.GetProperty("id").GetString(), out var g) ? g : Guid.NewGuid(),
                    i.GetProperty("name").GetString() ?? string.Empty,
                    Array.Empty<Guid>(), Array.Empty<SignatureRequest>(),
                    ParseStatus(i.GetProperty("status").GetString()), "logalty", DateTimeOffset.UtcNow));
            }
        }

        return list;
    }

    private string Url(string suffix) => $"{_opt.BaseUrl.TrimEnd('/')}{suffix}";

    private async Task EnsureAuthAsync(CancellationToken ct)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt - TimeSpan.FromSeconds(30))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken);
            return;
        }

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _opt.ClientId,
            ["client_secret"] = _opt.ClientSecret,
        };

        using var resp = await _http.PostAsync($"{_opt.BaseUrl.TrimEnd('/')}/oauth/token",
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
        "ACTIVE" or "IN_PROGRESS" or "PENDING" => SignatureEnvelopeStatus.Sent,
        "SIGNED" => SignatureEnvelopeStatus.Signed,
        "COMPLETED" => SignatureEnvelopeStatus.Completed,
        "CANCELLED" => SignatureEnvelopeStatus.Cancelled,
        "VOIDED" => SignatureEnvelopeStatus.Voided,
        _ => SignatureEnvelopeStatus.Draft,
    };

    private static string MapStatus(SignatureEnvelopeStatus status) => status switch
    {
        SignatureEnvelopeStatus.Draft => "DRAFT",
        SignatureEnvelopeStatus.Sent => "ACTIVE",
        SignatureEnvelopeStatus.Signed => "SIGNED",
        SignatureEnvelopeStatus.Completed => "COMPLETED",
        SignatureEnvelopeStatus.Cancelled => "CANCELLED",
        SignatureEnvelopeStatus.Voided => "VOIDED",
        _ => "DRAFT",
    };
}
