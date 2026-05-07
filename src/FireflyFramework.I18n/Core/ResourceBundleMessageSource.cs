// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Concurrent;
using System.Globalization;

namespace FireflyFramework.I18n.Core;

/// <summary>
/// Loads message bundles from JSON files following the pattern
/// <c>{baseName}_{locale}.json</c> and falls back through parents
/// (e.g. <c>en-US</c> → <c>en</c> → invariant). Mirrors Spring
/// <c>ResourceBundleMessageSource</c>.
/// </summary>
public sealed class ResourceBundleMessageSource : IMessageSource
{
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _bundles = new();
    private readonly Func<string, IReadOnlyDictionary<string, string>?> _loader;
    private readonly CultureInfo _defaultCulture;

    public ResourceBundleMessageSource(Func<string, IReadOnlyDictionary<string, string>?> loader, CultureInfo? defaultCulture = null)
    {
        _loader = loader;
        _defaultCulture = defaultCulture ?? CultureInfo.InvariantCulture;
    }

    public string GetMessage(string code, CultureInfo? culture = null, params object?[] args) =>
        GetMessageOrNull(code, culture, args) ?? code;

    public string? GetMessageOrNull(string code, CultureInfo? culture = null, params object?[] args)
    {
        var c = culture ?? _defaultCulture;
        foreach (var key in CulturesToTry(c))
        {
            var bundle = _bundles.GetOrAdd(key, k => _loader(k) ?? new Dictionary<string, string>());
            if (bundle.TryGetValue(code, out var template))
                return args is { Length: > 0 } ? string.Format(c, template, args) : template;
        }
        return null;
    }

    public bool HasMessage(string code, CultureInfo? culture = null) =>
        GetMessageOrNull(code, culture) is not null;

    private static IEnumerable<string> CulturesToTry(CultureInfo culture)
    {
        yield return culture.Name;
        if (!string.IsNullOrEmpty(culture.Parent.Name)) yield return culture.Parent.Name;
        yield return string.Empty; // invariant
    }
}
