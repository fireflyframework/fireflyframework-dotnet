// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Concurrent;

namespace FireflyFramework.WebSocket.Core;

public sealed class WebSocketSessionRegistry : IWebSocketSessionRegistry
{
    private readonly ConcurrentDictionary<string, IWebSocketSession> _sessions = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _groups = new();

    public void Add(IWebSocketSession session, params string[] groups)
    {
        _sessions[session.Id] = session;
        foreach (var g in groups)
        {
            var bucket = _groups.GetOrAdd(g, _ => new HashSet<string>());
            lock (bucket) bucket.Add(session.Id);
        }
    }

    public void Remove(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
        foreach (var bucket in _groups.Values)
            lock (bucket) bucket.Remove(sessionId);
    }

    public IEnumerable<IWebSocketSession> All() => _sessions.Values;

    public IEnumerable<IWebSocketSession> InGroup(string group)
    {
        if (!_groups.TryGetValue(group, out var ids)) yield break;
        string[] snapshot;
        lock (ids) snapshot = ids.ToArray();
        foreach (var id in snapshot)
            if (_sessions.TryGetValue(id, out var s)) yield return s;
    }

    public async Task BroadcastAsync(string text, string? group = null, CancellationToken ct = default)
    {
        var targets = group is null ? All() : InGroup(group);
        foreach (var s in targets.Where(s => s.IsOpen))
        {
            try { await s.SendTextAsync(text, ct).ConfigureAwait(false); }
            catch { /* best-effort broadcast */ }
        }
    }
}
