// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Messaging.Core;

/// <summary>
/// Wire-level message envelope. Mirrors Spring <c>Message&lt;T&gt;</c> and
/// pyfly <c>Message</c>: payload + case-insensitive headers + correlation
/// metadata. Records are immutable; use <see cref="WithHeader"/> to derive
/// a new instance with an extra header.
/// </summary>
public sealed record Message<T>(
    T Payload,
    IReadOnlyDictionary<string, string> Headers,
    string? CorrelationId = null,
    string? ReplyTo = null,
    string? Destination = null,
    DateTimeOffset Timestamp = default)
{
    /// <summary>Returns a new envelope with the additional header set.</summary>
    public Message<T> WithHeader(string key, string value)
    {
        var h = new Dictionary<string, string>(Headers, StringComparer.OrdinalIgnoreCase) { [key] = value };
        return this with { Headers = h };
    }

    /// <summary>Returns the header value or <c>null</c> if the key is absent.</summary>
    public string? GetHeader(string key) => Headers.TryGetValue(key, out var v) ? v : null;

    /// <summary>Convenience factory: builds an envelope with the supplied headers and <c>UtcNow</c> timestamp.</summary>
    public static Message<T> Of(T payload, params (string key, string value)[] headers) =>
        new(payload, headers.ToDictionary(h => h.key, h => h.value, StringComparer.OrdinalIgnoreCase),
            Timestamp: DateTimeOffset.UtcNow);
}
