using System.Net.Http.Json;
using FireflyFramework.RuleEngine.Interfaces;

namespace FireflyFramework.RuleEngine.Sdk;

/// <summary>HTTP client for the Firefly rule engine REST API.</summary>
public sealed class RuleEngineClient
{
    private readonly HttpClient _http;

    public RuleEngineClient(HttpClient http) => _http = http;

    public async Task<RulesEvaluationResponseDto?> EvaluateAsync(
        RulesEvaluationRequestDto request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/rules/evaluate/direct", request, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RulesEvaluationResponseDto>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<RulesEvaluationResponseDto?> EvaluateByCodeAsync(
        RuleEvaluationByCodeRequestDto request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/rules/evaluate/by-code", request, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RulesEvaluationResponseDto>(cancellationToken: ct).ConfigureAwait(false);
    }
}
