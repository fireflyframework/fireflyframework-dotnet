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

using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Webhooks.Core.Services;

public sealed class CompressionOptions
{
    public bool Enabled { get; set; } = true;
    public int MinSizeBytes { get; set; } = 1024;
}

/// <summary>
/// GZIP-based payload compression for webhook persistence and forwarding. Mirrors Java
/// <c>WebhookCompressionService</c>.
/// </summary>
public sealed class WebhookCompressionService
{
    private readonly CompressionOptions _options;

    public WebhookCompressionService(IOptions<CompressionOptions> options) => _options = options.Value;

    public byte[] Compress(string payload)
    {
        if (!_options.Enabled || payload.Length < _options.MinSizeBytes)
        {
            return Encoding.UTF8.GetBytes(payload);
        }

        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        using (var writer = new StreamWriter(gzip, Encoding.UTF8))
        {
            writer.Write(payload);
        }

        return output.ToArray();
    }

    public string Decompress(byte[] data)
    {
        if (!IsGzip(data))
        {
            return Encoding.UTF8.GetString(data);
        }

        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static bool IsGzip(byte[] data) => data.Length >= 2 && data[0] == 0x1F && data[1] == 0x8B;
}
