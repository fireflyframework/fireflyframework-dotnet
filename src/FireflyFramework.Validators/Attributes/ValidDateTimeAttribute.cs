using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace FireflyFramework.Validators.Attributes;

/// <summary>DateTime format validation, pattern-based. Mirrors <c>@ValidDateTime</c>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ValidDateTimeAttribute : ValidationAttribute
{
    public string Pattern { get; set; } = "yyyy-MM-ddTHH:mm:ss";

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is DateTime or DateTimeOffset)
        {
            return true;
        }

        if (value is string s)
        {
            return DateTime.TryParseExact(s, Pattern, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out _);
        }

        return false;
    }

    public override string FormatErrorMessage(string name) =>
        ErrorMessage ?? $"'{name}' must match datetime pattern '{Pattern}'";
}
