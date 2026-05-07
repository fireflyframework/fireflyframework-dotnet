// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Reflection;

namespace FireflyFramework.Aop.Core;

/// <summary>
/// Reflective view of an intercepted method invocation. Mirrors Spring
/// <c>JoinPoint</c> / pyfly <c>JoinPoint</c>.
/// </summary>
public sealed record JoinPoint(
    object Target,
    Type TargetType,
    MethodInfo Method,
    object?[] Arguments,
    string MethodSignature)
{
    public string MethodName => Method.Name;
    public string TypeName => TargetType.FullName ?? TargetType.Name;
}

/// <summary>Around-advice variant: gives the aspect a way to invoke (or skip) the call.</summary>
public sealed class ProceedingJoinPoint
{
    private readonly Func<object?[], object?> _proceed;
    public ProceedingJoinPoint(JoinPoint jp, Func<object?[], object?> proceed)
    {
        Inner = jp;
        _proceed = proceed;
    }

    public JoinPoint Inner { get; }
    public object Target => Inner.Target;
    public MethodInfo Method => Inner.Method;
    public object?[] Arguments => Inner.Arguments;

    public object? Proceed() => _proceed(Inner.Arguments);
    public object? Proceed(object?[] newArgs) => _proceed(newArgs);
}
