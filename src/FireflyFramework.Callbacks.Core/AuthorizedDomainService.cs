using System.Collections.Concurrent;
using FireflyFramework.Callbacks.Interfaces;

namespace FireflyFramework.Callbacks.Core;

/// <summary>
/// Validates that an outbound callback URL is allowed under the configured domain
/// allow-list. Mirrors Java <c>DomainAuthorizationService</c>.
/// </summary>
public interface IDomainAuthorizationService
{
    Task<bool> IsAuthorizedAsync(string url, CancellationToken ct = default);
    Task AuthorizeAsync(AuthorizedDomainDto domain, CancellationToken ct = default);
    Task RevokeAsync(string domain, CancellationToken ct = default);
    Task<IReadOnlyList<AuthorizedDomainDto>> ListAsync(CancellationToken ct = default);
}

public sealed class InMemoryDomainAuthorizationService : IDomainAuthorizationService
{
    private readonly ConcurrentDictionary<string, AuthorizedDomainDto> _domains = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> IsAuthorizedAsync(string url, CancellationToken ct = default)
    {
        if (_domains.IsEmpty) return Task.FromResult(true); // open by default until explicitly restricted
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return Task.FromResult(false);

        return Task.FromResult(_domains.Values.Any(d =>
            d.IsAuthorized && uri.Host.EndsWith(d.Domain, StringComparison.OrdinalIgnoreCase)));
    }

    public Task AuthorizeAsync(AuthorizedDomainDto domain, CancellationToken ct = default)
    {
        _domains[domain.Domain] = domain;
        return Task.CompletedTask;
    }

    public Task RevokeAsync(string domain, CancellationToken ct = default)
    {
        _domains.TryRemove(domain, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuthorizedDomainDto>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AuthorizedDomainDto>>(_domains.Values.ToList());
}
