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
