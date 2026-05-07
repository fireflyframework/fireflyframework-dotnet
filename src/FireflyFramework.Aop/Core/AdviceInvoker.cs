// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Aop.Core;

/// <summary>
/// Manual weaving entry point for callers that don't want to set up DispatchProxy.
/// Useful inside CQRS handlers or saga steps that already wrap their own invocation.
/// </summary>
public static class AdviceInvoker
{
    public static object? Run(IAspectRegistry registry, JoinPoint jp, Func<object?[], object?> proceed)
    {
        var bindings = registry.GetBindingsFor(jp);
        foreach (var b in bindings.Where(b => b.Kind == AdviceKind.Before)) b.InvokeBefore(jp);

        var around = bindings.FirstOrDefault(b => b.Kind == AdviceKind.Around);
        var pjp = new ProceedingJoinPoint(jp, proceed);
        try
        {
            var result = around is null ? proceed(jp.Arguments) : around.InvokeAround(pjp);
            foreach (var b in bindings.Where(b => b.Kind == AdviceKind.AfterReturning)) b.InvokeAfterReturning(jp, result);
            return result;
        }
        catch (Exception ex)
        {
            foreach (var b in bindings.Where(b => b.Kind == AdviceKind.AfterThrowing)) b.InvokeAfterThrowing(jp, ex);
            throw;
        }
        finally
        {
            foreach (var b in bindings.Where(b => b.Kind == AdviceKind.After)) b.InvokeAfter(jp);
        }
    }
}
