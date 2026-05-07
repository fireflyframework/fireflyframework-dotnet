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

using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FireflyFramework.ConfigServer;

/// <summary>
/// Implements the Spring-Cloud-Config-Server "native" protocol: reads YAML / JSON files
/// from a configured directory and serves them at <c>/{application}/{profile}[/{label}]</c>.
/// Mirrors the Java <c>FireflyConfigServerApplication</c>.
/// </summary>
/// <remarks>
/// Files are looked up in the following precedence (first match wins):
/// <list type="number">
///   <item><c>{appName}-{profile}.{ext}</c></item>
///   <item><c>{appName}.{ext}</c></item>
///   <item><c>application-{profile}.{ext}</c></item>
///   <item><c>application.{ext}</c></item>
/// </list>
/// where <c>{ext}</c> ∈ {yml, yaml, json, properties}.
/// </remarks>
public static class ConfigServerEndpoints
{
    public sealed class Options
    {
        public string SearchDirectory { get; set; } = "./config";
    }

    public static IServiceCollection AddFireflyConfigServer(this IServiceCollection services, IConfiguration cfg)
    {
        services.Configure<Options>(o =>
        {
            o.SearchDirectory = cfg.GetValue<string>("Firefly:ConfigServer:SearchDirectory") ?? "./config";
        });
        return services;
    }

    public static IEndpointRouteBuilder MapFireflyConfigServer(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("/{application}/{profile}", (HttpContext http, string application, string profile) =>
            ServeConfig(http, application, profile, label: null));

        builder.MapGet("/{application}/{profile}/{label}", (HttpContext http, string application, string profile, string label) =>
            ServeConfig(http, application, profile, label));

        builder.MapGet("/health", () => Results.Ok(new { status = "UP" }));

        return builder;
    }

    private static IResult ServeConfig(HttpContext http, string application, string profile, string? label)
    {
        var opts = http.RequestServices.GetService<Microsoft.Extensions.Options.IOptions<Options>>()?.Value
            ?? new Options();
        var dir = opts.SearchDirectory;
        if (!Directory.Exists(dir))
        {
            return Results.NotFound(new { error = "search directory not found", dir });
        }

        var candidates = new[]
        {
            $"{application}-{profile}", $"{application}", $"application-{profile}", "application",
        };
        var extensions = new[] { "yml", "yaml", "json", "properties" };

        var sources = new List<object>();
        foreach (var c in candidates)
        {
            foreach (var e in extensions)
            {
                var path = Path.Combine(dir, $"{c}.{e}");
                if (!File.Exists(path)) continue;
                sources.Add(new
                {
                    name = $"file:{path}",
                    source = ReadAsDictionary(path),
                });
                break;
            }
        }

        // Spring Cloud Config Server response envelope
        return Results.Json(new
        {
            name = application,
            profiles = profile.Split(','),
            label,
            version = (string?)null,
            state = (string?)null,
            propertySources = sources,
        });
    }

    private static IDictionary<string, object?> ReadAsDictionary(string path)
    {
        var text = File.ReadAllText(path);
        return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? JsonSerializer.Deserialize<Dictionary<string, object?>>(text) ?? new Dictionary<string, object?>()
            : ReadFlat(text);

        static Dictionary<string, object?> ReadFlat(string text) =>
            text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l is { Length: > 0 } && !l.StartsWith("#") && l.Contains(':'))
                .Select(l => l.Split(':', 2))
                .ToDictionary(kv => kv[0].Trim(), kv => (object?)kv[1].Trim());
    }
}
