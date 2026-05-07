using FireflyFramework.Kernel.Exceptions;

namespace FireflyFramework.Cqrs.Authorization;

/// <summary>Thrown by buses when a command/query fails authorization.</summary>
public sealed class CqrsAuthorizationException : FireflySecurityException
{
    public IReadOnlyList<AuthorizationError> Errors { get; }

    public CqrsAuthorizationException(string message, IEnumerable<AuthorizationError> errors)
        : base(message, "CQRS_AUTHORIZATION_DENIED")
    {
        Errors = errors.ToList();
    }
}
