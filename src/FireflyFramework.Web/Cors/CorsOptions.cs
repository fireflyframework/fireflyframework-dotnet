namespace FireflyFramework.Web.Cors;

/// <summary>Centralised CORS configuration. Mirrors Java <c>CorsProperties</c>.</summary>
public sealed class FireflyCorsOptions
{
    public const string SectionName = "Firefly:Web:Cors";

    public bool Enabled { get; set; } = true;
    public List<string> AllowedOrigins { get; set; } = new() { "*" };
    public List<string> AllowedMethods { get; set; } = new() { "GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS" };
    public List<string> AllowedHeaders { get; set; } = new() { "*" };
    public List<string> ExposedHeaders { get; set; } = new() { "X-Correlation-Id", "X-Request-Id", "X-Idempotency-Key" };
    public bool AllowCredentials { get; set; }
    public TimeSpan PreflightMaxAge { get; set; } = TimeSpan.FromMinutes(10);
}
