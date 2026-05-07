namespace FireflyFramework.Web.Logging;

/// <summary>Configuration for <see cref="PiiMaskingService"/>. Mirrors Java <c>PiiMaskingProperties</c>.</summary>
public sealed class PiiMaskingOptions
{
    public const string SectionName = "Firefly:Web:PiiMasking";

    public bool Enabled { get; set; } = true;

    public string MaskCharacter { get; set; } = "*";

    public int VisiblePrefix { get; set; } = 2;

    public int VisibleSuffix { get; set; } = 2;

    public List<string> SensitiveFields { get; set; } = new()
    {
        "password", "secret", "token", "apiKey", "authorization",
        "ssn", "creditCard", "cardNumber", "cvv", "iban", "pin",
    };

    public List<string> SensitivePatterns { get; set; } = new();
}
