namespace FireflyFramework.Idp.Keycloak;

public sealed class KeycloakOptions
{
    public const string SectionName = "Firefly:Idp:Keycloak";

    public string ServerUrl { get; set; } = "http://localhost:8080";
    public string Realm { get; set; } = "master";
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
    public string? AdminUsername { get; set; }
    public string? AdminPassword { get; set; }
    public bool VerifyTokenSignature { get; set; } = true;
}
