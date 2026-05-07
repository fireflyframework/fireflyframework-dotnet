using Microsoft.Extensions.DependencyInjection;

namespace FireflyFramework.RuleEngine.Sdk;

public static class RuleEngineClientExtensions
{
    /// <summary>
    /// Registers <see cref="IRuleEngineClient"/> backed by a typed <see cref="HttpClient"/>
    /// targeting the supplied base address.
    /// </summary>
    public static IHttpClientBuilder AddRuleEngineClient(this IServiceCollection services, Uri baseAddress) =>
        services.AddHttpClient<IRuleEngineClient, RuleEngineClient>(http => http.BaseAddress = baseAddress);
}
