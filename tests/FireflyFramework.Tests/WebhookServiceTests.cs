using System.Net;
using System.Text;
using System.Text.Json;
using FireflyFramework.Webhooks.Core.Services;
using FireflyFramework.Webhooks.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FireflyFramework.Tests;

public class WebhookServiceTests
{
    private static WebhookEventDto NewEvent(string? sourceIp = null, string payload = """{"hello":"world"}""")
    {
        using var doc = JsonDocument.Parse(payload);
        return new WebhookEventDto(
            EventId: "evt_1",
            ProviderName: "test",
            Payload: doc.RootElement.Clone(),
            Headers: new Dictionary<string, string>(),
            QueryParams: new Dictionary<string, string>(),
            ReceivedAt: DateTimeOffset.UtcNow,
            SourceIp: sourceIp,
            HttpMethod: "POST");
    }

    // ───── Compression ─────

    [Fact]
    public void Compression_round_trip_preserves_payload()
    {
        var svc = new WebhookCompressionService(Options.Create(new CompressionOptions { Enabled = true, MinSizeBytes = 0 }));
        var original = new string('a', 4096);

        var compressed = svc.Compress(original);
        var decompressed = svc.Decompress(compressed);

        compressed.Length.Should().BeLessThan(Encoding.UTF8.GetByteCount(original));
        decompressed.Should().Be(original);
    }

    [Fact]
    public void Compression_skips_below_threshold()
    {
        var svc = new WebhookCompressionService(Options.Create(new CompressionOptions { Enabled = true, MinSizeBytes = 1024 }));
        var compressed = svc.Compress("short");
        compressed.Should().BeEquivalentTo(Encoding.UTF8.GetBytes("short"));
    }

    [Fact]
    public void Compression_disabled_returns_raw_bytes()
    {
        var svc = new WebhookCompressionService(Options.Create(new CompressionOptions { Enabled = false, MinSizeBytes = 0 }));
        var compressed = svc.Compress("hello");
        compressed.Should().BeEquivalentTo(Encoding.UTF8.GetBytes("hello"));
    }

    // ───── Rate limiter ─────

    [Fact]
    public async Task RateLimiter_blocks_after_burst_exhaustion()
    {
        var svc = new WebhookRateLimitService(
            Options.Create(new RateLimitOptionsConfiguration
            {
                Enabled = true,
                BurstSize = 2,
                RequestsPerSecond = 1,
                Window = TimeSpan.FromSeconds(60),
            }),
            NullLogger<WebhookRateLimitService>.Instance);

        (await svc.TryAcquireAsync("p1")).Should().BeTrue();
        (await svc.TryAcquireAsync("p1")).Should().BeTrue();
        (await svc.TryAcquireAsync("p1")).Should().BeFalse();
    }

    [Fact]
    public async Task RateLimiter_disabled_always_allows()
    {
        var svc = new WebhookRateLimitService(
            Options.Create(new RateLimitOptionsConfiguration { Enabled = false }),
            NullLogger<WebhookRateLimitService>.Instance);

        for (var i = 0; i < 10; i++)
        {
            (await svc.TryAcquireAsync("p")).Should().BeTrue();
        }
    }

    [Fact]
    public async Task RateLimiter_separates_buckets_by_provider()
    {
        var svc = new WebhookRateLimitService(
            Options.Create(new RateLimitOptionsConfiguration
            {
                Enabled = true,
                BurstSize = 1,
                RequestsPerSecond = 1,
                Window = TimeSpan.FromSeconds(60),
            }),
            NullLogger<WebhookRateLimitService>.Instance);

        (await svc.TryAcquireAsync("a")).Should().BeTrue();
        (await svc.TryAcquireAsync("b")).Should().BeTrue();
        (await svc.TryAcquireAsync("a")).Should().BeFalse();
    }

    // ───── Batching ─────

    [Fact]
    public async Task Batching_emits_batch_on_size_threshold()
    {
        var svc = new WebhookBatchingService(
            Options.Create(new BatchingOptions { Enabled = true, BatchSize = 3, BatchWindow = TimeSpan.FromSeconds(60) }),
            NullLogger<WebhookBatchingService>.Instance);

        for (var i = 0; i < 3; i++)
        {
            await svc.EnqueueAsync(NewEvent());
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var batch in svc.ReadBatchesAsync(cts.Token))
        {
            batch.Should().HaveCount(3);
            return;
        }

        Assert.Fail("expected at least one batch");
    }

    // ───── Metadata enrichment ─────

    [Fact]
    public void Enrichment_adds_payload_checksum_and_size()
    {
        var svc = new WebhookMetadataEnrichmentService();
        var enriched = svc.Enrich(NewEvent());

        enriched.EnrichedMetadata.Should().NotBeNull();
        enriched.EnrichedMetadata!.Should().ContainKey("payloadSha256");
        enriched.EnrichedMetadata.Should().ContainKey("payloadBytes");
    }

