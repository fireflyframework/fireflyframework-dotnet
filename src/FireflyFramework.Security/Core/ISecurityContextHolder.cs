// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Security.Core;

/// <summary>
/// Async-local holder that exposes the current <see cref="SecurityContext"/>.
/// Mirrors Spring <c>SecurityContextHolder</c> / pyfly <c>SecurityContextHolder</c>.
/// </summary>
public interface ISecurityContextHolder
{
    SecurityContext Current { get; }
    IDisposable Push(SecurityContext context);
}

public sealed class AsyncLocalSecurityContextHolder : ISecurityContextHolder
{
    private static readonly AsyncLocal<SecurityContext?> _current = new();

    public SecurityContext Current => _current.Value ?? SecurityContext.Anonymous;

    public IDisposable Push(SecurityContext context)
    {
        var prev = _current.Value;
        _current.Value = context;
        return new Pop(() => _current.Value = prev);
    }

    private sealed class Pop : IDisposable
    {
        private Action? _action;
        public Pop(Action a) => _action = a;
        public void Dispose() { _action?.Invoke(); _action = null; }
    }
}
