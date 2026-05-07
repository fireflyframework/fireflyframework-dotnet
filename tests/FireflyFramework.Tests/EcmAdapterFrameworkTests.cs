using FireflyFramework.Ecm.Adapters;
using FireflyFramework.Ecm.Domain;
using FireflyFramework.Ecm.Ports;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// Tests the ECM adapter discovery / selection / introspection pieces in
/// <c>FireflyFramework.Ecm.Adapters</c>. These are the runtime building blocks every
/// real ECM adapter (Aws S3, Azure Blob, e-signature) plugs into.
/// </summary>
public sealed class EcmAdapterFrameworkTests
{
    [Fact]
    public void Introspection_ExtractsAttribute_FromDecoratedAdapter()
    {
        var info = AdapterIntrospection.GetInfo(typeof(NoOpAdapter));

        Assert.NotNull(info);
        Assert.Equal("noop", info!.Type);
        Assert.True(info.Enabled);
        Assert.Contains("No-op", info.Description);
        Assert.True(info.SupportedFeatures.HasFlag(AdapterFeature.DocumentCrud));
        Assert.True(info.SupportedFeatures.HasFlag(AdapterFeature.ContentStorage));
    }

    [Fact]
    public void Introspection_ReturnsNull_ForUnannotatedClass()
    {
        Assert.Null(AdapterIntrospection.GetInfo(typeof(string)));
    }

    [Fact]
    public void Registry_RegistersAndExposesAdapter()
    {
        var registry = new AdapterRegistry();
        var noop = new NoOpAdapter();

        registry.Register(noop);

        Assert.Single(registry.All());
        Assert.NotNull(registry.GetInfo("noop"));
        Assert.Same(noop, registry.ResolveByType<IDocumentPort>("noop"));
    }

    [Fact]
    public void Registry_Register_ThrowsOnUnannotatedAdapter()
    {
        var registry = new AdapterRegistry();
        Assert.Throws<InvalidOperationException>(() => registry.Register(new object()));
    }

    [Fact]
    public void Registry_SupportingFeature_FiltersByCapability()
    {
        var registry = new AdapterRegistry();
        registry.Register(new NoOpAdapter());

        Assert.Single(registry.SupportingFeature(AdapterFeature.ContentStorage));
        Assert.Empty(registry.SupportingFeature(AdapterFeature.SignatureValidation));
    }

    [Fact]
    public void Registry_Resolve_ReturnsEveryAdapterImplementingThePort()
    {
        var registry = new AdapterRegistry();
        registry.Register(new NoOpAdapter());

        var docPorts = registry.Resolve<IDocumentPort>();
        var contentPorts = registry.Resolve<IDocumentContentPort>();

        Assert.Single(docPorts);
        Assert.Single(contentPorts);
    }

    [Fact]
    public void Selector_PickByFeature_ReturnsHighestPriorityAdapter()
    {
        var registry = new AdapterRegistry();
        registry.Register(new NoOpAdapter());
        registry.Register(new HighPriorityFakeAdapter());

        var selector = new AdapterSelector<IDocumentPort>(registry);
        var picked = selector.PickByFeature(AdapterFeature.DocumentCrud);

        Assert.IsType<HighPriorityFakeAdapter>(picked);
    }

    [Fact]
    public void Selector_PickByType_HonoursExplicitChoice()
    {
        var registry = new AdapterRegistry();
        registry.Register(new NoOpAdapter());
        var selector = new AdapterSelector<IDocumentPort>(registry);

        Assert.NotNull(selector.PickByType("noop"));
        Assert.Null(selector.PickByType("does-not-exist"));
    }

    [Fact]
    public async Task NoOpAdapter_ContentRoundTrip_HasCorrectDefaults()
    {
        var noop = new NoOpAdapter();
        var now = DateTimeOffset.UtcNow;
        var doc = new Document(
            Id: Guid.NewGuid(),
            Name: "demo.txt",
            Owner: null,
            Status: DocumentStatus.Active,
            CreatedAt: now,
            ModifiedAt: now,
            ContentHash: null,
            SizeBytes: 0,
            MimeType: "text/plain",
            FolderId: null,
            Metadata: null);

        Assert.Same(doc, await noop.CreateAsync(doc));
        Assert.Null(await noop.GetAsync(doc.Id));
        Assert.True(await noop.DeleteAsync(doc.Id));
        Assert.False(await noop.ExistsAsync(doc.Id));

        await noop.StoreContentAsync(doc.Id, Stream.Null, "text/plain");
        Assert.Same(Stream.Null, await noop.GetContentAsync(doc.Id));
    }

    [EcmAdapter("hi-pri", Priority = 100, SupportedFeatures = AdapterFeature.DocumentCrud | AdapterFeature.ContentStorage)]
    private sealed class HighPriorityFakeAdapter : IDocumentPort, IDocumentContentPort
    {
        public Task<Document> CreateAsync(Document d, CancellationToken ct = default) => Task.FromResult(d);
        public Task<Document?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Document?>(null);
        public Task<Document> UpdateAsync(Document d, CancellationToken ct = default) => Task.FromResult(d);
        public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(false);

        public Task<Stream> GetContentAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Stream>(Stream.Null);
        public Task StoreContentAsync(Guid id, Stream s, string mime, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> DeleteContentAsync(Guid id, CancellationToken ct = default) => Task.FromResult(true);
        public async IAsyncEnumerable<byte[]> StreamAsync(Guid id, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default) { await Task.CompletedTask; yield break; }
        public Task<Stream> GetRangeContentAsync(Guid id, long start, long end, CancellationToken ct = default) => Task.FromResult<Stream>(Stream.Null);
    }
}
