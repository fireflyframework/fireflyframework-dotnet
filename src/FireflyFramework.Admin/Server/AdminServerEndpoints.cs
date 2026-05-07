// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Admin.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Admin.Server;

public static class AdminServerEndpoints
{
    public static IEndpointRouteBuilder MapFireflyAdminServer(this IEndpointRouteBuilder builder)
    {
        var options = builder.ServiceProvider.GetRequiredService<IOptions<FireflyAdminServerOptions>>().Value;
        var registry = builder.ServiceProvider.GetRequiredService<IInstanceRegistry>();
        var basePath = options.BasePath.TrimEnd('/');

        builder.MapPost($"{basePath}/instances", async (HttpContext ctx) =>
        {
            var instance = await ctx.Request.ReadFromJsonAsync<AdminInstance>(ctx.RequestAborted).ConfigureAwait(false);
            if (instance is null) { ctx.Response.StatusCode = 400; return; }
            var stored = registry.Register(instance);
            await ctx.Response.WriteAsJsonAsync(stored).ConfigureAwait(false);
        });

        builder.MapPut($"{basePath}/instances/{{id}}/heartbeat", async (HttpContext ctx, string id) =>
        {
            var status = ctx.Request.Query.TryGetValue("status", out var s) ? s.ToString() : "UP";
            var inst = registry.Heartbeat(id, status);
            if (inst is null) { ctx.Response.StatusCode = 404; return; }
            await ctx.Response.WriteAsJsonAsync(inst).ConfigureAwait(false);
        });

        builder.MapDelete($"{basePath}/instances/{{id}}", (string id) =>
            registry.Deregister(id) ? Results.NoContent() : Results.NotFound());

        builder.MapGet($"{basePath}/instances", () => Results.Ok(registry.All()));
        builder.MapGet($"{basePath}/instances/{{id}}", (string id) =>
            registry.Get(id) is { } inst ? Results.Ok(inst) : Results.NotFound());

        return builder;
    }
}
