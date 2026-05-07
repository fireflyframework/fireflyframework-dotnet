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
