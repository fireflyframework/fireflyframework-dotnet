using Azure.Storage.Blobs;
using FireflyFramework.Ecm.Adapters;
using FireflyFramework.Ecm.Storage.Azure;
using NSubstitute;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// Tests for <see cref="AzureBlobDocumentContentAdapter"/> that don't require a real Azure
/// Storage account. The adapter accepts a pre-built <see cref="BlobContainerClient"/> via the
/// testing constructor, which lets us pin the blob-name convention and the
/// <c>EcmAdapter</c> attribute. Full upload/download semantics are SDK behaviour and out of
/// scope for these unit tests; integration-shape testing belongs against Azurite.
/// </summary>
public sealed class AzureBlobDocumentContentAdapterTests
{
    [Fact]
    public void EcmAdapter_AttributeReports_Capabilities()
    {
        var info = AdapterIntrospection.GetInfo(typeof(AzureBlobDocumentContentAdapter));

        Assert.NotNull(info);
        Assert.Equal("azure-blob-content", info!.Type);
        Assert.True(info.SupportedFeatures.HasFlag(AdapterFeature.ContentStorage));
        Assert.True(info.SupportedFeatures.HasFlag(AdapterFeature.Streaming));
        Assert.True(info.SupportedFeatures.HasFlag(AdapterFeature.CloudStorage));
    }

    [Fact]
    public void Constructor_RejectsNullContainer() =>
        Assert.Throws<ArgumentNullException>(() =>
            new AzureBlobDocumentContentAdapter((BlobContainerClient)null!));

    [Fact]
    public async Task DeleteContentAsync_AddressesBlobByDocumentId()
    {
        var documentId = Guid.NewGuid();
        var container = Substitute.For<BlobContainerClient>();
        var blob = Substitute.For<BlobClient>();
        container.GetBlobClient(documentId.ToString()).Returns(blob);
        blob.DeleteIfExistsAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(global::Azure.Response.FromValue(true, Substitute.For<global::Azure.Response>()));

        var adapter = new AzureBlobDocumentContentAdapter(container);
        var deleted = await adapter.DeleteContentAsync(documentId);

        Assert.True(deleted);
        container.Received(1).GetBlobClient(documentId.ToString());
    }
}
