using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace FireflyFramework.Web.Logging;

/// <summary>
/// Prints the Firefly banner to <see cref="Console.Out"/> at application startup.
/// Mirrors the Spring Boot <c>banner.txt</c> mechanism used by the Java starters
/// (<c>fireflyframework-starter-{core,application,domain,data}</c>) — each .NET
/// starter ships an embedded <c>Resources/banner.txt</c> that this printer
/// resolves, applies ANSI substitutions to, and writes once before logging starts.
/// </summary>
public static class FireflyBanner
{
    private static int _printed;

    private static readonly Regex PlaceholderPattern = new(
        @"\$\{(?<key>[A-Za-z][A-Za-z0-9._-]*)(?::(?<default>[^}]*))?\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Prints the banner the first time it is called in this process; subsequent
    /// calls are no-ops. Safe to invoke from every starter — only the outermost
    /// starter on the call chain produces output.
    /// </summary>
    public static void Print(Assembly starterAssembly, string serviceName, string serviceVersion, TextWriter? output = null)
    {
        ArgumentNullException.ThrowIfNull(starterAssembly);
        ArgumentNullException.ThrowIfNull(serviceName);
        ArgumentNullException.ThrowIfNull(serviceVersion);

        if (Interlocked.Exchange(ref _printed, 1) != 0) return;

        var template = LoadTemplate(starterAssembly);
        if (template is null) return;

        var rendered = Render(template, serviceName, serviceVersion, AnsiSupported(output));
        (output ?? Console.Out).Write(rendered);
    }

    /// <summary>
    /// Test hook: resets the print latch so a second call to <see cref="Print"/>
    /// will produce output again. Not part of the supported API.
    /// </summary>
    internal static void ResetForTests() => Interlocked.Exchange(ref _printed, 0);

    /// <summary>
    /// Test hook: render the banner without writing it anywhere.
    /// </summary>
    internal static string? RenderForTests(Assembly starterAssembly, string serviceName, string serviceVersion, bool ansi)
    {
        var template = LoadTemplate(starterAssembly);
        return template is null ? null : Render(template, serviceName, serviceVersion, ansi);
    }

    private static string? LoadTemplate(Assembly assembly)
    {
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(".banner.txt", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null) return null;

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return null;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Render(string template, string serviceName, string serviceVersion, bool ansi)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["application.name"] = serviceName,
            ["application.version"] = serviceVersion,
            ["dotnet.version"] = RuntimeInformation.FrameworkDescription,
        };

        return PlaceholderPattern.Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            var fallback = match.Groups["default"].Success ? match.Groups["default"].Value : string.Empty;

            if (key.StartsWith("AnsiColor.", StringComparison.OrdinalIgnoreCase))
            {
                return ansi ? AnsiColor(key.AsSpan("AnsiColor.".Length)) : string.Empty;
            }

            if (key.StartsWith("AnsiStyle.", StringComparison.OrdinalIgnoreCase))
            {
                return ansi ? AnsiStyle(key.AsSpan("AnsiStyle.".Length)) : string.Empty;
            }

            return values.TryGetValue(key, out var value) ? value : fallback;
        });
    }

    private static string AnsiColor(ReadOnlySpan<char> name) => name switch
    {
        var s when s.Equals("RED", StringComparison.OrdinalIgnoreCase) => "[31m",
        var s when s.Equals("GREEN", StringComparison.OrdinalIgnoreCase) => "[32m",
        var s when s.Equals("YELLOW", StringComparison.OrdinalIgnoreCase) => "[33m",
        var s when s.Equals("BLUE", StringComparison.OrdinalIgnoreCase) => "[34m",
        var s when s.Equals("MAGENTA", StringComparison.OrdinalIgnoreCase) => "[35m",
        var s when s.Equals("CYAN", StringComparison.OrdinalIgnoreCase) => "[36m",
        var s when s.Equals("WHITE", StringComparison.OrdinalIgnoreCase) => "[37m",
        var s when s.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase) => "[39m",
        _ => string.Empty,
    };

    private static string AnsiStyle(ReadOnlySpan<char> name) => name switch
    {
        var s when s.Equals("BOLD", StringComparison.OrdinalIgnoreCase) => "[1m",
        var s when s.Equals("NORMAL", StringComparison.OrdinalIgnoreCase) => "[22m",
        var s when s.Equals("FAINT", StringComparison.OrdinalIgnoreCase) => "[2m",
        var s when s.Equals("ITALIC", StringComparison.OrdinalIgnoreCase) => "[3m",
        var s when s.Equals("UNDERLINE", StringComparison.OrdinalIgnoreCase) => "[4m",
        _ => string.Empty,
    };

    private static bool AnsiSupported(TextWriter? output)
    {
        // Honour the cross-platform NO_COLOR convention (https://no-color.org).
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"))) return false;

        // If the caller supplied their own writer (e.g. in a test), keep colors —
        // the writer can decide what to do with the escape codes.
        if (output is not null && output != Console.Out) return true;

        // Honour TERM=dumb on POSIX shells and absence of a TTY when running
        // under CI / piped output.
        var term = Environment.GetEnvironmentVariable("TERM");
        if (string.Equals(term, "dumb", StringComparison.OrdinalIgnoreCase)) return false;
        if (Console.IsOutputRedirected) return false;

        return true;
    }
}
