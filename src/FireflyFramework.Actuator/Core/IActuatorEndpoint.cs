// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Actuator.Core;

/// <summary>
/// Spring Boot Actuator endpoint port. Each endpoint produces a JSON-serializable
/// payload that the actuator router exposes under <c>/actuator/{Id}</c>.
/// Mirrors pyfly <c>ActuatorEndpoint</c>.
/// </summary>
public interface IActuatorEndpoint
{
    /// <summary>Endpoint id (e.g. <c>info</c>, <c>env</c>, <c>beans</c>).</summary>
    string Id { get; }
    /// <summary>True if the endpoint is exposed on the actuator router.</summary>
    bool Enabled => true;
    /// <summary>Produce the endpoint payload.</summary>
    Task<object?> InvokeAsync(IDictionary<string, string?> parameters, CancellationToken ct);
}
