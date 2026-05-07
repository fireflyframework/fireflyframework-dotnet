using System.ComponentModel.DataAnnotations;

namespace FireflyFramework.Validators.Attributes;

/// <summary>National identity document; length-only check by default. Mirrors <c>@ValidNationalId</c>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ValidNationalIdAttribute : ValidationAttribute
{
    public int MinLength { get; set; } = 5;
    public int MaxLength { get; set; } = 30;

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is string s && s.Length >= MinLength && s.Length <= MaxLength;
    }

    public override string FormatErrorMessage(string name) =>
        ErrorMessage ?? $"'{name}' is not a valid national id";
}
