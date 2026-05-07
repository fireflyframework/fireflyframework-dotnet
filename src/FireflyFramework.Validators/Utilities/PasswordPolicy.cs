namespace FireflyFramework.Validators.Utilities;

public sealed record PasswordPolicy
{
    public int MinLength { get; init; } = 8;
    public int MaxLength { get; init; } = 128;
    public bool RequireUppercase { get; init; } = true;
    public bool RequireLowercase { get; init; } = true;
    public bool RequireDigit { get; init; } = true;
    public bool RequireSymbol { get; init; } = true;
    public IReadOnlyCollection<string> Blacklist { get; init; } = Array.Empty<string>();
}

public sealed record PasswordEvaluation(bool IsAcceptable, int Score, IReadOnlyList<string> Violations);
