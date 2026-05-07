// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Session.Configuration;
using FireflyFramework.Session.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Session.Middleware;

public sealed class FireflySessionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ISessionStore _store;
    private readonly FireflySessionOptions _options;

    public FireflySessionMiddleware(RequestDelegate next, ISessionStore store, IOptions<FireflySessionOptions> options)
    {
        _next = next;
        _store = store;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        IFireflySession? session = null;
        if (context.Request.Cookies.TryGetValue(_options.CookieName, out var sid))
            session = await _store.LoadAsync(sid, context.RequestAborted).ConfigureAwait(false);

        session ??= await _store.CreateAsync(_options.MaxInactiveInterval, context.RequestAborted).ConfigureAwait(false);
        context.Items["firefly.session"] = session;

        try { await _next(context).ConfigureAwait(false); }
        finally
        {
            await _store.SaveAsync(session, CancellationToken.None).ConfigureAwait(false);
            context.Response.Cookies.Append(_options.CookieName, session.Id, new CookieOptions
            {
                HttpOnly = true,
                Secure = _options.SecureCookie,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = _options.MaxInactiveInterval,
            });
        }
    }
}

public static class FireflySessionMiddlewareExtensions
{
    public static IApplicationBuilder UseFireflySession(this IApplicationBuilder app) =>
        app.UseMiddleware<FireflySessionMiddleware>();

    public static IFireflySession? GetFireflySession(this HttpContext ctx) =>
        ctx.Items["firefly.session"] as IFireflySession;
}
