// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Messaging.Core;

/// <summary>
/// Wire-level message envelope. Mirrors Spring <c>Message&lt;T&gt;</c> /
/// pyfly <c>Message</c>.
/// </summary>
public sealed record Message<T>(
    T Payload,
    IReadOnlyDictionary<string, string> Headers,
    string? CorrelationId = null,
    string? ReplyTo = null,
    string? Destination = null,
    DateTimeOffset Timestamp = default)
{
    public Message<T> WithHeader(string key, string value)
    {
        var h = new Dictionary<string, string>(Headers, StringComparer.OrdinalIgnoreCase) { [key] = value };
        return this with { Headers = h };
    }

    public string? GetHeader(string key) => Headers.TryGetValue(key, out var v) ? v : null;

    public static Message<T> Of(T payload, params (string key, string value)[] headers) =>
        new(payload, headers.ToDictionary(h => h.key, h => h.value, StringComparer.OrdinalIgnoreCase),
            Timestamp: DateTimeOffset.UtcNow);
}
