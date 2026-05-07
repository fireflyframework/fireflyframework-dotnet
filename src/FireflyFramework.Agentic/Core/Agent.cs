// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Agentic.Memory;

namespace FireflyFramework.Agentic.Core;

/// <summary>
/// Tool-using agent loop: drives a chat model, dispatches tool calls back into
/// registered tools, threads results through memory, and stops on a final
/// assistant message or a tool budget.
/// </summary>
public sealed class Agent
{
    private readonly IChatModel _model;
    private readonly IAgentMemory _memory;
    private readonly IReadOnlyDictionary<string, IAgentTool> _tools;
    private readonly string _systemPrompt;
    private readonly int _maxTurns;

    public Agent(IChatModel model, IAgentMemory memory, IEnumerable<IAgentTool> tools, string systemPrompt, int maxTurns = 8)
    {
        _model = model;
        _memory = memory;
        _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        _systemPrompt = systemPrompt;
        _maxTurns = maxTurns;
    }

    public async Task<string> AskAsync(string userInput, CancellationToken ct)
    {
        await _memory.AppendAsync(new ChatMessage(MessageRole.User, userInput), ct).ConfigureAwait(false);

        for (int turn = 0; turn < _maxTurns; turn++)
        {
            var ctx = new List<ChatMessage> { new(MessageRole.System, _systemPrompt) };
            ctx.AddRange(await _memory.GetContextAsync(ct).ConfigureAwait(false));

            var response = await _model.CompleteAsync(ctx, new ChatOptions { Tools = _tools.Values.ToList() }, ct).ConfigureAwait(false);

            if (response.ToolCalls.Count == 0)
            {
                await _memory.AppendAsync(new ChatMessage(MessageRole.Assistant, response.Content), ct).ConfigureAwait(false);
                return response.Content;
            }

            await _memory.AppendAsync(new ChatMessage(MessageRole.Assistant, response.Content ?? string.Empty), ct).ConfigureAwait(false);

            foreach (var call in response.ToolCalls)
            {
                if (!_tools.TryGetValue(call.Name, out var tool))
                {
                    await _memory.AppendAsync(new ChatMessage(MessageRole.Tool, $"unknown tool {call.Name}", ToolCallId: call.Id), ct).ConfigureAwait(false);
                    continue;
                }
                var result = await tool.InvokeAsync(call.ArgumentsJson, ct).ConfigureAwait(false);
                await _memory.AppendAsync(new ChatMessage(MessageRole.Tool, result, Name: call.Name, ToolCallId: call.Id), ct).ConfigureAwait(false);
            }
        }

        return $"agent stopped after {_maxTurns} turns without a final answer";
    }
}
