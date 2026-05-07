// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Agentic.Core;

/// <summary>Provider-agnostic chat completion port. Adapters wrap OpenAI / Anthropic / Azure / Bedrock APIs.</summary>
public interface IChatModel
{
    string ModelId { get; }
    Task<ChatResponse> CompleteAsync(IReadOnlyList<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamAsync(IReadOnlyList<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default);
}

public sealed class ChatOptions
{
    public double? Temperature { get; init; }
    public int? MaxTokens { get; init; }
    public IReadOnlyList<IAgentTool>? Tools { get; init; }
    public string? ToolChoice { get; init; }
    public IReadOnlyList<string>? StopSequences { get; init; }
    public IDictionary<string, object?>? Metadata { get; init; }
}

/// <summary>Embedding model port for vector retrieval.</summary>
public interface IEmbeddingModel
{
    string ModelId { get; }
    int Dimensions { get; }
    Task<float[]> EmbedAsync(string input, CancellationToken ct = default);
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> inputs, CancellationToken ct = default);
}
