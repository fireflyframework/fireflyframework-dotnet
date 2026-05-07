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
using System.Text.RegularExpressions;

namespace FireflyFramework.Validators.Attributes;

/// <summary>3 or 4 digit CVV. Mirrors <c>@ValidCVV</c>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed partial class ValidCvvAttribute : ValidationAttribute
{
    [GeneratedRegex(@"^\d{3,4}$", RegexOptions.CultureInvariant)]
    private static partial Regex CvvRegex();

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is string s && CvvRegex().IsMatch(s);
    }

    public override string FormatErrorMessage(string name) =>
        ErrorMessage ?? $"'{name}' is not a valid CVV";
}
