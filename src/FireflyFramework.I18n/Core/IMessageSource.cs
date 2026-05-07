// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Globalization;

namespace FireflyFramework.I18n.Core;

/// <summary>Spring <c>MessageSource</c> port.</summary>
public interface IMessageSource
{
    string GetMessage(string code, CultureInfo? culture = null, params object?[] args);
    string? GetMessageOrNull(string code, CultureInfo? culture = null, params object?[] args);
    bool HasMessage(string code, CultureInfo? culture = null);
}

public interface ILocaleResolver
{
    CultureInfo Resolve(IDictionary<string, string?> hints);
}
