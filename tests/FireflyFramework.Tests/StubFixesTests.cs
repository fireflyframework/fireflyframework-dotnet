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

using System.Text.Json;
using FireflyFramework.Callbacks.Core;
using FireflyFramework.Callbacks.Interfaces;
using FireflyFramework.Idp;
using FireflyFramework.Idp.InternalDb;
using FireflyFramework.Webhooks.Core;
using FireflyFramework.Webhooks.Core.Services;
using FireflyFramework.Webhooks.Interfaces;
using FireflyFramework.Webhooks.Processor;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FireflyFramework.Tests;

public class StubFixesTests
{
    // ───── Token revocation store (InternalDb) ─────

    [Fact]
    public async Task TokenRevocationStore_marks_jti_as_revoked_until_expiry()
    {
        var store = new InMemoryTokenRevocationStore();
        var jti = "jti-123";
        await store.RevokeAsync(jti, DateTimeOffset.UtcNow.AddMinutes(5));
        (await store.IsRevokedAsync(jti)).Should().BeTrue();
        (await store.IsRevokedAsync("never-revoked")).Should().BeFalse();
    }

    [Fact]
    public async Task TokenRevocationStore_purges_expired_entries_on_query()
    {
        var store = new InMemoryTokenRevocationStore();
        var jti = "jti-old";
        await store.RevokeAsync(jti, DateTimeOffset.UtcNow.AddSeconds(-1));
        (await store.IsRevokedAsync(jti)).Should().BeFalse();
    }

    // ───── Role catalog (InternalDb) ─────

    [Fact]
    public async Task RoleCatalog_round_trips_role_set()
    {
        var catalog = new InMemoryRoleCatalog();
        await catalog.AddAsync("admin");
        await catalog.AddAsync("auditor");
        await catalog.AddAsync("admin");  // duplicate

        var roles = await catalog.ListAsync();
        roles.Should().BeEquivalentTo(new[] { "admin", "auditor" });

        await catalog.RemoveAsync("auditor");
        (await catalog.ListAsync()).Should().BeEquivalentTo(new[] { "admin" });
    }

    // ───── Webhook processing pipeline ─────

    private static WebhookEventDto NewEvent(string provider = "test", string payload = """{"id":1}""")
    {
        using var doc = JsonDocument.Parse(payload);
        return new WebhookEventDto(
            EventId: Guid.NewGuid().ToString(),
            ProviderName: provider,
            Payload: doc.RootElement.Clone(),
            Headers: new Dictionary<string, string>(),
            QueryParams: new Dictionary<string, string>(),
            ReceivedAt: DateTimeOffset.UtcNow,
            SourceIp: null,
            HttpMethod: "POST");
    }

    private sealed class StubProcessor : IWebhookProcessor
    {
        public bool Called { get; private set; }
        public bool Succeed { get; init; } = true;
        public Task<WebhookProcessingResult> ProcessAsync(WebhookProcessingContext context, CancellationToken ct = default)
        {
            Called = true;
            return Task.FromResult(new WebhookProcessingResult(Succeed, ShouldRetry: !Succeed, RetryAfter: null, Message: Succeed ? "ok" : "boom"));
        }
    }

    [Fact]
    public async Task Pipeline_accepts_when_no_processor_registered()
    {
        var svc = new WebhookProcessingService(NullLogger<WebhookProcessingService>.Instance);
        var resp = await svc.ProcessAsync(NewEvent());
        resp.Status.Should().Be("ACCEPTED");
    }

    [Fact]
    public async Task Pipeline_routes_to_registered_processor_and_marks_processed()
    {
        var processor = new StubProcessor();
        var registry = new InMemoryWebhookProcessorRegistry(new[]
        {
            new KeyValuePair<string, IWebhookProcessor>("stripe", processor),
        });

        var svc = new WebhookProcessingService(NullLogger<WebhookProcessingService>.Instance, processors: registry);
        var resp = await svc.ProcessAsync(NewEvent(provider: "stripe"));

        processor.Called.Should().BeTrue();
        resp.Status.Should().Be("PROCESSED");
    }

    [Fact]
    public async Task Pipeline_dlqs_when_processor_returns_failure()
    {
        var processor = new StubProcessor { Succeed = false };
        var registry = new InMemoryWebhookProcessorRegistry(new[]
        {
            new KeyValuePair<string, IWebhookProcessor>("stripe", processor),
        });
        var dlq = new InMemoryDeadLetterQueueService(NullLogger<InMemoryDeadLetterQueueService>.Instance);

        var svc = new WebhookProcessingService(NullLogger<WebhookProcessingService>.Instance, processors: registry, dlq: dlq);
        var resp = await svc.ProcessAsync(NewEvent(provider: "stripe"));

        resp.Status.Should().Be("FAILED");

        var dl = new List<DeadLetterEntry>();
        await foreach (var e in dlq.ListAsync()) dl.Add(e);
        dl.Should().HaveCount(1);
    }

    [Fact]
    public async Task Pipeline_rejects_oversize_payload()
    {
        var validator = new WebhookValidator(Options.Create(new WebhookSecurityOptions { MaxPayloadBytes = 5 }));
        var svc = new WebhookProcessingService(NullLogger<WebhookProcessingService>.Instance, validator: validator);

        var resp = await svc.ProcessAsync(NewEvent(payload: """{"oversize":"yes"}"""));
        resp.Status.Should().Be("REJECTED");
        resp.Message.Should().Be("payload-too-large");
    }

    // ───── Callback configuration store ─────

    private static CallbackConfigurationDto SampleConfig(string? tenantId = null) => new(
        Id: null,
        Name: "test",
        Url: "https://example.com",
        HttpMethod: CallbackHttpMethod.Post,
        Status: CallbackStatus.Active,
        SubscribedEventTypes: new[] { "order.created" },
        CustomHeaders: null,
        Secret: null,
        SignatureEnabled: false,
        SignatureHeader: null,
        MaxRetries: 3,
        RetryDelayMs: 100,
        RetryBackoffMultiplier: 2.0,
        TimeoutMs: 5000,
        Active: true,
        TenantId: tenantId,
        FilterExpression: null,
        Metadata: null,
        FailureThreshold: 5,
        FailureCount: 0,
        LastSuccessAt: null,
        LastFailureAt: null,
        CreatedAt: default,
        UpdatedAt: null,
        CreatedBy: null,
        UpdatedBy: null);

    [Fact]
    public async Task CallbackStore_round_trip_create_get_delete()
    {
        var store = new InMemoryCallbackConfigurationStore();
        var created = await store.CreateAsync(SampleConfig());
        created.Id.Should().NotBe(Guid.Empty);

        var fetched = await store.GetAsync(created.Id!.Value);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("test");

        (await store.DeleteAsync(created.Id!.Value)).Should().BeTrue();
        (await store.GetAsync(created.Id.Value)).Should().BeNull();
    }

    [Fact]
    public async Task CallbackStore_filters_by_tenant_and_event()
    {
        var store = new InMemoryCallbackConfigurationStore();
        await store.CreateAsync(SampleConfig(tenantId: "t1"));
        await store.CreateAsync(SampleConfig(tenantId: "t2"));

        var t1 = await store.ListAsync("t1");
        t1.Should().HaveCount(1);

        var byEvent = await store.FindBySubscribedEventAsync("order.created", "t1");
        byEvent.Should().HaveCount(1);

        var unknownEvent = await store.FindBySubscribedEventAsync("does.not.exist");
        unknownEvent.Should().BeEmpty();
    }
}
