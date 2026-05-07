using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace FireflyFramework.Validators.Attributes;

/// <summary>UK/Ireland sort code (NN-NN-NN). Mirrors <c>@ValidSortCode</c>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed partial class ValidSortCodeAttribute : ValidationAttribute
{
    [GeneratedRegex(@"^\d{2}-?\d{2}-?\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex SortCodeRegex();

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is string s && SortCodeRegex().IsMatch(s);
    }

    public override string FormatErrorMessage(string name) =>
        ErrorMessage ?? $"'{name}' is not a valid sort code";
}
