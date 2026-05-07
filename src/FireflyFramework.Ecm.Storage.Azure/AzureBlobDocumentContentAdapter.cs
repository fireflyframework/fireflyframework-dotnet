using System.Runtime.CompilerServices;
using Azure.Storage.Blobs;
using FireflyFramework.Ecm.Adapters;
using FireflyFramework.Ecm.Ports;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Ecm.Storage.Azure;

public sealed class AzureBlobOptions
{
    public const string SectionName = "Firefly:Ecm:Storage:AzureBlob";
    public string ContainerName { get; set; } = string.Empty;
    public string? ConnectionString { get; set; }
    public string? AccountUrl { get; set; }
}

[EcmAdapter("azure-blob-content",
    Description = "Azure Blob Storage Document Content Adapter",
    SupportedFeatures = AdapterFeature.ContentStorage | AdapterFeature.Streaming | AdapterFeature.CloudStorage,
    RequiredProperties = new[] { "ContainerName" },
    OptionalProperties = new[] { "ConnectionString", "AccountUrl" })]
public sealed class AzureBlobDocumentContentAdapter : IDocumentContentPort
{
    private readonly BlobContainerClient _container;

    /// <summary>Production constructor — wires the container client from connection-string or DefaultAzureCredential.</summary>
    public AzureBlobDocumentContentAdapter(IOptions<AzureBlobOptions> options)
        : this(BuildContainer(options.Value)) { }

    /// <summary>
    /// Testing / advanced-DI constructor — accepts an explicit <see cref="BlobContainerClient"/>. Use this when
    /// you want to share a client across adapters or substitute a fake (NSubstitute) in tests.
    /// </summary>
    public AzureBlobDocumentContentAdapter(BlobContainerClient container)
    {
        ArgumentNullException.ThrowIfNull(container);
        _container = container;
    }

    private static BlobContainerClient BuildContainer(AzureBlobOptions opt) => opt.ConnectionString is not null
        ? new BlobContainerClient(opt.ConnectionString, opt.ContainerName)
        : new BlobContainerClient(new Uri($"{opt.AccountUrl}/{opt.ContainerName}"), new global::Azure.Identity.DefaultAzureCredential());

    public async Task<Stream> GetContentAsync(Guid documentId, CancellationToken ct = default) =>
        await _container.GetBlobClient(documentId.ToString()).OpenReadAsync(cancellationToken: ct).ConfigureAwait(false);

    public async Task StoreContentAsync(Guid documentId, Stream content, string mimeType, CancellationToken ct = default)
    {
        await _container.GetBlobClient(documentId.ToString())
            .UploadAsync(content, new global::Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = mimeType }, cancellationToken: ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> DeleteContentAsync(Guid documentId, CancellationToken ct = default) =>
        await _container.GetBlobClient(documentId.ToString()).DeleteIfExistsAsync(cancellationToken: ct).ConfigureAwait(false);

    public async IAsyncEnumerable<byte[]> StreamAsync(Guid documentId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var stream = await _container.GetBlobClient(documentId.ToString())
            .OpenReadAsync(cancellationToken: ct).ConfigureAwait(false);
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) > 0)
        {
            yield return buffer[..read];
        }
    }

    public async Task<Stream> GetRangeContentAsync(Guid documentId, long start, long end, CancellationToken ct = default)
    {
        var resp = await _container.GetBlobClient(documentId.ToString())
            .DownloadStreamingAsync(new global::Azure.Storage.Blobs.Models.BlobDownloadOptions
            {
                Range = new global::Azure.HttpRange(start, end - start + 1),
            }, ct).ConfigureAwait(false);
        return resp.Value.Content;
    }
}
