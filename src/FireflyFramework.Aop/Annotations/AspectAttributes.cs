// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Aop.Annotations;

/// <summary>Marks a class as an aspect (collection of advices). Mirrors Spring <c>@Aspect</c>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class AspectAttribute : Attribute
{
    public int Order { get; init; }
}

/// <summary>Advice executed before the matched method.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class BeforeAttribute : Attribute
{
    public BeforeAttribute(string pointcut) { Pointcut = pointcut; }
    public string Pointcut { get; }
}

/// <summary>Advice executed after the matched method (regardless of outcome).</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AfterAttribute : Attribute
{
    public AfterAttribute(string pointcut) { Pointcut = pointcut; }
    public string Pointcut { get; }
}

/// <summary>Wraps execution; the advice receives a proceed delegate.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AroundAttribute : Attribute
{
    public AroundAttribute(string pointcut) { Pointcut = pointcut; }
    public string Pointcut { get; }
}

/// <summary>Advice executed after the method returns successfully.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AfterReturningAttribute : Attribute
{
    public AfterReturningAttribute(string pointcut) { Pointcut = pointcut; }
    public string Pointcut { get; }
    public string? Returning { get; init; }
}

/// <summary>Advice executed after the method throws an exception.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AfterThrowingAttribute : Attribute
{
    public AfterThrowingAttribute(string pointcut) { Pointcut = pointcut; }
    public string Pointcut { get; }
    public string? Throwing { get; init; }
}
