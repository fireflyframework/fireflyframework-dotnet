// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Concurrent;
using FireflyFramework.Kernel.Exceptions;
using Polly;
using Polly.Registry;

namespace FireflyFramework.Resilience.Core;

/// <summary>
/// Polly-backed implementation. Wraps <see cref="ResiliencePipelineProvider{TKey}"/> so
/// resolution stays consistent with the rest of the .NET 10 resilience ecosystem.
/// </summary>
public sealed class DefaultResilienceRegistry : IResilienceRegistry
{
    private readonly ConcurrentDictionary<string, ResiliencePipeline> _pipelines = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, object> _typedPipelines = new(StringComparer.OrdinalIgnoreCase);

    public ResiliencePipeline GetPipeline(string name) =>
        _pipelines.TryGetValue(name, out var p)
            ? p
            : throw new FireflyException($"Resilience pipeline not registered: {name}", "RESILIENCE_NOT_FOUND");

    public ResiliencePipeline<TResult> GetPipeline<TResult>(string name) =>
        _typedPipelines.TryGetValue(name, out var p) && p is ResiliencePipeline<TResult> typed
            ? typed
            : throw new FireflyException($"Resilience pipeline<{typeof(TResult).Name}> not registered: {name}", "RESILIENCE_NOT_FOUND");

    public void Register(string name, ResiliencePipeline pipeline) => _pipelines[name] = pipeline;
    public void Register<TResult>(string name, ResiliencePipeline<TResult> pipeline) => _typedPipelines[name] = pipeline;

    public bool Contains(string name) => _pipelines.ContainsKey(name) || _typedPipelines.ContainsKey(name);

    public IReadOnlyCollection<string> Names => _pipelines.Keys.Union(_typedPipelines.Keys, StringComparer.OrdinalIgnoreCase).ToList();
}
