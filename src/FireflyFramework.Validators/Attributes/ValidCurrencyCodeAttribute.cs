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

using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace FireflyFramework.Validators.Attributes;

/// <summary>ISO 4217 currency code (3 uppercase letters). Mirrors <c>@ValidCurrencyCode</c>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ValidCurrencyCodeAttribute : ValidationAttribute
{
    private static readonly HashSet<string> KnownCodes = BuildKnown();

    private static HashSet<string> BuildKnown()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                set.Add(new RegionInfo(c.Name).ISOCurrencySymbol);
            }
            catch (ArgumentException) { /* non-region cultures */ }
        }

        return set;
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is string s && s.Length == 3 && KnownCodes.Contains(s.ToUpperInvariant());
    }

    public override string FormatErrorMessage(string name) =>
        ErrorMessage ?? $"'{name}' is not a valid ISO 4217 currency code";
}
