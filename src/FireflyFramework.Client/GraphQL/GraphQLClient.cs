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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FireflyFramework.Client.GraphQL;

/// <summary>
/// Lightweight GraphQL client over <see cref="HttpClient"/>. Mirrors Java
/// <c>GraphQLClientHelper</c>. The client posts <c>{query, variables, operationName}</c>
/// to the configured endpoint and surfaces both the deserialised <c>data</c> field and
/// the GraphQL <c>errors</c> array as a strongly-typed
/// <see cref="GraphQLResponse{T}"/>.
///
/// <para>This is deliberately not a full-featured client (no schema introspection, no
/// subscriptions, no automatic persisted queries). It's a thin wrapper that lets a Firefly
/// service talk to a GraphQL backend without taking on a heavy dependency. For richer
/// scenarios use <c>HotChocolate.AspNetCore</c> client-side helpers (already pinned).</para>
/// </summary>
public sealed class GraphQLClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json;

    public GraphQLClient(HttpClient http, JsonSerializerOptions? jsonOptions = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _json = jsonOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    }

    /// <summary>
    /// Executes a GraphQL query (or mutation — they share the same wire shape) and returns
    /// the parsed <see cref="GraphQLResponse{T}"/>. Throws <see cref="HttpRequestException"/>
    /// only on transport-level failures; GraphQL-level failures surface in
    /// <see cref="GraphQLResponse{T}.Errors"/>.
    /// </summary>
    public async Task<GraphQLResponse<T>> ExecuteAsync<T>(
        string query,
        object? variables = null,
        string? operationName = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var payload = new GraphQLRequest(query, variables, operationName);
        using var resp = await _http.PostAsJsonAsync((string?)null, payload, _json, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<GraphQLResponse<T>>(_json, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("GraphQL endpoint returned an empty body.");

        return body;
    }

    /// <summary>Convenience: executes the query and unwraps <c>data</c>, throwing if errors are present.</summary>
    public async Task<T> ExecuteOrThrowAsync<T>(string query, object? variables = null, string? operationName = null, CancellationToken ct = default)
    {
        var response = await ExecuteAsync<T>(query, variables, operationName, ct).ConfigureAwait(false);
        if (response.Errors is { Count: > 0 })
        {
            throw new GraphQLException(response.Errors);
        }
        return response.Data ?? throw new InvalidOperationException("GraphQL response has no data and no errors.");
    }
}

/// <summary>Wire-format GraphQL request. <c>variables</c> is serialised as raw JSON.</summary>
public sealed record GraphQLRequest(
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("variables")] object? Variables,
    [property: JsonPropertyName("operationName")] string? OperationName);

/// <summary>Wire-format GraphQL response. Either <c>Data</c> or <c>Errors</c> may be populated; usually just one.</summary>
public sealed class GraphQLResponse<T>
{
    [JsonPropertyName("data")] public T? Data { get; init; }
    [JsonPropertyName("errors")] public IReadOnlyList<GraphQLError>? Errors { get; init; }
    [JsonPropertyName("extensions")] public IReadOnlyDictionary<string, JsonElement>? Extensions { get; init; }

    public bool HasErrors => Errors is { Count: > 0 };
}

/// <summary>One error from the GraphQL <c>errors</c> array.</summary>
public sealed class GraphQLError
{
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("path")] public IReadOnlyList<JsonElement>? Path { get; init; }
    [JsonPropertyName("locations")] public IReadOnlyList<GraphQLErrorLocation>? Locations { get; init; }
    [JsonPropertyName("extensions")] public IReadOnlyDictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>Source-document location of a GraphQL error.</summary>
public sealed record GraphQLErrorLocation(
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("column")] int Column);

/// <summary>Thrown by <see cref="GraphQLClient.ExecuteOrThrowAsync{T}"/> when the response carries errors.</summary>
public sealed class GraphQLException : Exception
{
    public IReadOnlyList<GraphQLError> Errors { get; }

    public GraphQLException(IReadOnlyList<GraphQLError> errors)
        : base(errors.Count == 1 ? errors[0].Message : $"{errors.Count} GraphQL errors: {string.Join("; ", errors.Select(e => e.Message))}")
    {
        Errors = errors;
    }
}
