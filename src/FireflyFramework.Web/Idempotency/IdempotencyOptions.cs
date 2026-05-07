namespace FireflyFramework.Web.Idempotency;

/// <summary>Configuration for <see cref="IdempotencyMiddleware"/>. Mirrors Java <c>IdempotencyProperties</c>.</summary>
public sealed class IdempotencyOptions
{
    public const string SectionName = "Firefly:Web:Idempotency";

    public bool Enabled { get; set; } = true;

    public string HeaderName { get; set; } = "X-Idempotency-Key";

    public TimeSpan Ttl { get; set; } = TimeSpan.FromHours(24);

    public int MaxKeyLength { get; set; } = 256;

    public HashSet<string> Methods { get; set; } = new(StringComparer.OrdinalIgnoreCase) { "POST", "PATCH", "PUT", "DELETE" };
}
