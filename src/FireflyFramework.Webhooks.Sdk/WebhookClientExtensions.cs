using Microsoft.Extensions.DependencyInjection;

namespace FireflyFramework.Webhooks.Sdk;

public static class WebhookClientExtensions
{
    /// <summary>
    /// Registers <see cref="IWebhookClient"/> backed by a typed <see cref="HttpClient"/>
    /// targeting the supplied base address.
    /// </summary>
    public static IHttpClientBuilder AddWebhookClient(this IServiceCollection services, Uri baseAddress) =>
        services.AddHttpClient<IWebhookClient, WebhookClient>(http => http.BaseAddress = baseAddress);
}
