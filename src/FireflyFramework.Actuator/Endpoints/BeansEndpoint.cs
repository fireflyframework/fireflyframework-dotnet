// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Actuator.Core;

namespace FireflyFramework.Actuator.Endpoints;

public sealed class BeansEndpoint : IActuatorEndpoint
{
    private readonly IReadOnlyList<BeanRegistration> _registrations;

    public BeansEndpoint(IReadOnlyList<BeanRegistration> registrations) { _registrations = registrations; }

    public string Id => "beans";

    public Task<object?> InvokeAsync(IDictionary<string, string?> parameters, CancellationToken ct)
    {
        var beans = _registrations.Select(r => new
        {
            type = r.ServiceType,
            implementation = r.ImplementationType,
            lifetime = r.Lifetime,
            isKeyed = r.IsKeyed,
        });
        return Task.FromResult<object?>(new { contexts = new { application = new { beans } } });
    }

    /// <summary>Snapshot of a single registration captured at <c>AddFireflyActuator</c> time.</summary>
    public sealed record BeanRegistration(string ServiceType, string? ImplementationType, string Lifetime, bool IsKeyed);
}
