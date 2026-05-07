using System.Collections.Concurrent;
using FireflyFramework.Ecm.Domain;
using FireflyFramework.Ecm.Ports;

namespace FireflyFramework.Ecm.Adapters;

/// <summary>
/// In-memory document search backed by a shared <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// that tracks documents created via <see cref="IDocumentPort"/>. Mirrors Java
/// <c>LocalDocumentSearchAdapter</c>: useful for tests and single-node deployments where
/// no external search index is available.
/// </summary>
[EcmAdapter("local-search",
    Description = "In-memory document search adapter",
    Priority = 0,
    SupportedFeatures = AdapterFeature.Search | AdapterFeature.MetadataSearch)]
public sealed class LocalDocumentSearchAdapter : IDocumentSearchPort
{
    private readonly ConcurrentDictionary<Guid, Document> _documents;

    public LocalDocumentSearchAdapter() : this(new ConcurrentDictionary<Guid, Document>()) { }

    public LocalDocumentSearchAdapter(ConcurrentDictionary<Guid, Document> documents) =>
        _documents = documents;

    public void Index(Document doc) => _documents[doc.Id] = doc;
    public void Remove(Guid id) => _documents.TryRemove(id, out _);

    public Task<IReadOnlyList<Document>> SearchAsync(string query, CancellationToken ct = default)
    {
        var matches = _documents.Values
            .Where(d =>
                d.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (d.Metadata?.Any(kv => kv.Value?.ToString()?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ?? false))
            .ToList();
        return Task.FromResult<IReadOnlyList<Document>>(matches);
    }

    public Task<IReadOnlyList<Document>> FindByFolderAsync(Guid folderId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Document>>(_documents.Values
            .Where(d => Guid.TryParse(d.FolderId, out var f) && f == folderId)
            .ToList());

    public Task<IReadOnlyList<Document>> FindByOwnerAsync(string owner, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Document>>(_documents.Values
            .Where(d => d.Owner == owner)
            .ToList());

    public Task<IReadOnlyList<Document>> FindByStatusAsync(DocumentStatus status, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Document>>(_documents.Values
            .Where(d => d.Status == status)
            .ToList());
}

/// <summary>
/// In-memory RBAC permission store backed by a tuple set. Mirrors Java
/// <c>LocalPermissionAdapter</c>.
/// </summary>
[EcmAdapter("local-permissions",
    Description = "In-memory permission adapter",
    Priority = 0,
    SupportedFeatures = AdapterFeature.Permissions)]
public sealed class LocalPermissionAdapter : IPermissionPort
{
    private readonly ConcurrentDictionary<(string principal, string resource, string action), byte> _grants = new();

    public Task GrantAsync(string principal, string resource, string action, CancellationToken ct = default)
    {
        _grants[(principal, resource, action)] = 1;
        return Task.CompletedTask;
    }

    public Task RevokeAsync(string principal, string resource, string action, CancellationToken ct = default)
    {
        _grants.TryRemove((principal, resource, action), out _);
        return Task.CompletedTask;
    }

    public Task<bool> CheckAsync(string principal, string resource, string action, CancellationToken ct = default) =>
        Task.FromResult(_grants.ContainsKey((principal, resource, action)));
}
