// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Actuator.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;

namespace FireflyFramework.Actuator.Endpoints;

public sealed class MappingsEndpoint : IActuatorEndpoint
{
    private readonly EndpointDataSource? _dataSource;

    /// <summary>
    /// <paramref name="dataSource"/> is optional: outside an ASP.NET Core host
    /// (test rigs, console hosts, hosted services without routing) the actuator
    /// still loads, and this endpoint reports "routing not available" instead of
    /// failing the entire registration graph.
    /// </summary>
    public MappingsEndpoint(EndpointDataSource? dataSource = null) { _dataSource = dataSource; }

    public string Id => "mappings";

    public Task<object?> InvokeAsync(IDictionary<string, string?> parameters, CancellationToken ct)
    {
        if (_dataSource is null)
        {
            return Task.FromResult<object?>(new { contexts = new { application = new { endpoints = Array.Empty<object>(), note = "routing not available" } } });
        }

        var endpoints = _dataSource.Endpoints.OfType<RouteEndpoint>().Select(e => new
        {
            displayName = e.DisplayName,
            pattern = e.RoutePattern.RawText,
            order = e.Order,
            methods = e.Metadata.OfType<HttpMethodMetadata>().SelectMany(m => m.HttpMethods).Distinct().ToArray(),
        });
        return Task.FromResult<object?>(new { contexts = new { application = new { endpoints } } });
    }
}
