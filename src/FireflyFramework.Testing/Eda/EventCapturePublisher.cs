// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Concurrent;
using FireflyFramework.Eda.Events;
using FireflyFramework.Eda.Publisher;

namespace FireflyFramework.Testing.Eda;

/// <summary>
/// Test double for <c>IEventPublisher</c> that records every published envelope so
/// tests can assert on them. Mirrors pyfly's <c>assert_event_published</c>.
/// </summary>
public sealed class EventCapturePublisher : IEventPublisher
{
    private readonly ConcurrentBag<EventEnvelope> _published = new();

    public PublisherType Type => PublisherType.InMemory;
    public string? DefaultDestination => null;
    public bool IsAvailable => true;

    public IReadOnlyCollection<EventEnvelope> Published => _published.ToList();

    public Task PublishAsync(EventEnvelope envelope, CancellationToken ct = default)
    {
        _published.Add(envelope);
        return Task.CompletedTask;
    }

    public Task<PublisherHealth> GetHealthAsync(CancellationToken ct = default) =>
        Task.FromResult(new PublisherHealth(Type, true, "UP"));

    public bool HasPublished<T>() => _published.Any(e => e.Payload is T || e.EventType == typeof(T).Name);
    public T? FirstOf<T>() where T : class =>
        _published.Select(e => e.Payload).OfType<T>().FirstOrDefault();
    public IReadOnlyCollection<T> AllOf<T>() where T : class =>
        _published.Select(e => e.Payload).OfType<T>().ToList();
    public void Clear() { while (_published.TryTake(out _)) { } }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
