using Microsoft.Extensions.DependencyInjection;

namespace FireflyFramework.Samples.OrdersService.Sdk;

public static class OrdersServiceClientExtensions
{
    public static IHttpClientBuilder AddOrdersServiceClient(this IServiceCollection services, Uri baseAddress) =>
        services.AddHttpClient<IOrdersServiceClient, OrdersServiceClient>(http =>
        {
            http.BaseAddress = baseAddress;
        });
}
