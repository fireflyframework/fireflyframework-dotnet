using System.ComponentModel.DataAnnotations;

namespace FireflyFramework.Validators.Attributes;

/// <summary>PIN validation: configurable length, all digits. Mirrors <c>@ValidPIN</c>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ValidPinAttribute : ValidationAttribute
{
    public int MinLength { get; set; } = 4;
    public int MaxLength { get; set; } = 6;

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

        return s.Length >= MinLength && s.Length <= MaxLength && s.All(char.IsDigit);
    }

    public override string FormatErrorMessage(string name) =>
        ErrorMessage ?? $"'{name}' must be a numeric PIN of length {MinLength}–{MaxLength}";
}
