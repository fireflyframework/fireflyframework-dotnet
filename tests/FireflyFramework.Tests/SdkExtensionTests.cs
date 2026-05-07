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

using FireflyFramework.Callbacks.Interfaces;
using FireflyFramework.Callbacks.Sdk;
using FireflyFramework.RuleEngine.Sdk;
using FireflyFramework.Webhooks.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FireflyFramework.Tests;

public class SdkExtensionTests
{
    [Fact]
    public void AddCallbackClient_ResolvesInterface_ToTypedHttpClientImpl()
    {
        var services = new ServiceCollection();
        services.AddCallbackClient(new Uri("https://callbacks.svc.local"));
        using var sp = services.BuildServiceProvider();

        var resolved = sp.GetRequiredService<ICallbackClient>();
        Assert.IsType<CallbackClient>(resolved);
    }

    [Fact]
    public void AddWebhookClient_ResolvesInterface_ToTypedHttpClientImpl()
    {
        var services = new ServiceCollection();
        services.AddWebhookClient(new Uri("https://webhooks.svc.local"));
        using var sp = services.BuildServiceProvider();

        var resolved = sp.GetRequiredService<IWebhookClient>();
        Assert.IsType<WebhookClient>(resolved);
    }

    [Fact]
    public void AddRuleEngineClient_ResolvesInterface_ToTypedHttpClientImpl()
    {
        var services = new ServiceCollection();
        services.AddRuleEngineClient(new Uri("https://rules.svc.local"));
        using var sp = services.BuildServiceProvider();

        var resolved = sp.GetRequiredService<IRuleEngineClient>();
        Assert.IsType<RuleEngineClient>(resolved);
    }

    [Fact]
    public void CallbackClient_AcceptsTypedHttpClient_AndIsNonNull()
    {
        var http = new HttpClient { BaseAddress = new Uri("https://callbacks.svc.local") };
        var client = new CallbackClient(http);
        Assert.NotNull(client);
    }
}
