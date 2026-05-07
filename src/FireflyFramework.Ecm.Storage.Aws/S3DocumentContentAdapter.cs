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

using System.Runtime.CompilerServices;
using Amazon.S3;
using Amazon.S3.Model;
using FireflyFramework.Ecm.Adapters;
using FireflyFramework.Ecm.Ports;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Ecm.Storage.Aws;

public sealed class S3StorageOptions
{
    public const string SectionName = "Firefly:Ecm:Storage:S3";
    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string? Endpoint { get; set; }
    public string? PathPrefix { get; set; }
}

[EcmAdapter("s3-content",
    Description = "Amazon S3 Document Content Adapter",
    SupportedFeatures = AdapterFeature.ContentStorage | AdapterFeature.Streaming | AdapterFeature.CloudStorage,
    RequiredProperties = new[] { "BucketName", "Region" },
    OptionalProperties = new[] { "AccessKey", "SecretKey", "Endpoint", "PathPrefix" })]
public sealed class S3DocumentContentAdapter : IDocumentContentPort
{
    private readonly IAmazonS3 _client;
    private readonly S3StorageOptions _opt;

    public S3DocumentContentAdapter(IAmazonS3 client, IOptions<S3StorageOptions> options)
    {
        _client = client;
        _opt = options.Value;
    }

    private string Key(Guid id) => string.IsNullOrEmpty(_opt.PathPrefix) ? id.ToString() : $"{_opt.PathPrefix.TrimEnd('/')}/{id}";

    public async Task<Stream> GetContentAsync(Guid documentId, CancellationToken ct = default)
    {
        var resp = await _client.GetObjectAsync(_opt.BucketName, Key(documentId), ct).ConfigureAwait(false);
        return resp.ResponseStream;
    }

    public async Task StoreContentAsync(Guid documentId, Stream content, string mimeType, CancellationToken ct = default)
    {
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _opt.BucketName,
            Key = Key(documentId),
            InputStream = content,
            ContentType = mimeType,
            AutoCloseStream = false,
        }, ct).ConfigureAwait(false);
    }

    public async Task<bool> DeleteContentAsync(Guid documentId, CancellationToken ct = default)
    {
        await _client.DeleteObjectAsync(_opt.BucketName, Key(documentId), ct).ConfigureAwait(false);
        return true;
    }

    public async IAsyncEnumerable<byte[]> StreamAsync(Guid documentId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var resp = await _client.GetObjectAsync(_opt.BucketName, Key(documentId), ct).ConfigureAwait(false);
        await using var stream = resp.ResponseStream;
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) > 0)
        {
            yield return buffer[..read];
        }
    }

    public async Task<Stream> GetRangeContentAsync(Guid documentId, long start, long end, CancellationToken ct = default)
    {
        var resp = await _client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _opt.BucketName,
            Key = Key(documentId),
            ByteRange = new ByteRange(start, end),
        }, ct).ConfigureAwait(false);
        return resp.ResponseStream;
    }
}
