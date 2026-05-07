using FireflyFramework.Cqrs.Authorization;
using FireflyFramework.Cqrs.Context;
using FireflyFramework.Cqrs.Validation;

namespace FireflyFramework.Cqrs.Commands;

/// <summary>
/// Marker for write-side messages. Mirrors Java <c>Command&lt;R&gt;</c>: the type
/// argument <typeparamref name="TResult"/> defines the response shape.
/// </summary>
public interface ICommand<out TResult>
{
    Guid CommandId => Guid.NewGuid();
    DateTimeOffset Timestamp => DateTimeOffset.UtcNow;
    string? CorrelationId => null;
    string? InitiatedBy => null;
    IReadOnlyDictionary<string, object?> Metadata => EmptyMetadata;

    Task<ValidationResult> ValidateAsync(CancellationToken ct = default) => Task.FromResult(ValidationResult.Successful());
    Task<AuthorizationResult> AuthorizeAsync(ExecutionContext context, CancellationToken ct = default) => Task.FromResult(AuthorizationResult.Allowed());

    private static readonly IReadOnlyDictionary<string, object?> EmptyMetadata = new Dictionary<string, object?>();
}
