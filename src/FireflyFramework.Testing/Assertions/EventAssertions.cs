// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Testing.Eda;

namespace FireflyFramework.Testing.Assertions;

public static class EventAssertions
{
    public static void AssertEventPublished<T>(this EventCapturePublisher publisher) where T : class
    {
        if (!publisher.HasPublished<T>())
            throw new Xunit.Sdk.XunitException($"Expected event {typeof(T).Name} to be published, but none was found. " +
                $"Captured: {string.Join(", ", publisher.Published.Select(e => e.EventType))}");
    }

    public static void AssertNoEventsPublished(this EventCapturePublisher publisher)
    {
        if (publisher.Published.Count != 0)
            throw new Xunit.Sdk.XunitException($"Expected no events, but {publisher.Published.Count} were published: " +
                string.Join(", ", publisher.Published.Select(e => e.EventType)));
    }

    public static void AssertEventCount<T>(this EventCapturePublisher publisher, int expected) where T : class
    {
        var actual = publisher.AllOf<T>().Count;
        if (actual != expected)
            throw new Xunit.Sdk.XunitException($"Expected {expected} {typeof(T).Name} events, got {actual}.");
    }
}
