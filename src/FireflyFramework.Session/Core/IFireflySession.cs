// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Session.Core;

/// <summary>Spring <c>HttpSession</c> port — distributed session abstraction.</summary>
public interface IFireflySession
{
    string Id { get; }
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset LastAccessedAt { get; set; }
    TimeSpan MaxInactiveInterval { get; set; }
    bool IsExpired { get; }

    bool TryGet<T>(string key, out T? value);
    void Set<T>(string key, T value);
    void Remove(string key);
    IReadOnlyCollection<string> Keys { get; }
    IReadOnlyDictionary<string, object?> Snapshot();
}

/// <summary>Backing store contract.</summary>
public interface ISessionStore
{
    Task<IFireflySession?> LoadAsync(string id, CancellationToken ct);
    Task SaveAsync(IFireflySession session, CancellationToken ct);
    Task DeleteAsync(string id, CancellationToken ct);
    Task<IFireflySession> CreateAsync(TimeSpan maxInactive, CancellationToken ct);
}
