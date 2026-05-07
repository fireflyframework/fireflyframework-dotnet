// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Agentic.Core;
using FireflyFramework.Agentic.Memory;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class AgenticTests
{
    [Fact]
    public async Task WindowedMemory_evicts_oldest_when_full()
    {
        var mem = new WindowedMemory(windowSize: 2);
        await mem.AppendAsync(new ChatMessage(MessageRole.User, "first"), CancellationToken.None);
        await mem.AppendAsync(new ChatMessage(MessageRole.User, "second"), CancellationToken.None);
        await mem.AppendAsync(new ChatMessage(MessageRole.User, "third"), CancellationToken.None);

        var ctx = await mem.GetContextAsync(CancellationToken.None);
        ctx.Select(m => m.Content).Should().BeEquivalentTo(new[] { "second", "third" });
    }

    [Fact]
    public async Task Agent_loops_until_assistant_returns_no_tool_calls()
    {
        var model = new ScriptedChatModel(new[]
        {
            new ChatResponse(string.Empty, new[] { new ToolCall("c1", "echo", "{\"input\":\"hi\"}") }, FinishReason: "tool_calls"),
            new ChatResponse("done: hi", Array.Empty<ToolCall>(), FinishReason: "stop"),
        });
        var memory = new WindowedMemory();
        var tool = new EchoTool();
        var agent = new Agent(model, memory, new[] { (IAgentTool)tool }, "system", maxTurns: 3);

        var result = await agent.AskAsync("hi", CancellationToken.None);

        result.Should().Be("done: hi");
        tool.LastInput.Should().Be("hi");
    }

    private sealed class ScriptedChatModel : IChatModel
    {
        private readonly Queue<ChatResponse> _scripted;
        public string ModelId => "scripted";

        public ScriptedChatModel(IEnumerable<ChatResponse> scripted) { _scripted = new(scripted); }

        public Task<ChatResponse> CompleteAsync(IReadOnlyList<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(_scripted.Dequeue());

        public async IAsyncEnumerable<string> StreamAsync(IReadOnlyList<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            yield return _scripted.Dequeue().Content;
        }
    }

    private sealed class EchoTool : IAgentTool
    {
        public string Name => "echo";
        public string Description => "echoes input";
        public System.Text.Json.JsonElement ParametersSchema => System.Text.Json.JsonDocument.Parse("{}").RootElement;
        public string? LastInput { get; private set; }

        public Task<string> InvokeAsync(string argumentsJson, CancellationToken ct)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(argumentsJson);
            LastInput = doc.RootElement.GetProperty("input").GetString();
            return Task.FromResult($"\"{LastInput}\"");
        }
    }
}
