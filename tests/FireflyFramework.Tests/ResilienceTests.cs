// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Kernel.Exceptions;
using FireflyFramework.Resilience.Configuration;
using FireflyFramework.Resilience.Core;
using FireflyFramework.Resilience.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class ResilienceTests
{
    [Fact]
    public void AddFireflyResilience_registers_named_pipelines_from_config()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Firefly:Resilience:Retries:default:MaxAttempts"] = "5",
                ["Firefly:Resilience:Retries:default:Delay"] = "00:00:00.1",
                ["Firefly:Resilience:CircuitBreakers:default:FailureRateThreshold"] = "0.4",
                ["Firefly:Resilience:RateLimiters:default:PermitLimit"] = "50",
            }).Build();

        var sp = new ServiceCollection().AddFireflyResilience(config).BuildServiceProvider();
        var registry = sp.GetRequiredService<IResilienceRegistry>();

        registry.Contains("default").Should().BeTrue();
        registry.GetPipeline("default").Should().NotBeNull();
        registry.Names.Should().Contain("default");
    }

    [Fact]
    public void Registry_throws_kernel_exception_for_missing_pipeline()
    {
        var registry = new DefaultResilienceRegistry();
        Action act = () => registry.GetPipeline("missing");
        act.Should().Throw<FireflyException>().Where(e => e.ErrorCode == "RESILIENCE_NOT_FOUND");
    }

    [Fact]
    public async Task Retry_pipeline_retries_until_success()
    {
        var pipeline = ResiliencePipelineFactory.BuildRetry("retry", new RetryOptions
        {
            MaxAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(1),
            BackoffType = "Constant",
            UseJitter = false,
        });

        var attempts = 0;
        var result = await pipeline.ExecuteAsync(_ =>
        {
            attempts++;
            if (attempts < 2) throw new InvalidOperationException("transient");
            return ValueTask.FromResult(42);
        });

        result.Should().Be(42);
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task TimeLimiter_pipeline_cancels_long_calls()
    {
        var pipeline = ResiliencePipelineFactory.BuildTimeLimiter("timeout", new TimeLimiterOptions
        {
            Timeout = TimeSpan.FromMilliseconds(50),
        });

        Func<Task> act = async () => await pipeline.ExecuteAsync(async ct =>
        {
            await Task.Delay(2000, ct);
        });

        await act.Should().ThrowAsync<Polly.Timeout.TimeoutRejectedException>();
    }
}
