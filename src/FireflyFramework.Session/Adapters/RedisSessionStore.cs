// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Text.Json;
using FireflyFramework.Session.Core;
using StackExchange.Redis;

namespace FireflyFramework.Session.Adapters;

public sealed class RedisSessionStore : ISessionStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _keyPrefix;

    public RedisSessionStore(IConnectionMultiplexer redis, string keyPrefix = "firefly:session:")
    {
        _redis = redis;
        _keyPrefix = keyPrefix;
    }

    private RedisKey Key(string id) => $"{_keyPrefix}{id}";

    public async Task<IFireflySession?> LoadAsync(string id, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var raw = await db.StringGetAsync(Key(id)).ConfigureAwait(false);
        if (!raw.HasValue) return null;

        var dto = JsonSerializer.Deserialize<SessionDto>(raw.ToString());
        if (dto is null) return null;

        var s = new FireflySession(dto.Id, dto.CreatedAt, dto.MaxInactiveInterval, dto.Attributes) { LastAccessedAt = DateTimeOffset.UtcNow };
        if (s.IsExpired) { await db.KeyDeleteAsync(Key(id)).ConfigureAwait(false); return null; }
        return s;
    }

    public async Task SaveAsync(IFireflySession session, CancellationToken ct)
    {
        var dto = new SessionDto(session.Id, session.CreatedAt, session.MaxInactiveInterval, session.Snapshot().ToDictionary(kv => kv.Key, kv => kv.Value));
        var json = JsonSerializer.Serialize(dto);
        await _redis.GetDatabase().StringSetAsync(Key(session.Id), json, session.MaxInactiveInterval).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string id, CancellationToken ct) =>
        await _redis.GetDatabase().KeyDeleteAsync(Key(id)).ConfigureAwait(false);

    public Task<IFireflySession> CreateAsync(TimeSpan maxInactive, CancellationToken ct) =>
        Task.FromResult<IFireflySession>(new FireflySession(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, maxInactive));

    private sealed record SessionDto(string Id, DateTimeOffset CreatedAt, TimeSpan MaxInactiveInterval, Dictionary<string, object?> Attributes);
}
