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

using FireflyFramework.Starter.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FireflyFramework.BackOffice;

public static class FireflyBackOfficeExtensions
{
    /// <summary>
    /// Registers everything from <see cref="FireflyApplicationExtensions.AddFireflyApplication"/>
    /// plus the back-office context resolver. Replace
    /// <see cref="HeaderBackofficeContextResolver"/> with a service-specific subclass to plug
    /// in your security center.
    /// </summary>
    public static IServiceCollection AddFireflyBackOffice(
        this IServiceCollection services,
        IConfiguration config,
        string serviceName,
        string serviceVersion = "1.0.0",
        params System.Reflection.Assembly[] cqrsAssemblies)
    {
        services.AddFireflyApplication(config, serviceName, serviceVersion, cqrsAssemblies);
        services.TryAddScoped<IBackofficeContextResolver, HeaderBackofficeContextResolver>();
        return services;
    }
}