    [Fact]
    public void Enrichment_computes_transit_latency_when_timestamp_header_present()
    {
        var svc = new WebhookMetadataEnrichmentService();
        var sentAt = DateTimeOffset.UtcNow.AddMilliseconds(-250);
        var evt = NewEvent();
        evt.Headers["x-event-timestamp"] = sentAt.ToUnixTimeMilliseconds().ToString();

        var enriched = svc.Enrich(evt);

        var meta = enriched.EnrichedMetadata!;
        meta.Should().ContainKey("transitLatencyMs");
        ((long)meta["transitLatencyMs"]!).Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void Enrichment_records_ip_family()
    {
        var svc = new WebhookMetadataEnrichmentService();
        var enriched = svc.Enrich(NewEvent(sourceIp: "127.0.0.1"));
        enriched.EnrichedMetadata!["ipFamily"].Should().Be("InterNetwork");
        enriched.EnrichedMetadata["isLoopback"].Should().Be(true);
    }

    // ───── DLQ ─────

    [Fact]
    public async Task DeadLetter_stores_then_lists_then_removes_entries()
    {
        var dlq = new InMemoryDeadLetterQueueService(NullLogger<InMemoryDeadLetterQueueService>.Instance);
        await dlq.PublishAsync(NewEvent(), reason: "bad-signature", attempts: 3);

        var listed = new List<DeadLetterEntry>();
        await foreach (var e in dlq.ListAsync()) listed.Add(e);
        listed.Should().HaveCount(1);

        var id = listed[0].Id;
        (await dlq.RemoveAsync(id)).Should().BeTrue();

        listed.Clear();
        await foreach (var e in dlq.ListAsync()) listed.Add(e);
        listed.Should().BeEmpty();
    }

    [Fact]
    public async Task DeadLetter_redrive_removes_entry()
    {
        var dlq = new InMemoryDeadLetterQueueService(NullLogger<InMemoryDeadLetterQueueService>.Instance);
        await dlq.PublishAsync(NewEvent(), reason: "x", attempts: 1);

        var entries = new List<DeadLetterEntry>();
        await foreach (var e in dlq.ListAsync()) entries.Add(e);
        var id = entries[0].Id;

        (await dlq.RedriveAsync(id)).Should().Be(1);
        (await dlq.RedriveAsync(id)).Should().Be(0);  // already removed
    }

    // ───── Validator (CIDR + payload size) ─────

    [Fact]
    public void Validator_accepts_within_payload_limit()
    {
        var v = new WebhookValidator(Options.Create(new WebhookSecurityOptions { MaxPayloadBytes = 1024 }));
        v.Validate(NewEvent(), payloadByteCount: 100).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_rejects_oversize_payload()
    {
        var v = new WebhookValidator(Options.Create(new WebhookSecurityOptions { MaxPayloadBytes = 50 }));
        var result = v.Validate(NewEvent(), payloadByteCount: 100);
        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be("payload-too-large");
    }

    [Fact]
    public void Validator_rejects_blacklisted_ip()
    {
        var opts = new WebhookSecurityOptions { IpBlacklist = ["10.0.0.0/8"] };
        var v = new WebhookValidator(Options.Create(opts));
        var result = v.Validate(NewEvent(sourceIp: "10.5.6.7"), 100);
        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be("ip-blacklisted");
    }

    [Fact]
    public void Validator_rejects_ip_outside_whitelist()
    {
        var opts = new WebhookSecurityOptions { IpWhitelist = ["192.168.1.0/24"] };
        var v = new WebhookValidator(Options.Create(opts));
        var result = v.Validate(NewEvent(sourceIp: "10.0.0.1"), 100);
        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be("ip-not-allowed");
    }

    [Fact]
    public void Validator_accepts_whitelisted_ip()
    {
        var opts = new WebhookSecurityOptions { IpWhitelist = ["192.168.1.0/24"] };
        var v = new WebhookValidator(Options.Create(opts));
        v.Validate(NewEvent(sourceIp: "192.168.1.50"), 100).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("10.0.0.5", "10.0.0.0/8", true)]
    [InlineData("11.0.0.5", "10.0.0.0/8", false)]
    [InlineData("192.168.1.50", "192.168.1.0/24", true)]
    [InlineData("192.168.2.1", "192.168.1.0/24", false)]
    [InlineData("192.168.1.50", "192.168.1.50", true)]
    public void InCidr_matches_expected(string ipString, string cidr, bool expected) =>
        WebhookValidator.InCidr(IPAddress.Parse(ipString), cidr).Should().Be(expected);
}
