// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Net.WebSockets;
using System.Text;

namespace FireflyFramework.WebSocket.Core;

internal sealed class AspNetWebSocketSession : IWebSocketSession
{
    private readonly System.Net.WebSockets.WebSocket _ws;

    public AspNetWebSocketSession(string id, string path, IReadOnlyDictionary<string, string?> headers, string? subProtocol, System.Net.WebSockets.WebSocket ws)
    {
        Id = id;
        Path = path;
        Headers = headers;
        SubProtocol = subProtocol;
        _ws = ws;
    }

    public string Id { get; }
    public string Path { get; }
    public IReadOnlyDictionary<string, string?> Headers { get; }
    public string? SubProtocol { get; }
    public bool IsOpen => _ws.State == WebSocketState.Open;

    public Task SendTextAsync(string payload, CancellationToken ct) =>
        _ws.SendAsync(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, endOfMessage: true, ct);

    public Task SendBinaryAsync(ReadOnlyMemory<byte> payload, CancellationToken ct) =>
        _ws.SendAsync(payload, WebSocketMessageType.Binary, endOfMessage: true, ct).AsTask();

    public async Task CloseAsync(string? reason = null, CancellationToken ct = default)
    {
        if (_ws.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, reason ?? "closed", ct).ConfigureAwait(false);
    }
}
