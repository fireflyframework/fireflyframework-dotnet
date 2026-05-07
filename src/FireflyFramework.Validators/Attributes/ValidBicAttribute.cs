using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace FireflyFramework.Validators.Attributes;

/// <summary>
/// BIC/SWIFT code validation (8 or 11 chars). Mirrors <c>@ValidBic</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed partial class ValidBicAttribute : ValidationAttribute
{
    [GeneratedRegex(@"^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$", RegexOptions.CultureInvariant)]
    private static partial Regex BicRegex();

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is string s && BicRegex().IsMatch(s.Trim().ToUpperInvariant());
    }

    public override string FormatErrorMessage(string name) =>
        ErrorMessage ?? $"'{name}' is not a valid BIC/SWIFT code";
}
