// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FireflyFramework.AgenticBridge.Core;

namespace FireflyFramework.AgenticBridge.Adapters;

/// <summary>HTTP-based bridge client. Sends a JSON envelope and polls or streams for the result.</summary>
public sealed class RestAgenticClient : IAgenticClient
{
    private readonly HttpClient _http;

    public RestAgenticClient(HttpClient http) { _http = http; }

    public async Task<AgentInvocationResult> InvokeAsync(AgentInvocation invocation, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync($"agents/{invocation.AgentId}/invoke", invocation, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentInvocationResult>(cancellationToken: ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Empty agent response");
    }

    public async IAsyncEnumerable<AgentEvent> StreamAsync(AgentInvocation invocation, [EnumeratorCancellation] CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"agents/{invocation.AgentId}/stream")
        {
            Content = JsonContent.Create(invocation),
        };
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        string? line;
        while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var json = line["data:".Length..].Trim();
            var ev = JsonSerializer.Deserialize<AgentEvent>(json);
            if (ev is not null) yield return ev;
        }
    }
}
