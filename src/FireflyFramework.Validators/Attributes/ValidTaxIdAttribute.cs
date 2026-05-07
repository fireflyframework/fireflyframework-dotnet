using System.ComponentModel.DataAnnotations;

namespace FireflyFramework.Validators.Attributes;

/// <summary>Country-specific tax id; defaults to length-only check. Mirrors <c>@ValidTaxId</c>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ValidTaxIdAttribute : ValidationAttribute
{
    public int MinLength { get; set; } = 8;
    public int MaxLength { get; set; } = 20;

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is string s && s.Length >= MinLength && s.Length <= MaxLength;
    }

    public override string FormatErrorMessage(string name) =>
        ErrorMessage ?? $"'{name}' is not a valid tax id";
}
