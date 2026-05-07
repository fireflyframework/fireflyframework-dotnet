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
