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
