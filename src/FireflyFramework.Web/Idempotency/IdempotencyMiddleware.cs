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

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Web.Idempotency;

/// <summary>
/// ASP.NET Core middleware that caches the response of write requests carrying the
/// <see cref="IdempotencyOptions.HeaderName"/> header so retries return the same body.
/// Mirrors Java <c>IdempotencyWebFilter</c>.
/// </summary>
public sealed class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IdempotencyOptions _options;
    private readonly IDistributedCache _cache;
    private readonly ILogger<IdempotencyMiddleware> _log;

    public IdempotencyMiddleware(
        RequestDelegate next,
        IOptions<IdempotencyOptions> options,
        IDistributedCache cache,
        ILogger<IdempotencyMiddleware> log)
    {
        _next = next;
        _options = options.Value;
        _cache = cache;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled || !_options.Methods.Contains(context.Request.Method))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<DisableIdempotencyAttribute>() is not null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var key = context.Request.Headers[_options.HeaderName].ToString();
        if (string.IsNullOrEmpty(key))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (key.Length > _options.MaxKeyLength)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync($"Idempotency key exceeds {_options.MaxKeyLength} chars").ConfigureAwait(false);
            return;
        }

        var cacheKey = $"firefly:idempotency:{context.Request.Path}:{key}";
        var cached = await _cache.GetStringAsync(cacheKey).ConfigureAwait(false);
        if (cached is not null)
        {
            _log.LogDebug("Replaying cached idempotent response for {Key}", key);
            var entry = JsonSerializer.Deserialize<CachedResponse>(cached);
            if (entry is not null)
            {
                context.Response.StatusCode = entry.StatusCode;
                foreach (var header in entry.Headers)
                {
                    context.Response.Headers[header.Key] = header.Value;
                }

                if (entry.Body is not null)
                {
                    await context.Response.WriteAsync(entry.Body).ConfigureAwait(false);
                }

                return;
            }
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context).ConfigureAwait(false);

            buffer.Position = 0;
            var body = await new StreamReader(buffer).ReadToEndAsync().ConfigureAwait(false);
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody).ConfigureAwait(false);

            if (context.Response.StatusCode is >= 200 and < 300)
            {
                var entry = new CachedResponse(
                    context.Response.StatusCode,
                    context.Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                    body);

                await _cache.SetStringAsync(
                        cacheKey,
                        JsonSerializer.Serialize(entry),
                        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _options.Ttl })
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private sealed record CachedResponse(int StatusCode, Dictionary<string, string> Headers, string? Body);
}
