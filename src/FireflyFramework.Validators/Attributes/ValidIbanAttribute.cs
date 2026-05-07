using System.ComponentModel.DataAnnotations;
using FireflyFramework.Validators.Internal;

namespace FireflyFramework.Validators.Attributes;

/// <summary>
/// IBAN validation. Mirrors <c>@ValidIban</c>: country prefix + length + ISO 7064 mod-97.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ValidIbanAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not string raw)
        {
            return false;
        }

        var normalised = raw.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (normalised.Length is < 15 or > 34)
        {
            return false;
        }

        if (!char.IsLetter(normalised[0]) || !char.IsLetter(normalised[1]))
        {
            return false;
        }

        return CheckDigit.Iban(normalised);
    }

    public override string FormatErrorMessage(string name) =>
        ErrorMessage ?? $"'{name}' is not a valid IBAN";
}
