// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Net.WebSockets;
using FireflyFramework.WebSocket.Annotations;
using FireflyFramework.WebSocket.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.WebSocket.Hosting;

public static class WebSocketEndpointMapping
{
    public static IEndpointRouteBuilder MapFireflyWebSockets(this IEndpointRouteBuilder builder)
    {
        var handlers = builder.ServiceProvider.GetServices<IWebSocketHandler>().ToList();
        var registry = builder.ServiceProvider.GetRequiredService<IWebSocketSessionRegistry>();
        var loggerFactory = builder.ServiceProvider.GetRequiredService<ILoggerFactory>();

        foreach (var handler in handlers)
        {
            var attr = handler.GetType().GetCustomAttributes(typeof(WebSocketMappingAttribute), inherit: true)
                .Cast<WebSocketMappingAttribute>().FirstOrDefault();
            if (attr is null) continue;
            var path = attr.Path;
            var logger = loggerFactory.CreateLogger($"FireflyFramework.WebSocket{path}");

            builder.Map(path, async (HttpContext ctx) =>
            {
                if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
                var subProtocol = attr.SubProtocols.FirstOrDefault(p => ctx.WebSockets.WebSocketRequestedProtocols.Contains(p));
                using var ws = await ctx.WebSockets.AcceptWebSocketAsync(subProtocol).ConfigureAwait(false);

                var headers = ctx.Request.Headers.ToDictionary(h => h.Key, h => (string?)h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
                var session = new AspNetWebSocketSession(Guid.NewGuid().ToString("N"), path, headers, subProtocol, ws);
                registry.Add(session);
                try
                {
                    await handler.OnOpenAsync(session, ctx.RequestAborted).ConfigureAwait(false);
                    var buffer = new byte[8 * 1024];
                    while (ws.State == WebSocketState.Open && !ctx.RequestAborted.IsCancellationRequested)
                    {
                        var result = await ws.ReceiveAsync(buffer, ctx.RequestAborted).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        if (result.MessageType == WebSocketMessageType.Text)
                            await handler.OnTextAsync(session, System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count), ctx.RequestAborted).ConfigureAwait(false);
                        else
                            await handler.OnBinaryAsync(session, new ReadOnlyMemory<byte>(buffer, 0, result.Count), ctx.RequestAborted).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "WebSocket handler error on {Path}", path);
                    await handler.OnErrorAsync(session, ex, ctx.RequestAborted).ConfigureAwait(false);
                }
                finally
                {
                    await handler.OnCloseAsync(session, CancellationToken.None).ConfigureAwait(false);
                    registry.Remove(session.Id);
                    await session.CloseAsync(ct: CancellationToken.None).ConfigureAwait(false);
                }
            });
        }

        return builder;
    }
}
