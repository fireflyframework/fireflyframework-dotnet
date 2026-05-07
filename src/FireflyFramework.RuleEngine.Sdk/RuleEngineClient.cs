using System.Net.Http.Json;
using FireflyFramework.RuleEngine.Interfaces;

namespace FireflyFramework.RuleEngine.Sdk;

public sealed class RuleEngineClient : IRuleEngineClient
{
    private readonly HttpClient _http;

    public RuleEngineClient(HttpClient http) => _http = http;

    public Task<RulesEvaluationResponseDto?> EvaluateAsync(
        RulesEvaluationRequestDto request, CancellationToken ct = default) =>
        PostAsync("api/rules/evaluate/direct", request, ct);

    public Task<RulesEvaluationResponseDto?> EvaluatePlainAsync(
        PlainYamlEvaluationRequestDto request, CancellationToken ct = default) =>
        PostAsync("api/rules/evaluate/plain", request, ct);

    public Task<RulesEvaluationResponseDto?> EvaluateByCodeAsync(
        RuleEvaluationByCodeRequestDto request, CancellationToken ct = default) =>
        PostAsync("api/rules/evaluate/by-code", request, ct);

    private async Task<RulesEvaluationResponseDto?> PostAsync<T>(string path, T body, CancellationToken ct)
    {
        using var resp = await _http.PostAsJsonAsync(path, body, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RulesEvaluationResponseDto>(cancellationToken: ct).ConfigureAwait(false);
    }
}
