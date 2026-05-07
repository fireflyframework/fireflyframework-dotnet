// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Text.Json;

namespace FireflyFramework.Agentic.Core;

/// <summary>Tool exposed to a chat model.</summary>
public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    JsonElement ParametersSchema { get; }
    Task<string> InvokeAsync(string argumentsJson, CancellationToken ct);
}

/// <summary>Strongly-typed convenience base.</summary>
public abstract class AgentTool<TArgs, TResult> : IAgentTool where TArgs : class
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual JsonElement ParametersSchema => JsonDocument.Parse("{\"type\":\"object\"}").RootElement;

    public async Task<string> InvokeAsync(string argumentsJson, CancellationToken ct)
    {
        var args = JsonSerializer.Deserialize<TArgs>(argumentsJson) ?? throw new ArgumentException($"Invalid args for tool {Name}");
        var result = await ExecuteAsync(args, ct).ConfigureAwait(false);
        return JsonSerializer.Serialize(result);
    }

    protected abstract Task<TResult> ExecuteAsync(TArgs args, CancellationToken ct);
}
