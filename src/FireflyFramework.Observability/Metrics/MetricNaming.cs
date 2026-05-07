using System.Text.RegularExpressions;

namespace FireflyFramework.Observability.Metrics;

/// <summary>Utility for naming metrics consistently. Mirrors Java <c>MetricNaming</c>.</summary>
public static partial class MetricNaming
{
    [GeneratedRegex(@"^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ModuleRegex();

    public static string Prefix(string module)
    {
        if (string.IsNullOrWhiteSpace(module) || !ModuleRegex().IsMatch(module))
        {
            throw new ArgumentException("Module names must be lowercase, alphanumeric and underscore-only.", nameof(module));
        }

        return $"firefly.{module}";
    }

    public static string Name(string prefix, string metric) => $"{prefix}.{metric}";
}
