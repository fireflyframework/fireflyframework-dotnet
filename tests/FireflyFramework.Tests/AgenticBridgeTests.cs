// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Net;
using System.Net.Http.Json;
using FireflyFramework.AgenticBridge.Adapters;
using FireflyFramework.AgenticBridge.Core;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class AgenticBridgeTests
{
    [Fact]
    public async Task RestAgenticClient_invokes_remote_agent_and_returns_payload()
    {
        var handler = new StubHandler((req, _) =>
        {
            req.RequestUri!.PathAndQuery.Should().Be("/agents/sample/invoke");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new AgentInvocationResult(
                    ConversationId: "conv-1",
                    Output: "the answer is 42",
                    Tools: Array.Empty<AgentToolInvocation>(),
                    Metadata: new Dictionary<string, object?> { ["model"] = "scripted" })),
            };
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://agents.local/") };
        var client = new RestAgenticClient(http);

        var result = await client.InvokeAsync(new AgentInvocation("sample", "ask"), CancellationToken.None);

        result.Output.Should().Be("the answer is 42");
        result.ConversationId.Should().Be("conv-1");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _handler;
        public StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> h) { _handler = h; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(_handler(request, ct));
    }
}
