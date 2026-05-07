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
