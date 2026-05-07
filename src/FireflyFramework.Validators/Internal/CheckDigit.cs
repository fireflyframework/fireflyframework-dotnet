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
