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
