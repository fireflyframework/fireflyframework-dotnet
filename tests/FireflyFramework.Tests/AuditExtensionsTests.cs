using System.Text.Json;
using FireflyFramework.Callbacks.Core;
using FireflyFramework.Callbacks.Interfaces;
using FireflyFramework.Cqrs.Annotations;
using FireflyFramework.Cqrs.Buses;
using FireflyFramework.Cqrs.Cache;
using FireflyFramework.Ecm.Adapters;
using FireflyFramework.Ecm.Domain;
using FireflyFramework.Ecm.Ports;
using FireflyFramework.Eda.Events;
using FireflyFramework.Eda.ErrorHandling;
using FireflyFramework.Eda.Filtering;
using FireflyFramework.Orchestration.Compensation;
using FireflyFramework.Orchestration.Core;
using FireflyFramework.Orchestration.DeadLetter;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FireflyFramework.Tests;

public class AuditExtensionsTests
{
    // ───── ECM AdapterRegistry / Selector / Local + NoOp adapters ─────

    [Fact]
    public void AdapterRegistry_introspects_attribute_metadata()
    {
        var registry = new AdapterRegistry();
        registry.Register(new NoOpAdapter());

        var info = registry.GetInfo("noop");
        info.Should().NotBeNull();
        info!.SupportedFeatures.Should().HaveFlag(AdapterFeature.DocumentCrud);
        info.Priority.Should().Be(-1000);
    }

    [Fact]
    public void AdapterRegistry_filters_by_feature_flag()
    {
        var registry = new AdapterRegistry();
        registry.Register(new NoOpAdapter());
        registry.Register(new LocalDocumentSearchAdapter());

        var search = registry.SupportingFeature(AdapterFeature.Search);
        search.Should().Contain(i => i.Type == "local-search");
        search.Should().NotContain(i => i.Type == "noop");
    }

    [Fact]
    public void AdapterSelector_picks_highest_priority_adapter()
    {
        var registry = new AdapterRegistry();
        registry.Register(new LocalDocumentSearchAdapter()); // priority 0
        registry.Register(new NoOpAdapter());                // priority -1000

        var selector = new AdapterSelector<IDocumentSearchPort>(registry);
        var picked = selector.PickByFeature(AdapterFeature.Search);
        picked.Should().BeOfType<LocalDocumentSearchAdapter>();
    }

    [Fact]
    public async Task LocalDocumentSearchAdapter_indexes_and_searches()
    {
        var adapter = new LocalDocumentSearchAdapter();
        var doc = new Document(Guid.NewGuid(), "Invoice 2026-01", "alice", DocumentStatus.Active,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, 1024, "application/pdf", null, null);
        adapter.Index(doc);

        var byName = await adapter.SearchAsync("Invoice");
        byName.Should().HaveCount(1);

        var byOwner = await adapter.FindByOwnerAsync("alice");
        byOwner.Should().HaveCount(1);
    }

    [Fact]
    public async Task LocalPermissionAdapter_grants_revokes_checks()
    {
        var perms = new LocalPermissionAdapter();
        await perms.GrantAsync("alice", "document:42", "read");
        (await perms.CheckAsync("alice", "document:42", "read")).Should().BeTrue();
        (await perms.CheckAsync("bob", "document:42", "read")).Should().BeFalse();

        await perms.RevokeAsync("alice", "document:42", "read");
        (await perms.CheckAsync("alice", "document:42", "read")).Should().BeFalse();
    }

    // ───── Callbacks: Router + Stores + DomainAuth + Subscriptions ─────

    private sealed class StubDispatcher : ICallbackDispatcher
    {
        public List<FireflyFramework.Callbacks.Models.CallbackConfigurationEntity> Calls { get; } = new();
        public bool Succeed { get; init; } = true;

