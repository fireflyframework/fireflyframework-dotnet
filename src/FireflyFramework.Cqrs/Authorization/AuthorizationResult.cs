namespace FireflyFramework.Cqrs.Authorization;

public sealed class AuthorizationResult
{
    public bool IsAllowed { get; }
    public IReadOnlyList<AuthorizationError> Errors { get; }

    private AuthorizationResult(bool allowed, IReadOnlyList<AuthorizationError> errors)
    {
        IsAllowed = allowed;
        Errors = errors;
    }

    public static AuthorizationResult Allowed() => new(true, Array.Empty<AuthorizationError>());
    public static AuthorizationResult Denied(string code, string message) =>
        new(false, new[] { new AuthorizationError(code, message) });
}

public sealed record AuthorizationError(string Code, string Message);
