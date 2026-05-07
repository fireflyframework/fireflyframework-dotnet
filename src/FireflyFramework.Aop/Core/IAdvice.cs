// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Aop.Core;

public enum AdviceKind { Before, After, Around, AfterReturning, AfterThrowing }

/// <summary>An aspect's advice bound to a pointcut.</summary>
public interface IAdviceBinding
{
    AdviceKind Kind { get; }
    string Pointcut { get; }
    int Order { get; }
    bool Matches(JoinPoint joinPoint);
    void InvokeBefore(JoinPoint jp);
    object? InvokeAround(ProceedingJoinPoint jp);
    void InvokeAfter(JoinPoint jp);
    void InvokeAfterReturning(JoinPoint jp, object? returnValue);
    void InvokeAfterThrowing(JoinPoint jp, Exception thrown);
}

public interface IAspectRegistry
{
    void Register(IAdviceBinding binding);
    IReadOnlyList<IAdviceBinding> GetBindingsFor(JoinPoint jp);
}
