// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Aop.Annotations;
using FireflyFramework.Aop.Core;
using FireflyFramework.Aop.DependencyInjection;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class AopTests
{
    [Fact]
    public void Around_advice_observes_arguments_and_can_short_circuit()
    {
        var aspect = new TimingAspect();
        var registry = AspectRegistry.FromAspects(new[] { aspect });

        var target = new Calculator();
        var method = typeof(Calculator).GetMethod(nameof(Calculator.Add))!;
        var jp = new JoinPoint(target, target.GetType(), method, new object?[] { 2, 3 }, "Calculator.Add");

        var result = AdviceInvoker.Run(registry, jp, args => method.Invoke(target, args));
        result.Should().Be(5);
        aspect.Calls.Should().Be(1);
    }

    [Fact]
    public void Pointcut_matches_execution_pattern()
    {
        var target = new Calculator();
        var method = typeof(Calculator).GetMethod(nameof(Calculator.Add))!;
        var jp = new JoinPoint(target, target.GetType(), method, new object?[] { 1, 1 }, "Calculator.Add");

        PointcutMatcher.Matches("execution(* FireflyFramework.Tests.Calculator.* (..))", jp).Should().BeTrue();
        PointcutMatcher.Matches("within(FireflyFramework.Tests.Calculator)", jp).Should().BeTrue();
        PointcutMatcher.Matches("execution(* SomeOther.Calculator.* (..))", jp).Should().BeFalse();
    }

    [Fact]
    public void AfterThrowing_advice_runs_when_target_throws()
    {
        var aspect = new RecordingAspect();
        var registry = AspectRegistry.FromAspects(new[] { aspect });

        var target = new Calculator();
        var method = typeof(Calculator).GetMethod(nameof(Calculator.Boom))!;
        var jp = new JoinPoint(target, target.GetType(), method, Array.Empty<object?>(), "Calculator.Boom");

        Action act = () => AdviceInvoker.Run(registry, jp, _ => method.Invoke(target, null));
        act.Should().Throw<System.Reflection.TargetInvocationException>();
        aspect.Throws.Should().Be(1);
    }
}

public sealed class Calculator
{
    public int Add(int a, int b) => a + b;
    public void Boom() => throw new InvalidOperationException("boom");
}

[Aspect]
public sealed class TimingAspect : IFireflyAspect
{
    public int Calls { get; private set; }

    [Around("execution(* FireflyFramework.Tests.Calculator.Add (..))")]
    public object? Time(ProceedingJoinPoint jp)
    {
        Calls++;
        return jp.Proceed();
    }
}

[Aspect]
public sealed class RecordingAspect : IFireflyAspect
{
    public int Throws { get; private set; }

    [AfterThrowing("execution(* FireflyFramework.Tests.Calculator.Boom (..))")]
    public void Capture(JoinPoint jp) => Throws++;
}
