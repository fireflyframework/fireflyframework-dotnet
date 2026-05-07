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

using FireflyFramework.Cache.Core;
using FireflyFramework.Cqrs.Authorization;
using FireflyFramework.Cqrs.Context;
using FireflyFramework.Cqrs.Queries;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Cqrs.Buses;

public sealed class DefaultQueryBus : IQueryBus
{
    private const string CachePrefix = "firefly:cqrs:query:";

    private readonly IServiceProvider _provider;
    private readonly ICacheAdapter? _cache;
    private readonly ILogger<DefaultQueryBus> _log;

    public DefaultQueryBus(IServiceProvider provider, ILogger<DefaultQueryBus> log, ICacheAdapter? cache = null)
    {
        _provider = provider;
        _cache = cache;
        _log = log;
    }

    public async Task<TResult> AskAsync<TResult>(IQuery<TResult> query, ExecutionContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var auth = await query.AuthorizeAsync(context, ct).ConfigureAwait(false);
        if (!auth.IsAllowed)
        {
            throw new CqrsAuthorizationException(
                string.Join("; ", auth.Errors.Select(e => $"{e.Code}: {e.Message}")), auth.Errors);
        }

        if (query.IsCacheable && _cache is not null && query.CacheKey is { } key)
        {
            var hit = await _cache.GetAsync<TResult>(CachePrefix + key, ct).ConfigureAwait(false);
            if (hit is not null && !EqualityComparer<TResult>.Default.Equals(hit, default!))
            {
                return hit;
            }
        }

        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
        var handler = _provider.GetService(handlerType)
            ?? throw new InvalidOperationException($"No query handler registered for {query.GetType().FullName}");

        var method = handlerType.GetMethod("HandleAsync")!;
        _log.LogDebug("Dispatching {QueryType} via {Handler}", query.GetType().Name, handler.GetType().Name);
        var task = (Task<TResult>)method.Invoke(handler, new object[] { query, context, ct })!;
        var result = await task.ConfigureAwait(false);

        if (query.IsCacheable && _cache is not null && query.CacheKey is { } cacheKey)
        {
            var ttl = query.CacheTtl ?? TimeSpan.FromMinutes(5);
            await _cache.PutAsync(CachePrefix + cacheKey, result, ttl, ct).ConfigureAwait(false);
        }

        return result;
    }

    public async Task ClearCacheAsync(string? pattern = null, CancellationToken ct = default)
    {
        if (_cache is null) return;
        if (pattern is null)
        {
            await _cache.EvictByPrefixAsync(CachePrefix, ct).ConfigureAwait(false);
        }
        else
        {
            await _cache.EvictByPrefixAsync(CachePrefix + pattern, ct).ConfigureAwait(false);
        }
    }
}
