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

using FireflyFramework.Cache.DependencyInjection;
using FireflyFramework.Cqrs.DependencyInjection;
using FireflyFramework.Eda.DependencyInjection;
using FireflyFramework.Observability.DependencyInjection;
using FireflyFramework.Web.DependencyInjection;
using FireflyFramework.Web.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FireflyFramework.Starter.Core;

/// <summary>
/// One-call registration of the Firefly infrastructure tier — equivalent to importing
/// <c>fireflyframework-starter-core</c> on the Java side.
/// </summary>
public static class FireflyCoreExtensions
{
    public static IServiceCollection AddFireflyCore(
        this IServiceCollection services,
        IConfiguration config,
        string serviceName,
        string serviceVersion = "1.0.0",
        params System.Reflection.Assembly[] cqrsAssemblies)
    {
        FireflyBanner.Print(typeof(FireflyCoreExtensions).Assembly, serviceName, serviceVersion);
        services.AddFireflyWeb(config);
        services.AddFireflyObservability(config, serviceName, serviceVersion);
        services.AddFireflyCache(config);
        services.AddFireflyEda(config);
        services.AddFireflyCqrs(cqrsAssemblies);
        return services;
    }
}