        public Task<CallbackExecutionDto> DispatchAsync(FireflyFramework.Callbacks.Models.CallbackConfigurationEntity config, string eventType, string payload, CancellationToken ct = default)
        {
            Calls.Add(config);
            return Task.FromResult(new CallbackExecutionDto(
                Guid.NewGuid(), config.Id, eventType, "src",
                Succeed ? CallbackExecutionStatus.Success : CallbackExecutionStatus.FailedPermanent,
                payload, null, Succeed ? 200 : 500, null, 1, config.MaxRetries, 1, null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        }
    }

    private static CallbackConfigurationDto MakeConfig(string url, params string[] events) => new(
        Id: null, Name: "test", Url: url, HttpMethod: CallbackHttpMethod.Post, Status: CallbackStatus.Active,
        SubscribedEventTypes: events, CustomHeaders: null, Secret: null, SignatureEnabled: false, SignatureHeader: null,
        MaxRetries: 1, RetryDelayMs: 0, RetryBackoffMultiplier: 2, TimeoutMs: 1000, Active: true,
        TenantId: null, FilterExpression: null, Metadata: null, FailureThreshold: 5, FailureCount: 0,
        LastSuccessAt: null, LastFailureAt: null, CreatedAt: default, UpdatedAt: null, CreatedBy: null, UpdatedBy: null);

    [Fact]
    public async Task CallbackRouter_dispatches_to_subscribed_callbacks_only()
    {
        var store = new InMemoryCallbackConfigurationStore();
        await store.CreateAsync(MakeConfig("https://target.example.com", "order.created"));
        await store.CreateAsync(MakeConfig("https://target.example.com", "order.updated"));

        var dispatcher = new StubDispatcher();
        var router = new CallbackRouter(store, dispatcher, NullLogger<CallbackRouter>.Instance);

        var results = await router.RouteAsync("order.created", "{}");
        results.Should().HaveCount(1);
        dispatcher.Calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task DomainAuthorization_blocks_unauthorized_outbound()
    {
        var auth = new InMemoryDomainAuthorizationService();
        await auth.AuthorizeAsync(new AuthorizedDomainDto("trusted.example.com", null, IsAuthorized: true));

        (await auth.IsAuthorizedAsync("https://trusted.example.com/path")).Should().BeTrue();
        (await auth.IsAuthorizedAsync("https://malicious.bad/path")).Should().BeFalse();
    }

    [Fact]
    public async Task EventSubscriptionService_round_trip()
    {
        var svc = new InMemoryEventSubscriptionService();
        var configId = Guid.NewGuid();
        await svc.SubscribeAsync(configId, "order.created");
        (await svc.ListAsync(configId)).Should().HaveCount(1);

        await svc.UnsubscribeAsync(configId, "order.created");
        (await svc.ListAsync(configId)).Should().BeEmpty();
    }

    // ───── EDA Filters & error handlers ─────

    [Fact]
    public void EventTypeFilter_supports_wildcard_prefix()
    {
        var filter = new EventTypeFilter("order.*");
        filter.Accepts(EventEnvelope.ForPublishing("d", "order.created", new { })).Should().BeTrue();
        filter.Accepts(EventEnvelope.ForPublishing("d", "payment.captured", new { })).Should().BeFalse();
    }

    [Fact]
    public void HeaderFilter_matches_exact_value_only()
    {
        var filter = new HeaderEventFilter("tenant", "alpha");
        var env = EventEnvelope.ForPublishing("d", "T", new { })
            .WithHeaders(new Dictionary<string, string> { ["tenant"] = "alpha" });
        filter.Accepts(env).Should().BeTrue();

        var other = EventEnvelope.ForPublishing("d", "T", new { })
            .WithHeaders(new Dictionary<string, string> { ["tenant"] = "beta" });
        filter.Accepts(other).Should().BeFalse();
    }

    [Fact]
    public void Composite_requires_all_children_to_accept()
    {
        var c = new CompositeEventFilter(new IEventFilter[]
        {
            new EventTypeFilter("order.*"),
            new DestinationEventFilter(new[] { "topic.a" }),
        });
        c.Accepts(EventEnvelope.ForPublishing("topic.a", "order.created", new { })).Should().BeTrue();
        c.Accepts(EventEnvelope.ForPublishing("topic.b", "order.created", new { })).Should().BeFalse();
    }

    [Fact]
    public async Task DefaultErrorHandler_retries_until_max_then_dlq()
    {
        var handler = new DefaultErrorHandler(NullLogger<DefaultErrorHandler>.Instance) { MaxRetries = 2 };
        var env = EventEnvelope.ForPublishing("d", "T", new { });
        (await handler.HandleAsync(env, new Exception("x"), attempt: 1)).Should().Be(ErrorHandlingStrategy.Retry);
        (await handler.HandleAsync(env, new Exception("x"), attempt: 2)).Should().Be(ErrorHandlingStrategy.DeadLetter);
    }

    // ───── Orchestration: DLQ + Compensation ─────

    [Fact]
    public async Task OrchestrationDeadLetterStore_captures_state_and_lists()
    {
        var dlq = new InMemoryDeadLetterStore(NullLogger<InMemoryDeadLetterStore>.Instance);
        var ctx = new OrchestrationExecutionContext { CorrelationId = "c-1", Pattern = ExecutionPattern.Saga };
        await dlq.PublishAsync(ctx, new InvalidOperationException("step failed"));

        var entries = await dlq.ListAsync();
        entries.Should().HaveCount(1);
        entries[0].CorrelationId.Should().Be("c-1");
        entries[0].Reason.Should().Be("step failed");
    }

    [Fact]
    public void CompensationPolicy_static_presets_have_expected_actions()
    {
        CompensationPolicy.Default.FailureAction.Should().Be(CompensationFailureAction.Abort);
        CompensationPolicy.SkipOnFailure.FailureAction.Should().Be(CompensationFailureAction.Skip);
        CompensationPolicy.RetryThenDeadLetter.FailureAction.Should().Be(CompensationFailureAction.Retry);
        CompensationPolicy.RetryThenDeadLetter.ContinueOnFailure.Should().BeTrue();
    }

    // ───── CQRS: cache invalidator ─────

    [InvalidateCacheOn(typeof(SampleEvent), Pattern = "users")]
    private sealed class SampleQueryHandler { }
    private sealed record SampleEvent(string Id);

    private sealed class StubQueryBus : IQueryBus
    {
        public List<string?> ClearedPatterns { get; } = new();
        public Task<TResult> AskAsync<TResult>(FireflyFramework.Cqrs.Queries.IQuery<TResult> query, FireflyFramework.Cqrs.Context.ExecutionContext context, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task ClearCacheAsync(string? pattern = null, CancellationToken ct = default)
        {
            ClearedPatterns.Add(pattern);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task EventDrivenCacheInvalidator_clears_when_registered_event_arrives()
    {
        var bus = new StubQueryBus();
        var inv = new EventDrivenCacheInvalidator(bus, NullLogger<EventDrivenCacheInvalidator>.Instance);
        inv.RegisterFromAssemblies(new[] { typeof(AuditExtensionsTests).Assembly });

        await inv.OnEventAsync(new SampleEvent("1"));

        bus.ClearedPatterns.Should().Contain("users");
    }
}
