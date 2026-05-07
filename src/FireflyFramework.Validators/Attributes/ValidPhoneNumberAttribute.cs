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

/// <summary>E.164 international phone number. Mirrors <c>@ValidPhoneNumber</c>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed partial class ValidPhoneNumberAttribute : ValidationAttribute
{
    [GeneratedRegex(@"^\+?[1-9]\d{6,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneRegex();

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not string s)
        {
            return false;
        }

        var stripped = new string(s.Where(c => c == '+' || char.IsDigit(c)).ToArray());
        return PhoneRegex().IsMatch(stripped);
    }

    public override string FormatErrorMessage(string name) =>
        ErrorMessage ?? $"'{name}' is not a valid international phone number";
}
