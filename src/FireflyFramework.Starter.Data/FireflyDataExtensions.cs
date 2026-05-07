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

using FireflyFramework.Starter.Core;
using FireflyFramework.Web.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FireflyFramework.Starter.Data;

/// <summary>
/// Data-tier starter: includes everything in the core starter. The application is
/// expected to call <c>services.AddDbContext&lt;TDb&gt;(...)</c> with its own
/// <see cref="Microsoft.EntityFrameworkCore.DbContext"/>; the Firefly filtering
/// utilities (<c>FireflyFramework.Data.Filters</c>) and pagination types are made
/// available transitively.
/// </summary>
public static class FireflyDataExtensions
{
    public static IServiceCollection AddFireflyData(
        this IServiceCollection services,
        IConfiguration config,
        string serviceName,
        string serviceVersion = "1.0.0",
        params System.Reflection.Assembly[] cqrsAssemblies)
    {
        FireflyBanner.Print(typeof(FireflyDataExtensions).Assembly, serviceName, serviceVersion);
        return services.AddFireflyCore(config, serviceName, serviceVersion, cqrsAssemblies);
    }
}
