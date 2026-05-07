// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using FireflyFramework.Ecm.Adapters;
using FireflyFramework.Ecm.Storage.Aws;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// NSubstitute-mocked tests for <see cref="S3DocumentContentAdapter"/>. Verify each port
/// method dispatches the correct AWS S3 request and reads the correct fields from the
/// response, without ever touching real S3.
/// </summary>
public sealed class S3DocumentContentAdapterTests
{
    private readonly IAmazonS3 _s3;
    private readonly S3DocumentContentAdapter _adapter;
    private readonly Guid _docId = Guid.NewGuid();

    public S3DocumentContentAdapterTests()
    {
        _s3 = Substitute.For<IAmazonS3>();
        var options = Options.Create(new S3StorageOptions
        {
            BucketName = "firefly-bucket",
            Region = "eu-west-1",
            PathPrefix = "documents",
        });
        _adapter = new S3DocumentContentAdapter(_s3, options);
    }

    [Fact]
    public async Task GetContentAsync_DispatchesGetObject_ReturnsResponseStream()
    {
        var bytes = Encoding.UTF8.GetBytes("hello");
        _s3.GetObjectAsync("firefly-bucket", $"documents/{_docId}", Arg.Any<CancellationToken>())
            .Returns(new GetObjectResponse { ResponseStream = new MemoryStream(bytes) });

        var stream = await _adapter.GetContentAsync(_docId);

        using var reader = new StreamReader(stream);
        Assert.Equal("hello", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task StoreContentAsync_PutsObjectWithCorrectKeyAndMimeType()
    {
        var content = new MemoryStream(Encoding.UTF8.GetBytes("payload"));

        await _adapter.StoreContentAsync(_docId, content, "text/plain");

        await _s3.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.BucketName == "firefly-bucket" &&
                r.Key == $"documents/{_docId}" &&
                r.ContentType == "text/plain" &&
                r.AutoCloseStream == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteContentAsync_DispatchesDeleteAndReturnsTrue()
    {
        _s3.DeleteObjectAsync("firefly-bucket", $"documents/{_docId}", Arg.Any<CancellationToken>())
            .Returns(new DeleteObjectResponse());

        var ok = await _adapter.DeleteContentAsync(_docId);

        Assert.True(ok);
        await _s3.Received(1).DeleteObjectAsync("firefly-bucket", $"documents/{_docId}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRangeContentAsync_PassesByteRangeOnRequest()
    {
        _s3.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetObjectResponse { ResponseStream = new MemoryStream(new byte[] { 0x01, 0x02 }) });

        await _adapter.GetRangeContentAsync(_docId, 100, 200);

        await _s3.Received(1).GetObjectAsync(
            Arg.Is<GetObjectRequest>(r =>
                r.BucketName == "firefly-bucket" &&
                r.Key == $"documents/{_docId}" &&
                r.ByteRange.Start == 100 && r.ByteRange.End == 200),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StreamAsync_ReadsTheResponseStreamInChunks()
    {
        var payload = new byte[20_000];
        Random.Shared.NextBytes(payload);
        _s3.GetObjectAsync("firefly-bucket", $"documents/{_docId}", Arg.Any<CancellationToken>())
            .Returns(new GetObjectResponse { ResponseStream = new MemoryStream(payload) });

        var collected = new MemoryStream();
        await foreach (var chunk in _adapter.StreamAsync(_docId))
        {
            collected.Write(chunk, 0, chunk.Length);
        }

        Assert.Equal(payload, collected.ToArray());
    }

    [Fact]
    public void EcmAdapter_AttributeReports_Capabilities()
    {
        var info = AdapterIntrospection.GetInfo(typeof(S3DocumentContentAdapter));

        Assert.NotNull(info);
        Assert.Equal("s3-content", info!.Type);
        Assert.True(info.SupportedFeatures.HasFlag(AdapterFeature.ContentStorage));
        Assert.True(info.SupportedFeatures.HasFlag(AdapterFeature.Streaming));
        Assert.True(info.SupportedFeatures.HasFlag(AdapterFeature.CloudStorage));
    }
}
