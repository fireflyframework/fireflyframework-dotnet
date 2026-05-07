using Grpc.Net.Client;

namespace FireflyFramework.Client.Grpc;

/// <summary>Fluent builder for gRPC channels with optional auth and retry policies.</summary>
public sealed class GrpcClientBuilder
{
    private string? _address;
    private GrpcChannelOptions _options = new();

    public static GrpcClientBuilder Create() => new();

    public GrpcClientBuilder WithAddress(string address) { _address = address; return this; }

    public GrpcClientBuilder WithOptions(Action<GrpcChannelOptions> cfg) { cfg(_options); return this; }

    public GrpcChannel Build() => GrpcChannel.ForAddress(_address ?? throw new InvalidOperationException("Address is required"), _options);
}
