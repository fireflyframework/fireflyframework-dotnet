// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Agentic.Core;

namespace FireflyFramework.Agentic.Memory;

/// <summary>Conversation memory abstraction (short-term / window / summarized / vector).</summary>
public interface IAgentMemory
{
    Task AppendAsync(ChatMessage message, CancellationToken ct);
    Task<IReadOnlyList<ChatMessage>> GetContextAsync(CancellationToken ct);
    Task ClearAsync(CancellationToken ct);
}

public sealed class WindowedMemory : IAgentMemory
{
    private readonly int _windowSize;
    private readonly LinkedList<ChatMessage> _messages = new();

    public WindowedMemory(int windowSize = 20) { _windowSize = windowSize; }

    public Task AppendAsync(ChatMessage message, CancellationToken ct)
    {
        _messages.AddLast(message);
        while (_messages.Count > _windowSize) _messages.RemoveFirst();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ChatMessage>> GetContextAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ChatMessage>>(_messages.ToList());

    public Task ClearAsync(CancellationToken ct) { _messages.Clear(); return Task.CompletedTask; }
}
