// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Concurrent;

namespace FireflyFramework.Admin.Server;

public sealed record AdminInstance(
    string Id,
    string Name,
    string ManagementUrl,
    string HealthUrl,
    DateTimeOffset RegisteredAt,
    DateTimeOffset LastHeartbeat,
    string Status,
    IReadOnlyDictionary<string, string> Metadata)
{
    public AdminInstance WithHeartbeat(string status) =>
        this with { LastHeartbeat = DateTimeOffset.UtcNow, Status = status };
}

public interface IInstanceRegistry
{
    AdminInstance Register(AdminInstance instance);
    AdminInstance? Heartbeat(string id, string status);
    bool Deregister(string id);
    AdminInstance? Get(string id);
    IReadOnlyCollection<AdminInstance> All();
    void EvictStale(TimeSpan timeout);
}

public sealed class InMemoryInstanceRegistry : IInstanceRegistry
{
    private readonly ConcurrentDictionary<string, AdminInstance> _store = new(StringComparer.OrdinalIgnoreCase);

    public AdminInstance Register(AdminInstance instance)
    {
        var stored = instance with { RegisteredAt = DateTimeOffset.UtcNow, LastHeartbeat = DateTimeOffset.UtcNow };
        _store[instance.Id] = stored;
        return stored;
    }

    public AdminInstance? Heartbeat(string id, string status) =>
        _store.TryGetValue(id, out var inst) ? _store[id] = inst.WithHeartbeat(status) : null;

    public bool Deregister(string id) => _store.TryRemove(id, out _);

    public AdminInstance? Get(string id) => _store.TryGetValue(id, out var i) ? i : null;

    public IReadOnlyCollection<AdminInstance> All() => _store.Values.ToList();

    public void EvictStale(TimeSpan timeout)
    {
        var threshold = DateTimeOffset.UtcNow - timeout;
        foreach (var kv in _store.Where(kv => kv.Value.LastHeartbeat < threshold).ToList())
            _store.TryRemove(kv.Key, out _);
    }
}
