using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace FireflyFramework.Validators.Attributes;

/// <summary>ISO 4217 currency code (3 uppercase letters). Mirrors <c>@ValidCurrencyCode</c>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ValidCurrencyCodeAttribute : ValidationAttribute
{
    private static readonly HashSet<string> KnownCodes = BuildKnown();

    private static HashSet<string> BuildKnown()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                set.Add(new RegionInfo(c.Name).ISOCurrencySymbol);
            }
            catch (ArgumentException) { /* non-region cultures */ }
        }

        return set;
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is string s && s.Length == 3 && KnownCodes.Contains(s.ToUpperInvariant());
    }

    public override string FormatErrorMessage(string name) =>
        ErrorMessage ?? $"'{name}' is not a valid ISO 4217 currency code";
}
