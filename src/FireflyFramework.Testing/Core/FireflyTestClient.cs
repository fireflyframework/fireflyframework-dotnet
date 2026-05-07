// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Net.Http.Json;
using System.Text;

namespace FireflyFramework.Testing.Core;

/// <summary>
/// Thin wrapper over <see cref="HttpClient"/> exposing JSON-friendly helpers
/// — Firefly equivalent of pyfly's <c>PyFlyTestClient</c>.
/// </summary>
public sealed class FireflyTestClient
{
    private readonly HttpClient _client;
    public FireflyTestClient(HttpClient client) { _client = client; }

    public async Task<TestResponse<T>> GetAsync<T>(string path, CancellationToken ct = default)
    {
        var response = await _client.GetAsync(path, ct).ConfigureAwait(false);
        var body = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct).ConfigureAwait(false) : default;
        return new TestResponse<T>((int)response.StatusCode, response, body);
    }

    public async Task<TestResponse<TOut>> PostAsync<TIn, TOut>(string path, TIn body, CancellationToken ct = default)
    {
        var response = await _client.PostAsJsonAsync(path, body, ct).ConfigureAwait(false);
        var payload = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<TOut>(cancellationToken: ct).ConfigureAwait(false) : default;
        return new TestResponse<TOut>((int)response.StatusCode, response, payload);
    }

    public async Task<TestResponse<string>> SendAsync(HttpMethod method, string path, object? body = null, CancellationToken ct = default)
    {
        using var msg = new HttpRequestMessage(method, path);
        if (body is not null) msg.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(msg, ct).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return new TestResponse<string>((int)response.StatusCode, response, raw);
    }
}

public sealed record TestResponse<T>(int StatusCode, HttpResponseMessage Raw, T? Body)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300;
}
