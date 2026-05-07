// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Globalization;
using System.Text.Json;
using FireflyFramework.I18n.Configuration;
using FireflyFramework.I18n.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FireflyFramework.I18n.DependencyInjection;

public static class FireflyI18nExtensions
{
    public static IServiceCollection AddFireflyI18n(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<FireflyI18nOptions>().Bind(config.GetSection(FireflyI18nOptions.SectionName));

        services.TryAddSingleton<IMessageSource>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<FireflyI18nOptions>>().Value;
            return new ResourceBundleMessageSource(locale =>
            {
                var fileName = string.IsNullOrEmpty(locale) ? $"{opt.BaseName}.json" : $"{opt.BaseName}_{locale}.json";
                var path = Path.Combine(opt.ResourceDirectory, fileName);
                if (!File.Exists(path)) return null;
                using var stream = File.OpenRead(path);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            }, CultureInfo.GetCultureInfo(opt.DefaultLocale));
        });

        services.TryAddSingleton<ILocaleResolver>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<FireflyI18nOptions>>().Value;
            var fallback = CultureInfo.GetCultureInfo(opt.DefaultLocale);
            return opt.Resolver switch
            {
                "Fixed" => new FixedLocaleResolver(fallback),
                "Cookie" => new CookieLocaleResolver(fallback) { CookieName = opt.CookieName },
                _ => new AcceptHeaderLocaleResolver(fallback),
            };
        });

        return services;
    }
}
