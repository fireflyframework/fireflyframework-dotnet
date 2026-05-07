// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Actuator.Core;
using Microsoft.Extensions.Configuration;

namespace FireflyFramework.Actuator.Endpoints;

public sealed class EnvEndpoint : IActuatorEndpoint
{
    private static readonly string[] _maskedKeys = { "password", "secret", "token", "key", "connectionstring" };
    private readonly IConfigurationRoot _config;

    public EnvEndpoint(IConfiguration config) { _config = (IConfigurationRoot)config; }

    public string Id => "env";

    public Task<object?> InvokeAsync(IDictionary<string, string?> parameters, CancellationToken ct)
    {
        var sources = _config.Providers.Select(p => new
        {
            name = p.GetType().Name,
            properties = Flatten(p),
        });
        return Task.FromResult<object?>(new { propertySources = sources });
    }

    private static IReadOnlyDictionary<string, object?> Flatten(IConfigurationProvider provider)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in EnumerateKeys(provider, null))
        {
            if (provider.TryGet(key, out var value))
                result[key] = ShouldMask(key) ? "***" : value;
        }
        return result;
    }

    private static IEnumerable<string> EnumerateKeys(IConfigurationProvider provider, string? parent)
    {
        var children = provider.GetChildKeys(Array.Empty<string>(), parent)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var child in children)
        {
            var path = parent is null ? child : $"{parent}:{child}";
            yield return path;
            foreach (var c in EnumerateKeys(provider, path)) yield return c;
        }
    }

    private static bool ShouldMask(string key) =>
        _maskedKeys.Any(m => key.Contains(m, StringComparison.OrdinalIgnoreCase));
}
