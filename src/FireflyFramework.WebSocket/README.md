# FireflyFramework.WebSocket

Server-side WebSocket framework matching Spring's `@WebSocketMapping`
and pyfly's `@websocket_mapping`. Complements the client-side helpers
already in `FireflyFramework.Client.WebSockets`.

## What it provides

| Concept | .NET form |
|---|---|
| `@WebSocketMapping("/path")` | `[WebSocketMapping("/path")]` on an `IWebSocketHandler` |
| Lifecycle hooks | `OnOpen`, `OnText`, `OnBinary`, `OnError`, `OnClose` |
| Session abstraction | `IWebSocketSession` (Send/Close, headers, subprotocol) |
| Group/broadcast | `IWebSocketSessionRegistry.BroadcastAsync(text, group)` |

## Quick start

```csharp
[WebSocketMapping("/notifications", SubProtocols = new[] { "firefly.v1" })]
public sealed class NotificationsSocket : IWebSocketHandler
{
    public Task OnTextAsync(IWebSocketSession s, string payload, CancellationToken ct)
    {
        // echo
        return s.SendTextAsync($"ack:{payload}", ct);
    }
}

services.AddFireflyWebSockets()
        .AddWebSocketHandler<NotificationsSocket>();

app.UseWebSockets();        // ASP.NET Core middleware
app.MapFireflyWebSockets(); // discovers and binds [WebSocketMapping] handlers
```

To broadcast from elsewhere in the app:

```csharp
public PriceTicker(IWebSocketSessionRegistry registry) { _registry = registry; }
await _registry.BroadcastAsync("""{"type":"tick","price":42.10}""");
```
