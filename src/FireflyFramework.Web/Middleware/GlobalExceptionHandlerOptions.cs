namespace FireflyFramework.Web.Middleware;

/// <summary>Configuration for <see cref="GlobalExceptionHandlerMiddleware"/>. Mirrors Java <c>ErrorHandlingProperties</c>.</summary>
public sealed class GlobalExceptionHandlerOptions
{
    public const string SectionName = "Firefly:Web:ErrorHandling";

    /// <summary>Include exception stack traces in the response body. Should be off in production.</summary>
    public bool IncludeStackTrace { get; set; }

    /// <summary>Include the inner-exception chain in the <c>debugInfo</c> map. Off by default.</summary>
    public bool IncludeDebugInfo { get; set; }

    /// <summary>RFC 7807 type-URI prefix used when generating <c>type</c> URIs.</summary>
    public string ProblemTypeBaseUri { get; set; } = "https://errors.fireflyframework.org/";

    /// <summary>Apply PII masking to the error <c>message</c> and <c>details</c> fields.</summary>
    public bool MaskPii { get; set; } = true;
}
