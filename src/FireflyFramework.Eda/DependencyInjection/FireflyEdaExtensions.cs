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
