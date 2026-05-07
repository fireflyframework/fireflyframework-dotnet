using System.ComponentModel.DataAnnotations;
using FireflyFramework.Validators.Utilities;

namespace FireflyFramework.Validators.Attributes;

/// <summary>Password strength validation (length, classes, blacklist). Mirrors <c>@ValidPasswordStrength</c>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ValidPasswordStrengthAttribute : ValidationAttribute
{
    public int MinLength { get; set; } = 8;
    public int MaxLength { get; set; } = 128;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireSymbol { get; set; } = true;

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

        var policy = new PasswordPolicy
        {
            MinLength = MinLength,
            MaxLength = MaxLength,
            RequireUppercase = RequireUppercase,
            RequireLowercase = RequireLowercase,
            RequireDigit = RequireDigit,
            RequireSymbol = RequireSymbol,
        };

        return PasswordStrengthUtils.Evaluate(s, policy).IsAcceptable;
    }

    public override string FormatErrorMessage(string name) =>
        ErrorMessage ?? $"'{name}' does not meet password strength requirements";
}
