using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace FireflyFramework.Validators.Attributes;

/// <summary>Numeric account number, configurable length. Mirrors <c>@ValidAccountNumber</c>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed partial class ValidAccountNumberAttribute : ValidationAttribute
{
    public int MinLength { get; set; } = 6;
    public int MaxLength { get; set; } = 20;

    [GeneratedRegex(@"^\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex DigitRegex();

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is string s && s.Length >= MinLength && s.Length <= MaxLength && DigitRegex().IsMatch(s);
    }

    public override string FormatErrorMessage(string name) =>
        ErrorMessage ?? $"'{name}' must be a numeric account number of length {MinLength}–{MaxLength}";
}
