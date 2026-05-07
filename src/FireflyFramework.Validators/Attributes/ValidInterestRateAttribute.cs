using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace FireflyFramework.Validators.Attributes;

/// <summary>Interest rate range validation (default 0–100%). Mirrors <c>@ValidInterestRate</c>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ValidInterestRateAttribute : ValidationAttribute
{
    public double Min { get; set; }
    public double Max { get; set; } = 100.0;

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        var rate = value switch
        {
            decimal d => (double)d,
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            string s when double.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) => v,
            _ => double.NaN
        };

        return !double.IsNaN(rate) && rate >= Min && rate <= Max;
    }

    public override string FormatErrorMessage(string name) =>
        ErrorMessage ?? $"'{name}' must be between {Min} and {Max}";
}
