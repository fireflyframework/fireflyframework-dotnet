// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Concurrent;
using FireflyFramework.Session.Core;

namespace FireflyFramework.Session.Adapters;

public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, IFireflySession> _store = new();

    public Task<IFireflySession?> LoadAsync(string id, CancellationToken ct)
    {
        if (!_store.TryGetValue(id, out var s) || s.IsExpired) return Task.FromResult<IFireflySession?>(null);
        s.LastAccessedAt = DateTimeOffset.UtcNow;
        return Task.FromResult<IFireflySession?>(s);
    }

    public Task SaveAsync(IFireflySession session, CancellationToken ct)
    {
        _store[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken ct)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<IFireflySession> CreateAsync(TimeSpan maxInactive, CancellationToken ct)
    {
        var s = new FireflySession(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, maxInactive);
        _store[s.Id] = s;
        return Task.FromResult<IFireflySession>(s);
    }
}
