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
using FireflyFramework.Validators.Internal;

namespace FireflyFramework.Validators.Attributes;

/// <summary>Credit card number validation via the Luhn algorithm. Mirrors <c>@ValidCreditCard</c>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ValidCreditCardAttribute : ValidationAttribute
{
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

        var digits = new string(s.Where(char.IsDigit).ToArray());
        return digits.Length is >= 13 and <= 19 && CheckDigit.Luhn(digits);
    }

    public override string FormatErrorMessage(string name) =>
        ErrorMessage ?? $"'{name}' is not a valid credit card number";
}
