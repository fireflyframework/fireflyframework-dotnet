using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace FireflyFramework.Validators.Attributes;

/// <summary>Date format validation, pattern-based. Mirrors <c>@ValidDate</c>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ValidDateAttribute : ValidationAttribute
{
    public string Pattern { get; set; } = "yyyy-MM-dd";

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is DateTime or DateOnly or DateTimeOffset)
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
        ErrorMessage ?? $"'{name}' must match date pattern '{Pattern}'";
}
