using Microsoft.AspNetCore.Http;

namespace FireflyFramework.BackOffice;

/// <summary>
/// Resolves a <see cref="BackofficeContext"/> from the inbound HTTP request. Mirrors Java
/// <c>BackofficeContextResolver</c>. Default expectations:
/// <list type="bullet">
///   <item><c>X-User-Id</c> — back-office user UUID (required)</item>
///   <item><c>X-Impersonate-Party-Id</c> — impersonated customer UUID (required)</item>
///   <item><c>X-Tenant-Id</c> — optional tenant UUID</item>
///   <item><c>X-Impersonation-Reason</c> — optional reason string</item>
/// </list>
/// </summary>
public interface IBackofficeContextResolver
{
    Task<BackofficeContext> ResolveAsync(HttpContext httpContext, CancellationToken ct = default);

    Task<BackofficeContext> ResolveAsync(
        HttpContext httpContext,
        Guid? contractId,
        Guid? productId,
        CancellationToken ct = default);

    Task<bool> ValidateImpersonationAsync(
        Guid backofficeUserId,
        Guid impersonatedPartyId,
        HttpContext httpContext,
        CancellationToken ct = default);

    int Priority => 0;
    bool Supports(HttpContext httpContext) => true;
}
