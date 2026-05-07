// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Reflection;
using FireflyFramework.Aop.Annotations;

namespace FireflyFramework.Aop.Core;

public sealed class AspectRegistry : IAspectRegistry
{
    private readonly List<IAdviceBinding> _bindings = new();

    public void Register(IAdviceBinding binding) { _bindings.Add(binding); _bindings.Sort((a, b) => a.Order.CompareTo(b.Order)); }

    public IReadOnlyList<IAdviceBinding> GetBindingsFor(JoinPoint jp) =>
        _bindings.Where(b => PointcutMatcher.Matches(b.Pointcut, jp)).ToList();

    public static AspectRegistry FromAspects(IEnumerable<object> aspects)
    {
        var reg = new AspectRegistry();
        foreach (var aspect in aspects)
        {
            var aspectAttr = aspect.GetType().GetCustomAttribute<AspectAttribute>();
            if (aspectAttr is null) continue;
            var order = aspectAttr.Order;
            foreach (var m in aspect.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (m.GetCustomAttribute<BeforeAttribute>() is { } b) reg.Register(new ReflectiveAdviceBinding(AdviceKind.Before, b.Pointcut, order, aspect, m));
                if (m.GetCustomAttribute<AfterAttribute>() is { } a) reg.Register(new ReflectiveAdviceBinding(AdviceKind.After, a.Pointcut, order, aspect, m));
                if (m.GetCustomAttribute<AroundAttribute>() is { } ar) reg.Register(new ReflectiveAdviceBinding(AdviceKind.Around, ar.Pointcut, order, aspect, m));
                if (m.GetCustomAttribute<AfterReturningAttribute>() is { } arr) reg.Register(new ReflectiveAdviceBinding(AdviceKind.AfterReturning, arr.Pointcut, order, aspect, m));
                if (m.GetCustomAttribute<AfterThrowingAttribute>() is { } at) reg.Register(new ReflectiveAdviceBinding(AdviceKind.AfterThrowing, at.Pointcut, order, aspect, m));
            }
        }
        return reg;
    }
}

internal sealed class ReflectiveAdviceBinding : IAdviceBinding
{
    private readonly object _aspect;
    private readonly MethodInfo _method;

    public ReflectiveAdviceBinding(AdviceKind kind, string pointcut, int order, object aspect, MethodInfo method)
    { Kind = kind; Pointcut = pointcut; Order = order; _aspect = aspect; _method = method; }

    public AdviceKind Kind { get; }
    public string Pointcut { get; }
    public int Order { get; }

    public bool Matches(JoinPoint jp) => PointcutMatcher.Matches(Pointcut, jp);

    public void InvokeBefore(JoinPoint jp) { if (Kind == AdviceKind.Before) Invoke(jp); }
    public object? InvokeAround(ProceedingJoinPoint jp) => Kind == AdviceKind.Around ? _method.Invoke(_aspect, new object?[] { jp }) : jp.Proceed();
    public void InvokeAfter(JoinPoint jp) { if (Kind == AdviceKind.After) Invoke(jp); }
    public void InvokeAfterReturning(JoinPoint jp, object? returnValue) { if (Kind == AdviceKind.AfterReturning) Invoke(jp, returnValue); }
    public void InvokeAfterThrowing(JoinPoint jp, Exception thrown) { if (Kind == AdviceKind.AfterThrowing) Invoke(jp, thrown); }

    private void Invoke(JoinPoint jp, params object?[] extra)
    {
        var ps = _method.GetParameters();
        var args = ps.Length switch
        {
            0 => Array.Empty<object?>(),
            1 when ps[0].ParameterType == typeof(JoinPoint) => new object?[] { jp },
            _ => new object?[] { jp }.Concat(extra).Take(ps.Length).ToArray(),
        };
        _method.Invoke(_aspect, args);
    }
}
