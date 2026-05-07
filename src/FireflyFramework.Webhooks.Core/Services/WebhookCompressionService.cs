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
