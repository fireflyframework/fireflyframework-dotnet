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
