// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Concurrent;
using FireflyFramework.Messaging.Core;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Messaging.Adapters;

public sealed class InMemoryMessageBroker : IMessageBroker
{
    private readonly ILogger<InMemoryMessageBroker> _logger;
    private readonly ConcurrentDictionary<string, List<Func<object, CancellationToken, Task>>> _subscribers = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryMessageBroker(ILogger<InMemoryMessageBroker> logger) { _logger = logger; }

    public async Task SendAsync<T>(string destination, Message<T> message, CancellationToken ct = default)
    {
        if (!_subscribers.TryGetValue(destination, out var subs)) return;
        var snapshot = subs.ToArray();
        foreach (var s in snapshot)
        {
            try { await s(message!, ct).ConfigureAwait(false); }
            catch (Exception ex) { _logger.LogError(ex, "Subscriber failed on {Destination}", destination); }
        }
    }

    public IDisposable Subscribe<T>(string destination, Func<Message<T>, CancellationToken, Task> handler)
    {
        var subs = _subscribers.GetOrAdd(destination, _ => new List<Func<object, CancellationToken, Task>>());
        Func<object, CancellationToken, Task> wrap = (m, ct) => m is Message<T> typed ? handler(typed, ct) : Task.CompletedTask;
        lock (subs) subs.Add(wrap);
        return new Subscription(() => { lock (subs) subs.Remove(wrap); });
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _dispose;
        public Subscription(Action d) => _dispose = d;
        public void Dispose() { _dispose?.Invoke(); _dispose = null; }
    }
}
