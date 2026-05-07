using System.Text.RegularExpressions;

namespace FireflyFramework.Validators.Utilities;

/// <summary>
/// Password strength helpers. Mirrors <c>org.fireflyframework.validators.PasswordStrengthUtils</c>:
/// reusable from validation attributes, FluentValidation rules and ad-hoc service code.
/// </summary>
public static partial class PasswordStrengthUtils
{
    [GeneratedRegex(@"[A-Z]", RegexOptions.CultureInvariant)] private static partial Regex Upper();
    [GeneratedRegex(@"[a-z]", RegexOptions.CultureInvariant)] private static partial Regex Lower();
    [GeneratedRegex(@"\d",   RegexOptions.CultureInvariant)] private static partial Regex Digit();
    [GeneratedRegex(@"[^A-Za-z0-9]", RegexOptions.CultureInvariant)] private static partial Regex Symbol();

    public static PasswordEvaluation Evaluate(string password, PasswordPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var violations = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            violations.Add("password.empty");
            return new PasswordEvaluation(false, 0, violations);
        }

        if (password.Length < policy.MinLength) violations.Add("password.tooShort");
        if (password.Length > policy.MaxLength) violations.Add("password.tooLong");
        if (policy.RequireUppercase && !Upper().IsMatch(password)) violations.Add("password.noUppercase");
        if (policy.RequireLowercase && !Lower().IsMatch(password)) violations.Add("password.noLowercase");
        if (policy.RequireDigit && !Digit().IsMatch(password)) violations.Add("password.noDigit");
        if (policy.RequireSymbol && !Symbol().IsMatch(password)) violations.Add("password.noSymbol");
        foreach (var banned in policy.Blacklist)
        {
            if (password.Contains(banned, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add($"password.blacklisted:{banned}");
            }
        }

        var score = 0;
        if (Upper().IsMatch(password)) score++;
        if (Lower().IsMatch(password)) score++;
        if (Digit().IsMatch(password)) score++;
        if (Symbol().IsMatch(password)) score++;
        if (password.Length >= 12) score++;
        if (password.Length >= 16) score++;

        return new PasswordEvaluation(violations.Count == 0, score, violations);
    }
}
