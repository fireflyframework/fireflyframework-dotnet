// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Text.Json;
using FireflyFramework.Actuator.Configuration;
using FireflyFramework.Actuator.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Actuator.Hosting;

public static class ActuatorEndpointRouteBuilder
{
    public static IEndpointRouteBuilder MapFireflyActuator(this IEndpointRouteBuilder builder)
    {
        var options = builder.ServiceProvider.GetRequiredService<IOptions<FireflyActuatorOptions>>().Value;
        var endpoints = builder.ServiceProvider.GetServices<IActuatorEndpoint>().ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);

        builder.MapGet(options.BasePath, async ctx =>
        {
            var links = endpoints.Keys
                .Where(id => options.ExposeEndpoints.Contains(id, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(id => id, id => new { href = $"{options.BasePath}/{id}", templated = false });
            await ctx.Response.WriteAsJsonAsync(new { _links = links }).ConfigureAwait(false);
        });

        builder.MapGet($"{options.BasePath}/{{id}}", async ctx =>
        {
            var id = (string)ctx.Request.RouteValues["id"]!;
            if (!options.ExposeEndpoints.Contains(id, StringComparer.OrdinalIgnoreCase) || !endpoints.TryGetValue(id, out var ep))
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            var parameters = ctx.Request.Query.ToDictionary(q => q.Key, q => (string?)q.Value.ToString());
            var payload = await ep.InvokeAsync(parameters, ctx.RequestAborted).ConfigureAwait(false);
            ctx.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(ctx.Response.Body, payload, new JsonSerializerOptions { WriteIndented = false }).ConfigureAwait(false);
        });

        return builder;
    }
}
