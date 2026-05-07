// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.AgenticBridge.Core;

public sealed record AgentInvocation(
    string AgentId,
    string Input,
    IDictionary<string, object?>? Context = null,
    string? CorrelationId = null,
    string? ConversationId = null);

public sealed record AgentInvocationResult(
    string ConversationId,
    string Output,
    IReadOnlyList<AgentToolInvocation> Tools,
    IReadOnlyDictionary<string, object?> Metadata);

public sealed record AgentToolInvocation(string Name, string ArgumentsJson, string ResultJson, TimeSpan Duration);

public sealed record AgentEvent(string Type, string Payload, DateTimeOffset Timestamp);

/// <summary>Client for Python-hosted Firefly agents.</summary>
public interface IAgenticClient
{
    Task<AgentInvocationResult> InvokeAsync(AgentInvocation invocation, CancellationToken ct);
    IAsyncEnumerable<AgentEvent> StreamAsync(AgentInvocation invocation, CancellationToken ct);
}
