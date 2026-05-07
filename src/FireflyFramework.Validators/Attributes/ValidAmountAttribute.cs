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

/// <summary>Monetary amount validation: optional bounds + max scale. Mirrors <c>@ValidAmount</c>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ValidAmountAttribute : ValidationAttribute
{
    public double Min { get; set; } = double.MinValue;
    public double Max { get; set; } = double.MaxValue;
    public int MaxFractionDigits { get; set; } = 4;
    public bool AllowNegative { get; set; } = true;

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        decimal amount;
        switch (value)
        {
            case decimal d: amount = d; break;
            case double d:  amount = (decimal)d; break;
            case float f:   amount = (decimal)f; break;
            case long l:    amount = l; break;
            case int i:     amount = i; break;
            case string s when decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed):
                amount = parsed; break;
            default: return false;
        }

        if (!AllowNegative && amount < 0)
        {
            return false;
        }

        if ((double)amount < Min || (double)amount > Max)
        {
            return false;
        }

        var scale = (decimal.GetBits(amount)[3] >> 16) & 0xFF;
        return scale <= MaxFractionDigits;
    }

    public override string FormatErrorMessage(string name) =>
        ErrorMessage ?? $"'{name}' is not a valid monetary amount";
}
