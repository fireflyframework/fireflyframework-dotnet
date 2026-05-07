// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Concurrent;

namespace FireflyFramework.Session.Core;

public sealed class FireflySession : IFireflySession
{
    private readonly ConcurrentDictionary<string, object?> _attributes;

    public FireflySession(string id, DateTimeOffset createdAt, TimeSpan maxInactive, IDictionary<string, object?>? initial = null)
    {
        Id = id;
        CreatedAt = createdAt;
        LastAccessedAt = DateTimeOffset.UtcNow;
        MaxInactiveInterval = maxInactive;
        _attributes = new(initial ?? new Dictionary<string, object?>(), StringComparer.OrdinalIgnoreCase);
    }

    public string Id { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset LastAccessedAt { get; set; }
    public TimeSpan MaxInactiveInterval { get; set; }

    public bool IsExpired => MaxInactiveInterval > TimeSpan.Zero && DateTimeOffset.UtcNow - LastAccessedAt > MaxInactiveInterval;

    public bool TryGet<T>(string key, out T? value)
    {
        if (_attributes.TryGetValue(key, out var raw) && raw is T cast) { value = cast; return true; }
        value = default; return false;
    }

    public void Set<T>(string key, T value) => _attributes[key] = value;
    public void Remove(string key) => _attributes.TryRemove(key, out _);

    public IReadOnlyCollection<string> Keys => _attributes.Keys.ToList();
    public IReadOnlyDictionary<string, object?> Snapshot() => _attributes.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
}
