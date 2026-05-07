// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Globalization;
using FireflyFramework.I18n.Core;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class I18nTests
{
    [Fact]
    public void ResourceBundleMessageSource_resolves_specific_then_parent_culture()
    {
        var bundles = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            [""] = new Dictionary<string, string> { ["welcome"] = "hi" },
            ["en"] = new Dictionary<string, string> { ["welcome"] = "Hello" },
            ["es"] = new Dictionary<string, string> { ["welcome"] = "Hola {0}" },
            ["es-AR"] = new Dictionary<string, string> { ["welcome"] = "Che, {0}!" },
        };

        var source = new ResourceBundleMessageSource(loc => bundles.TryGetValue(loc, out var b) ? b : null);

        source.GetMessage("welcome", CultureInfo.GetCultureInfo("es-AR"), "Ana").Should().Be("Che, Ana!");
        source.GetMessage("welcome", CultureInfo.GetCultureInfo("es-MX"), "Ana").Should().Be("Hola Ana");
        source.GetMessage("welcome", CultureInfo.GetCultureInfo("en-US")).Should().Be("Hello");
        source.GetMessage("missing").Should().Be("missing");
    }

    [Fact]
    public void AcceptHeaderLocaleResolver_picks_first_quality_value()
    {
        var resolver = new AcceptHeaderLocaleResolver(fallback: CultureInfo.InvariantCulture);
        var hints = new Dictionary<string, string?> { ["Accept-Language"] = "fr-CA, en;q=0.8, *;q=0.5" };
        resolver.Resolve(hints).Name.Should().Be("fr-CA");
    }

    [Fact]
    public void FixedLocaleResolver_always_returns_configured_culture()
    {
        var resolver = new FixedLocaleResolver(CultureInfo.GetCultureInfo("ja-JP"));
        resolver.Resolve(new Dictionary<string, string?>()).Name.Should().Be("ja-JP");
    }
}
