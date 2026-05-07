// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.WebSocket.Annotations;

/// <summary>
/// Marks an <see cref="Core.IWebSocketHandler"/> implementation with the URL path
/// it serves. Mirrors Spring <c>@WebSocketMapping</c> / pyfly <c>@websocket_mapping</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class WebSocketMappingAttribute : Attribute
{
    public WebSocketMappingAttribute(string path) { Path = path; }
    public string Path { get; }
    public string[] AllowedOrigins { get; init; } = Array.Empty<string>();
    public string[] SubProtocols { get; init; } = Array.Empty<string>();
}
