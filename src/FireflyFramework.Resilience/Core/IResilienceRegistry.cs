// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using Polly;

namespace FireflyFramework.Resilience.Core;

/// <summary>
/// Central registry for named resilience pipelines. Mirrors Java
/// <c>CircuitBreakerRegistry</c> / <c>RetryRegistry</c> et al, unified.
/// </summary>
public interface IResilienceRegistry
{
    ResiliencePipeline GetPipeline(string name);
    ResiliencePipeline<TResult> GetPipeline<TResult>(string name);
    void Register(string name, ResiliencePipeline pipeline);
    void Register<TResult>(string name, ResiliencePipeline<TResult> pipeline);
    bool Contains(string name);
    IReadOnlyCollection<string> Names { get; }
}
