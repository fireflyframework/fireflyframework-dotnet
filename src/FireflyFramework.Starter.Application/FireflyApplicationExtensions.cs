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

using FireflyFramework.Actuator.DependencyInjection;
using FireflyFramework.Aop.DependencyInjection;
using FireflyFramework.I18n.DependencyInjection;
using FireflyFramework.Plugins.Api;
using FireflyFramework.Plugins.Core;
using FireflyFramework.Resilience.DependencyInjection;
using FireflyFramework.Scheduling.DependencyInjection;
using FireflyFramework.Security.DependencyInjection;
using FireflyFramework.Session.DependencyInjection;
using FireflyFramework.Starter.Core;
using FireflyFramework.Web.Logging;
using FireflyFramework.WebSocket.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FireflyFramework.Starter.Application;

/// <summary>
/// Application-tier starter: includes everything in the core starter plus the plugin
/// extension registry, security, actuator, scheduling, session, i18n, AOP, and the
/// WebSocket session registry. IDP / orchestration / rule-engine wiring remains
/// application-specific because each service picks one adapter — register them in your
/// composition root.
/// </summary>
public static class FireflyApplicationExtensions
{
    public static IServiceCollection AddFireflyApplication(
        this IServiceCollection services,
        IConfiguration config,
        string serviceName,
        string serviceVersion = "1.0.0",
        params System.Reflection.Assembly[] cqrsAssemblies)
    {
        FireflyBanner.Print(typeof(FireflyApplicationExtensions).Assembly, serviceName, serviceVersion);
        services.AddFireflyCore(config, serviceName, serviceVersion, cqrsAssemblies);
        services.TryAddSingleton<IExtensionRegistry, DefaultExtensionRegistry>();
        services.TryAddSingleton<IPluginManager, DefaultPluginManager>();

        services.AddFireflyResilience(config);
        services.AddFireflySecurity(config);
        services.AddFireflyActuator(config);
        services.AddFireflyScheduling();
        services.AddFireflySession(config);
        services.AddFireflyI18n(config);
        services.AddFireflyAop();
        services.AddFireflyWebSockets();

        return services;
    }
}
