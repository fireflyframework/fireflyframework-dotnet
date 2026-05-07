using System.Net;
using System.Security.Cryptography;
using System.Text;
using FireflyFramework.Webhooks.Interfaces;

namespace FireflyFramework.Webhooks.Core.Services;

/// <summary>
/// Enriches inbound webhook events with derived metadata: payload checksum, latency
/// from a header timestamp, normalised user-agent, IP locality. Mirrors Java
/// <c>WebhookMetadataEnrichmentService</c>.
/// </summary>
public sealed class WebhookMetadataEnrichmentService
{
    public WebhookEventDto Enrich(WebhookEventDto evt)
    {
        var enrichedMetadata = new Dictionary<string, object?>(evt.EnrichedMetadata ?? new());

        // payload checksum
        var payloadJson = evt.Payload.GetRawText();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson));
        enrichedMetadata["payloadSha256"] = Convert.ToHexString(hash).ToLowerInvariant();
        enrichedMetadata["payloadBytes"] = Encoding.UTF8.GetByteCount(payloadJson);

        // latency
        if (evt.Headers.TryGetValue("x-event-timestamp", out var ts) && long.TryParse(ts, out var unixMs))
        {
            var sentAt = DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
            enrichedMetadata["transitLatencyMs"] = (long)(evt.ReceivedAt - sentAt).TotalMilliseconds;
        }

        // IP family
        if (evt.SourceIp is not null && IPAddress.TryParse(evt.SourceIp, out var ip))
        {
            enrichedMetadata["ipFamily"] = ip.AddressFamily.ToString();
            enrichedMetadata["isLoopback"] = IPAddress.IsLoopback(ip);
        }

        // User agent normalisation
        if (evt.Headers.TryGetValue("user-agent", out var ua))
        {
            enrichedMetadata["userAgent"] = ua;
        }

        return evt with { EnrichedMetadata = enrichedMetadata };
    }
}
