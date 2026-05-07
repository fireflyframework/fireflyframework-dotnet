using Microsoft.Extensions.DependencyInjection;

namespace FireflyFramework.Callbacks.Sdk;

public static class CallbackClientExtensions
{
    /// <summary>
    /// Registers <see cref="ICallbackClient"/> backed by a typed <see cref="HttpClient"/>
    /// targeting the supplied base address. Mirrors the
    /// <c>AddOrdersServiceClient</c> pattern from the canonical service Sdk in
    /// <c>samples/FireflyFramework.Samples.OrdersService.Sdk</c>.
    /// </summary>
    public static IHttpClientBuilder AddCallbackClient(this IServiceCollection services, Uri baseAddress) =>
        services.AddHttpClient<ICallbackClient, CallbackClient>(http => http.BaseAddress = baseAddress);
}
