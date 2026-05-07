// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using FireflyFramework.Client.Grpc;
using FireflyFramework.Client.Rest;
using FireflyFramework.Client.Soap;
using FireflyFramework.Client.WebSockets;

namespace FireflyFramework.Client;

/// <summary>
/// Top-level entry point. Mirrors Java <c>ServiceClient</c>'s static factories and
/// returns a fluent builder per protocol.
/// </summary>
public static class ServiceClient
{
    public static RestClientBuilder Rest() => RestClientBuilder.Create();

    public static GrpcClientBuilder Grpc() => GrpcClientBuilder.Create();

    public static SoapClientBuilder<TChannel> Soap<TChannel>() where TChannel : class =>
        SoapClientBuilder<TChannel>.Create();

    /// <summary>Creates a fresh <see cref="WebSocketClientHelper"/>; call <c>ConnectAsync</c> on it.</summary>
    public static WebSocketClientHelper WebSocket() => new();
}
