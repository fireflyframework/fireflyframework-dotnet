// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Agentic.Core;

public enum MessageRole { System, User, Assistant, Tool }

public sealed record ChatMessage(MessageRole Role, string Content, string? Name = null, string? ToolCallId = null);

public sealed record ToolCall(string Id, string Name, string ArgumentsJson);

public sealed record ChatResponse(
    string Content,
    IReadOnlyList<ToolCall> ToolCalls,
    string? FinishReason,
    int? PromptTokens = null,
    int? CompletionTokens = null);

public sealed record ToolResult(string ToolCallId, string ResultJson);
