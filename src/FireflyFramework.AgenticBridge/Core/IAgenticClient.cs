// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.AgenticBridge.Core;

/// <summary>
/// Request envelope for invoking a Python-hosted agent. <see cref="ConversationId"/>
/// lets callers thread multi-turn dialogs across requests; the bridge passes it
/// through to the agent runtime so it can rehydrate prior context.
/// </summary>
public sealed record AgentInvocation(
    string AgentId,
    string Input,
    IDictionary<string, object?>? Context = null,
    string? CorrelationId = null,
    string? ConversationId = null);

/// <summary>
/// Synchronous reply from an agent invocation. <see cref="Tools"/> contains
/// every tool call the agent made during this turn so the caller can audit them.
/// </summary>
public sealed record AgentInvocationResult(
    string ConversationId,
    string Output,
    IReadOnlyList<AgentToolInvocation> Tools,
    IReadOnlyDictionary<string, object?> Metadata);

/// <summary>One tool invocation observed during an agent turn.</summary>
public sealed record AgentToolInvocation(string Name, string ArgumentsJson, string ResultJson, TimeSpan Duration);

/// <summary>An incremental event emitted by the streaming endpoint (token, tool call, status).</summary>
public sealed record AgentEvent(string Type, string Payload, DateTimeOffset Timestamp);

/// <summary>
/// Transport abstraction over a Python-hosted Firefly agent. Implementations
/// translate <see cref="AgentInvocation"/> into the wire format the runtime
/// expects (REST today; SSE / WebSocket / queue in future).
/// </summary>
public interface IAgenticClient
{
    /// <summary>Invokes the agent and waits for the full reply.</summary>
    Task<AgentInvocationResult> InvokeAsync(AgentInvocation invocation, CancellationToken ct);

    /// <summary>Streams incremental events from the agent runtime.</summary>
    IAsyncEnumerable<AgentEvent> StreamAsync(AgentInvocation invocation, CancellationToken ct);
}
