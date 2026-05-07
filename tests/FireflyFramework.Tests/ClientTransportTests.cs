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

using System.Net;
using System.Net.Http.Json;
using System.ServiceModel;
using System.Text;
using FireflyFramework.Client;
using FireflyFramework.Client.Rest;
using FireflyFramework.Client.Soap;
using FireflyFramework.Client.WebSockets;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public class ClientTransportTests
{
    // ───── REST: HttpRestClient over a stub HttpClient ─────

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public List<HttpRequestMessage> Calls { get; } = new();

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add(request);
            return Task.FromResult(_handler(request));
        }
    }

    private sealed record Greeting(string Hello);

    [Fact]
    public async Task HttpRestClient_GetAsync_deserialises_response()
    {
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new Greeting("world")),
        });
        var http = new HttpClient(stub) { BaseAddress = new Uri("https://api.example.com/") };
        var client = new HttpRestClient(http);

        var greeting = await client.GetAsync<Greeting>("/greet");

        greeting!.Hello.Should().Be("world");
        stub.Calls.Single().Method.Should().Be(HttpMethod.Get);
    }

    [Fact]
    public async Task HttpRestClient_PostAsync_sends_body_and_returns_response()
    {
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(new Greeting("posted")),
        });
        var http = new HttpClient(stub) { BaseAddress = new Uri("https://api.example.com/") };
        var client = new HttpRestClient(http);

        var greeting = await client.PostAsync<Greeting>("/greet", new { name = "alice" });

        greeting!.Hello.Should().Be("posted");
        stub.Calls.Single().Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task HttpRestClient_DeleteAsync_returns_true_on_2xx()
    {
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var http = new HttpClient(stub) { BaseAddress = new Uri("https://api.example.com/") };
        var client = new HttpRestClient(http);

        (await client.DeleteAsync("/x")).Should().BeTrue();
    }

    [Fact]
    public async Task HttpRestClient_DeleteAsync_returns_false_on_4xx()
    {
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var http = new HttpClient(stub) { BaseAddress = new Uri("https://api.example.com/") };
        var client = new HttpRestClient(http);

        (await client.DeleteAsync("/x")).Should().BeFalse();
    }

    // ───── SOAP: builder configuration ─────

    [ServiceContract]
    public interface ISampleSoap
    {
        [OperationContract]
        string Echo(string value);
    }

    [Fact]
    public void Soap_builder_throws_when_endpoint_missing()
    {
        FluentActions.Invoking(() => ServiceClient.Soap<ISampleSoap>().Build())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Endpoint address is required*");
    }

    [Fact]
    public void Soap_builder_creates_channel_with_basic_http()
    {
        var channel = ServiceClient.Soap<ISampleSoap>()
            .WithEndpointAddress("http://localhost:8080/svc")
            .WithTransport(SoapTransport.Http)
            .WithTimeout(TimeSpan.FromSeconds(15))
            .Build();

        channel.Should().NotBeNull();
        // Channel is a transparent proxy — invocation would go to the server.
    }

    [Fact]
    public void Soap_builder_creates_channel_with_basic_auth()
    {
        var channel = ServiceClient.Soap<ISampleSoap>()
            .WithEndpointAddress("https://localhost:8443/svc")
            .WithTransport(SoapTransport.Https)
            .WithBasicAuth("user", "pass")
            .Build();

        channel.Should().NotBeNull();
    }

    // ───── WebSocket: helper state ─────

    [Fact]
    public void WebSocketHelper_starts_in_none_state()
    {
        var ws = ServiceClient.WebSocket();
        ws.State.Should().Be(System.Net.WebSockets.WebSocketState.None);
    }

    [Fact]
    public async Task WebSocketHelper_disposes_cleanly_when_never_connected()
    {
        var ws = ServiceClient.WebSocket();
        await ws.DisposeAsync();
        // No exception means the underlying ClientWebSocket was disposed safely.
    }

    [Fact]
    public void WebSocketFrame_AsText_decodes_utf8()
    {
        var frame = new WebSocketFrame(System.Net.WebSockets.WebSocketMessageType.Text,
            Encoding.UTF8.GetBytes("héllo"), EndOfMessage: true);
        frame.AsText().Should().Be("héllo");
    }
}
