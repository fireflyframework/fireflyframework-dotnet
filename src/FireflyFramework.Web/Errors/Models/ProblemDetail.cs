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

using System.Text.Json.Serialization;

namespace FireflyFramework.Web.Errors.Models;

/// <summary>
/// RFC 7807 Problem Details for HTTP APIs. Mirrors Java <c>ProblemDetail</c>. Use this
/// when you want a strict RFC 7807 response; <see cref="ErrorResponse"/> is the
/// recommended superset.
/// </summary>
public sealed class ProblemDetail
{
    [JsonPropertyName("type")]
    public Uri? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("status")]
    public int? Status { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("instance")]
    public Uri? Instance { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extensions { get; set; }

    public static ProblemDetail FromErrorResponse(ErrorResponse r) => new()
    {
        Type = r.Code is null ? null : new Uri($"https://errors.fireflyframework.org/{r.Code}", UriKind.Absolute),
        Title = r.Error ?? r.Message,
        Status = r.Status,
        Detail = r.Message,
        Instance = r.Path is null ? null : new Uri(r.Path, UriKind.Relative),
        Extensions = new Dictionary<string, object?>
        {
            ["timestamp"] = r.Timestamp,
            ["code"] = r.Code,
            ["traceId"] = r.TraceId,
            ["spanId"] = r.SpanId,
            ["correlationId"] = r.CorrelationId,
            ["category"] = r.Category.ToString(),
            ["severity"] = r.Severity.ToString(),
            ["retryable"] = r.Retryable,
            ["retryAfter"] = r.RetryAfter,
            ["errors"] = r.Errors,
            ["metadata"] = r.Metadata,
        }
    };
}
