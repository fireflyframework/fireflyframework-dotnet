// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.WebSocket.Core;

public interface IWebSocketSession
{
    string Id { get; }
    string Path { get; }
    IReadOnlyDictionary<string, string?> Headers { get; }
    string? SubProtocol { get; }
    bool IsOpen { get; }

    Task SendTextAsync(string payload, CancellationToken ct);
    Task SendBinaryAsync(ReadOnlyMemory<byte> payload, CancellationToken ct);
    Task CloseAsync(string? reason = null, CancellationToken ct = default);
}

public interface IWebSocketHandler
{
    Task OnOpenAsync(IWebSocketSession session, CancellationToken ct) => Task.CompletedTask;
    Task OnTextAsync(IWebSocketSession session, string payload, CancellationToken ct) => Task.CompletedTask;
    Task OnBinaryAsync(IWebSocketSession session, ReadOnlyMemory<byte> payload, CancellationToken ct) => Task.CompletedTask;
    Task OnErrorAsync(IWebSocketSession session, Exception ex, CancellationToken ct) => Task.CompletedTask;
    Task OnCloseAsync(IWebSocketSession session, CancellationToken ct) => Task.CompletedTask;
}

public interface IWebSocketSessionRegistry
{
    void Add(IWebSocketSession session, params string[] groups);
    void Remove(string sessionId);
    IEnumerable<IWebSocketSession> All();
    IEnumerable<IWebSocketSession> InGroup(string group);
    Task BroadcastAsync(string text, string? group = null, CancellationToken ct = default);
}
