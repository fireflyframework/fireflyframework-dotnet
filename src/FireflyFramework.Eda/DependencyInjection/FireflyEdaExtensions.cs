using FireflyFramework.Eda.Configuration;
using FireflyFramework.Eda.Consumer;
using FireflyFramework.Eda.Events;
using FireflyFramework.Eda.Publisher;
using FireflyFramework.Eda.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FireflyFramework.Eda.DependencyInjection;

public static class FireflyEdaExtensions
{
    public static IServiceCollection AddFireflyEda(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<EdaOptions>().Bind(config.GetSection(EdaOptions.SectionName));
        services.TryAddSingleton<IMessageSerializer, JsonMessageSerializer>();

        services.AddSingleton<InMemoryEventBus>();
        services.AddSingleton<InMemoryEventPublisher>();
        services.AddSingleton<InMemoryEventConsumer>();

        services.AddSingleton<IEventPublisher>(sp =>
        {
            var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EdaOptions>>().Value;
            return opt.DefaultPublisher switch
            {
                PublisherType.Kafka => sp.GetRequiredService<KafkaEventPublisher>(),
                PublisherType.InMemory => sp.GetRequiredService<InMemoryEventPublisher>(),
                _ => sp.GetRequiredService<InMemoryEventPublisher>(),
            };
        });

        services.AddSingleton<KafkaEventPublisher>();

        services.AddSingleton<IEventConsumer>(sp => sp.GetRequiredService<InMemoryEventConsumer>());

        return services;
    }
}
