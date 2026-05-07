// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Globalization;

namespace FireflyFramework.I18n.Core;

public sealed class FixedLocaleResolver : ILocaleResolver
{
    private readonly CultureInfo _culture;
    public FixedLocaleResolver(CultureInfo culture) { _culture = culture; }
    public CultureInfo Resolve(IDictionary<string, string?> hints) => _culture;
}

public sealed class AcceptHeaderLocaleResolver : ILocaleResolver
{
    private readonly CultureInfo _fallback;
    public AcceptHeaderLocaleResolver(CultureInfo? fallback = null) { _fallback = fallback ?? CultureInfo.InvariantCulture; }

    public CultureInfo Resolve(IDictionary<string, string?> hints)
    {
        if (!hints.TryGetValue("Accept-Language", out var header) || string.IsNullOrWhiteSpace(header))
            return _fallback;
        var first = header!.Split(',').Select(s => s.Trim()).FirstOrDefault();
        if (string.IsNullOrEmpty(first)) return _fallback;
        var bare = first.Split(';')[0].Trim();
        try { return CultureInfo.GetCultureInfo(bare); }
        catch (CultureNotFoundException) { return _fallback; }
    }
}

public sealed class CookieLocaleResolver : ILocaleResolver
{
    public string CookieName { get; init; } = "lang";
    private readonly CultureInfo _fallback;
    public CookieLocaleResolver(CultureInfo? fallback = null) { _fallback = fallback ?? CultureInfo.InvariantCulture; }

    public CultureInfo Resolve(IDictionary<string, string?> hints)
    {
        if (!hints.TryGetValue($"Cookie:{CookieName}", out var v) || string.IsNullOrWhiteSpace(v)) return _fallback;
        try { return CultureInfo.GetCultureInfo(v!); }
        catch (CultureNotFoundException) { return _fallback; }
    }
}
