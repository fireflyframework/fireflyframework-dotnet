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

using System;
using System.Globalization;
using System.Numerics;

namespace FireflyFramework.Validators.Internal;

/// <summary>
/// Reusable check-digit / mod-N validation primitives shared by IBAN, credit card
/// and similar validators. Mirrors the helpers in <c>commons-validator</c>.
/// </summary>
internal static class CheckDigit
{
    public static bool Luhn(string digits)
    {
        if (string.IsNullOrEmpty(digits))
        {
            return false;
        }

        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(digits[i]))
            {
                return false;
            }

            var n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9)
                {
                    n -= 9;
                }
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    /// <summary>ISO 7064 mod-97 used by IBAN.</summary>
    public static bool Iban(string iban)
    {
        if (string.IsNullOrWhiteSpace(iban) || iban.Length < 4)
        {
            return false;
        }

        var rearranged = iban[4..] + iban[..4];
        var numeric = string.Empty;
        foreach (var c in rearranged)
        {
            numeric += char.IsLetter(c)
                ? ((c - 'A' + 10).ToString(CultureInfo.InvariantCulture))
                : c.ToString(CultureInfo.InvariantCulture);
        }

        if (!BigInteger.TryParse(numeric, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        {
            return false;
        }

        return n % 97 == 1;
    }
}
