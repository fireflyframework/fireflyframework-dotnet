using FireflyFramework.Ecm.Domain;
using FireflyFramework.Ecm.Ports;

namespace FireflyFramework.Ecm.Adapters;

/// <summary>
/// Discards everything, returns sensible no-op responses. Useful for tests, dry-runs,
/// and as a safety net when no real adapter is configured for an optional capability.
/// Mirrors Java <c>NoOpGenericAdapter</c>.
/// </summary>
[EcmAdapter("noop",
    Description = "No-op ECM Adapter — all operations succeed but do nothing",
    Priority = -1000,
    SupportedFeatures = AdapterFeature.DocumentCrud | AdapterFeature.ContentStorage |
                        AdapterFeature.FolderManagement | AdapterFeature.FolderHierarchy |
                        AdapterFeature.Permissions | AdapterFeature.AuditTrail)]
public sealed class NoOpAdapter :
    IDocumentPort, IDocumentContentPort, IFolderPort, IFolderHierarchyPort,
    IPermissionPort, IAuditPort
{
    public Task<Document> CreateAsync(Document document, CancellationToken ct = default) => Task.FromResult(document);
    public Task<Document?> GetAsync(Guid documentId, CancellationToken ct = default) => Task.FromResult<Document?>(null);
    public Task<Document> UpdateAsync(Document document, CancellationToken ct = default) => Task.FromResult(document);
    public Task<bool> DeleteAsync(Guid documentId, CancellationToken ct = default) => Task.FromResult(true);
    public Task<bool> ExistsAsync(Guid documentId, CancellationToken ct = default) => Task.FromResult(false);

    public Task<Stream> GetContentAsync(Guid documentId, CancellationToken ct = default) =>
        Task.FromResult<Stream>(Stream.Null);
    public Task StoreContentAsync(Guid documentId, Stream content, string mimeType, CancellationToken ct = default) =>
        Task.CompletedTask;
    public Task<bool> DeleteContentAsync(Guid documentId, CancellationToken ct = default) => Task.FromResult(true);
    public async IAsyncEnumerable<byte[]> StreamAsync(Guid documentId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }
    public Task<Stream> GetRangeContentAsync(Guid documentId, long start, long end, CancellationToken ct = default) =>
        Task.FromResult<Stream>(Stream.Null);

    Task<Folder> IFolderPort.CreateAsync(Folder folder, CancellationToken ct) => Task.FromResult(folder);
    Task<Folder?> IFolderPort.GetAsync(Guid folderId, CancellationToken ct) => Task.FromResult<Folder?>(null);
    Task<Folder> IFolderPort.UpdateAsync(Folder folder, CancellationToken ct) => Task.FromResult(folder);
    Task<bool> IFolderPort.DeleteAsync(Guid folderId, CancellationToken ct) => Task.FromResult(true);
    public Task<Folder> MoveAsync(Guid folderId, Guid? newParentId, CancellationToken ct = default) =>
        Task.FromResult(new Folder(folderId, "noop", newParentId, "/", null, DateTimeOffset.UtcNow));

    public Task<IReadOnlyList<Folder>> ListChildrenAsync(Guid folderId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Folder>>(Array.Empty<Folder>());
    public Task<Folder?> GetParentAsync(Guid folderId, CancellationToken ct = default) =>
        Task.FromResult<Folder?>(null);
    public Task<string> GetPathAsync(Guid folderId, CancellationToken ct = default) =>
        Task.FromResult("/");

    public Task GrantAsync(string principal, string resource, string action, CancellationToken ct = default) => Task.CompletedTask;
    public Task RevokeAsync(string principal, string resource, string action, CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> CheckAsync(string principal, string resource, string action, CancellationToken ct = default) => Task.FromResult(true);

    public Task<AuditEvent> CreateAuditEventAsync(AuditEvent @event, CancellationToken ct = default) =>
        Task.FromResult(@event);
    public Task<IReadOnlyList<AuditEvent>> ListAuditEventsAsync(string? resourceId = null, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AuditEvent>>(Array.Empty<AuditEvent>());
}
