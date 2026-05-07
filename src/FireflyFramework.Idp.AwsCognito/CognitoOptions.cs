namespace FireflyFramework.Idp.AwsCognito;

public sealed class CognitoOptions
{
    public const string SectionName = "Firefly:Idp:Cognito";
    public string Region { get; set; } = "us-east-1";
    public string UserPoolId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
}
