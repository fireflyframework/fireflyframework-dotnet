# FireflyFramework.Callbacks.Core

## Overview

`FireflyFramework.Callbacks.Core` is the **runtime** of the outbound-callback
subsystem. It is the assembly that knows how to take a domain event, look up
every callback subscribed to it, sign each request with HMAC-SHA256, push it
through a Polly resilience pipeline (exponential backoff with jitter +
timeout), record the result in an audit log, and authorize the destination
URL against an allow-list. None of that machinery lives in `Interfaces` (which
is pure DTOs) or `Models` (which is pure persistence shapes); it all lives
here.

The mental model is "a stateless dispatch pipeline composed from four
SPI-style ports". Every concrete piece — `CallbackDispatcher`, `CallbackRouter`,
plus the four interfaces with `InMemory*` defaults — is replaceable. You
provide a real `ICallbackConfigurationStore` backed by EF Core; the framework
keeps using its in-memory default for tests. You inject your own dispatcher
that talks to RabbitMQ; the router doesn't notice. The compiler enforces the
boundaries (each csproj only references the tier below it), and DI ties the
pieces together at runtime.

This module mirrors `org.fireflyframework:firefly-callbacks-core` from the
Java line. The class names, method shapes, and sequence of operations track
the Java implementations one-for-one. Where Java uses Resilience4j, .NET uses
Polly v8 (`ResiliencePipelineBuilder`); where Java uses
`PlatformTransactionManager`, .NET leaves transaction management to EF Core.
Otherwise the framework, the SPI surface, and the on-the-wire behaviour are
identical.

## When to use this module

Reference `FireflyFramework.Callbacks.Core` from:

- The hosting project that exposes the callback admin REST controllers (it
  registers `ICallbackConfigurationStore` and friends in DI; the controllers
  in `Callbacks.Web` call into them).
- A background service or saga that wants to call `ICallbackRouter.RouteAsync`
  whenever a domain event fires.
- Tests that need to assemble the dispatcher manually, e.g. with a
  fake `HttpClient` and a stub configuration store.

You should *not* reference this module from any project that is supposed to
be a thin REST consumer of the callback service — those projects only need
`Callbacks.Sdk` (which only references `Callbacks.Interfaces`).

## Mental model

The runtime is a pipeline of three interfaces with one orchestrator on top:

```
                  ┌──────────────────────────────────┐
                  │ ICallbackRouter                  │
                  │   └─ RouteAsync(eventType, …)    │
                  └────────────┬─────────────────────┘
                               │
            ┌──────────────────┼─────────────────────────┐
            ▼                  ▼                         ▼
┌────────────────────┐ ┌──────────────────────┐ ┌────────────────────┐
│ ICallback-         │ │ IDomainAuthorization │ │ ICallback-         │
│   ConfigurationStore│ │   Service           │ │   ExecutionStore   │
│  (lookup by event) │ │ (URL allow-list)    │ │ (audit append)     │
└────────────────────┘ └──────────────────────┘ └────────────────────┘
            │
            ▼
┌────────────────────┐
│ ICallbackDispatcher│  ← Polly retry+timeout, HMAC signing
└────────────────────┘
```

For each subscribed configuration, the router asks `IDomainAuthorizationService`
whether the URL is permitted, calls `ICallbackDispatcher.DispatchAsync(...)`,
records the result via `ICallbackExecutionStore`, and accumulates the
`CallbackExecutionDto` into the response list.

`IEventSubscriptionService` is the supporting CRUD for the
`(configurationId, eventType)` join. It is independent of the routing
pipeline; the router goes via `ICallbackConfigurationStore.FindBySubscribedEventAsync`.

## Quick start

```csharp
using FireflyFramework.Callbacks.Core;
using FireflyFramework.Callbacks.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddLogging();

// 1. Plug in the store. Replace the in-memory default with EF Core in production.
services.AddSingleton<ICallbackConfigurationStore, InMemoryCallbackConfigurationStore>();
services.AddSingleton<ICallbackExecutionStore,    InMemoryCallbackExecutionStore>();
services.AddSingleton<IEventSubscriptionService,  InMemoryEventSubscriptionService>();
services.AddSingleton<IDomainAuthorizationService,InMemoryDomainAuthorizationService>();

// 2. Wire HttpClient + dispatcher.
services.AddHttpClient<ICallbackDispatcher, CallbackDispatcher>();

// 3. Router on top.
services.AddSingleton<ICallbackRouter, CallbackRouter>();

using var sp = services.BuildServiceProvider();

// Use it.
var store  = sp.GetRequiredService<ICallbackConfigurationStore>();
var router = sp.GetRequiredService<ICallbackRouter>();

await store.CreateAsync(new CallbackConfigurationDto(/* … */));
var executions = await router.RouteAsync(
    eventType: "order.created",
    payload:   "{\"orderId\":42}",
    tenantId:  "alpha");
```

## Public surface

### `ICallbackDispatcher` / `CallbackDispatcher`

The terminal stage. Its single responsibility is "post one payload to one URL
with the configured retry + signing semantics, return an audit row".

```csharp
public interface ICallbackDispatcher
{
    Task<CallbackExecutionDto> DispatchAsync(
        CallbackConfigurationEntity config,
        string                       eventType,
        string                       payload,
        CancellationToken            ct = default);
}
```

What it does, in order:

1. **Build a Polly v8 resilience pipeline** (`ResiliencePipelineBuilder<HttpResponseMessage>`):
   - `AddRetry` with `MaxRetryAttempts = config.MaxRetries`, exponential backoff,
     jitter, initial delay `config.RetryDelayMs` ms.
   - `AddTimeout(config.TimeoutMs)`.
2. **Compose the `HttpRequestMessage`** with method `MapMethod(config.HttpMethod)`,
   body `application/json`, and the configured custom headers.
3. **Sign**: when `config.SignatureEnabled && config.Secret != null`, compute
   `HMAC-SHA256(payload, secret)`, hex-encode it, and add the
   `config.SignatureHeader ?? "X-Signature"` header.
4. **Send**: drive the request through the pipeline, time it with a `Stopwatch`,
   and synthesise a `CallbackExecutionDto`.
5. **Recover**: any thrown exception (timeout, DNS failure, retries exhausted)
   becomes a `CallbackExecutionDto` with `Status = FailedPermanent` and the
   exception message in `ErrorMessage`.

The `CallbackDispatcher` constructor takes an injected `HttpClient` (so the
host owns the lifetime via `IHttpClientFactory`) and an `ILogger`.

### `ICallbackConfigurationStore`

Persistence-agnostic CRUD over `CallbackConfigurationDto`. Default implementation
`InMemoryCallbackConfigurationStore` uses a `ConcurrentDictionary<Guid, ...>`.

```csharp
public interface ICallbackConfigurationStore
{
    Task<IReadOnlyList<CallbackConfigurationDto>> ListAsync(string? tenantId = null, CancellationToken ct = default);
    Task<CallbackConfigurationDto?>               GetAsync (Guid id, CancellationToken ct = default);
    Task<CallbackConfigurationDto>                CreateAsync(CallbackConfigurationDto dto, CancellationToken ct = default);
    Task<CallbackConfigurationDto?>               UpdateAsync(Guid id, CallbackConfigurationDto dto, CancellationToken ct = default);
    Task<bool>                                    DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CallbackConfigurationDto>> FindBySubscribedEventAsync(string eventType, string? tenantId = null, CancellationToken ct = default);
}
```

`FindBySubscribedEventAsync` is the only non-trivial method — its semantics
are "all active configurations whose `SubscribedEventTypes` contains the
event type, filtered by tenant if provided". Real implementations should
maintain an index on the join table (`EventSubscriptionEntity`) for this
query.

### `ICallbackRouter` / `CallbackRouter`

Orchestrates a single fan-out:

```csharp
public interface ICallbackRouter
{
    Task<IReadOnlyList<CallbackExecutionDto>> RouteAsync(
        string             eventType,
        string             payload,
        string?            tenantId = null,
        CancellationToken  ct = default);
}
```

Algorithm:

1. Look up subscribers via `_store.FindBySubscribedEventAsync`.
2. Iterate in order; for each:
   - If `_domainAuth != null`, check `IsAuthorizedAsync(config.Url)`. Skip with
     a warning log when not authorized.
   - Map the DTO to a `CallbackConfigurationEntity` (via the private
     `ToEntity` helper) and call `_dispatcher.DispatchAsync(...)`.
   - Append the resulting `CallbackExecutionDto` to the response list.
   - If `_executions != null`, persist via `RecordAsync`.

The router is intentionally synchronous in its iteration (one subscriber at
a time). Parallelism, ordering guarantees, and back-pressure are
application concerns — wrap the call in your own pipeline if you need them.

### `ICallbackExecutionStore`

Append-only audit log. Five operations:

| Method                       | Purpose                                                  |
|------------------------------|----------------------------------------------------------|
| `RecordAsync`                | Append a single execution.                               |
| `ListByConfigurationAsync`   | Newest-first; default `limit = 100`.                     |
| `ListByStatusAsync`          | Filter by `Success`/`FailedRetrying`/`FailedPermanent`.  |
| `GetAsync`                   | Read one execution by id.                                |

`InMemoryCallbackExecutionStore` uses `ConcurrentBag<CallbackExecutionDto>`
internally. Production implementations should write to a partitioned table
keyed on `ExecutedAt` and apply a retention policy (Firefly typically
retains 30-90 days of audit rows).

### `IDomainAuthorizationService`

URL allow-list with sub-domain matching:

```csharp
public interface IDomainAuthorizationService
{
    Task<bool>                                IsAuthorizedAsync(string url, CancellationToken ct = default);
    Task                                      AuthorizeAsync   (AuthorizedDomainDto domain, CancellationToken ct = default);
    Task                                      RevokeAsync      (string domain, CancellationToken ct = default);
    Task<IReadOnlyList<AuthorizedDomainDto>>  ListAsync        (CancellationToken ct = default);
}
```

Default `InMemoryDomainAuthorizationService` semantics:

- **No domains configured** → `IsAuthorizedAsync` returns `true`. The
  service is "open by default" so green-field deployments still work.
- **At least one entry** → only URLs whose host *ends with* (case-insensitive)
  one of the authorized domains pass. So `partner.example.com` passes the
  filter when `example.com` is registered.
- `IsAuthorized = false` rows are stored but always reject.

This matches the Java behaviour exactly.

### `IEventSubscriptionService`

Manages the `(configurationId, eventType)` map separately from the
configuration document. Useful when subscriptions need to be added or
removed without rewriting the entire configuration row.

```csharp
public interface IEventSubscriptionService
{
    Task                                       SubscribeAsync       (Guid configurationId, string eventType, CancellationToken ct = default);
    Task                                       UnsubscribeAsync     (Guid configurationId, string eventType, CancellationToken ct = default);
    Task<IReadOnlyList<EventSubscriptionDto>>  ListAsync            (Guid configurationId, CancellationToken ct = default);
    Task<IReadOnlyList<EventSubscriptionDto>>  ListByEventTypeAsync (string eventType, CancellationToken ct = default);
}
```

## Configuration

This module exposes no `IOptions<T>`. Per-callback retry / timeout / signing
behaviour is controlled per-row through `CallbackConfigurationDto`, which is
the right granularity — different subscribers have different SLAs.

The only host-level configuration knob is the underlying `HttpClient` from
`IHttpClientFactory`; configure it through the standard ASP.NET Core
extensions:

```csharp
services.AddHttpClient<ICallbackDispatcher, CallbackDispatcher>(http =>
{
    http.DefaultRequestHeaders.Add("User-Agent", "firefly-callback-dispatcher/1.0");
});
```

## Common patterns

### Custom configuration store backed by EF Core

Replace the `InMemoryCallbackConfigurationStore` registration *before* the
controllers in `Callbacks.Web` are loaded:

```csharp
services.AddScoped<ICallbackConfigurationStore, EfCoreCallbackConfigurationStore>();
```

Implement the SPI by reading from `DbSet<CallbackConfigurationEntity>` and
mapping to `CallbackConfigurationDto` on the way out. Round-trip the JSON
columns (`SubscribedEventTypesJson`, `CustomHeadersJson`, `MetadataJson`)
with `System.Text.Json`.

### Adding a custom signature header

`CallbackDispatcher` reads the header name from `config.SignatureHeader`,
falling back to `"X-Signature"` when null. Set the column on the
configuration row to `"X-Hub-Signature-256"` (or whatever the consumer
expects) and the dispatcher will use it.

### Inhibiting retries for a "fire-and-forget" subscriber

Set `MaxRetries = 0` on the configuration. Polly's retry stage is still in
the pipeline but it does nothing.

### Pulling the audit log for a configuration

```csharp
var recent = await executionStore.ListByConfigurationAsync(configId, limit: 50);
foreach (var exec in recent)
{
    Console.WriteLine($"{exec.ExecutedAt:O} {exec.Status} {exec.RequestDurationMs}ms");
}
```

## Pitfalls and gotchas

- **HMAC payload encoding**: the dispatcher signs the *raw payload string*
  exactly as the consumer will see it on the wire. If you reformat the JSON
  on the way in, the signature will not validate downstream.
- **Polly v8, not v7**: `ResiliencePipelineBuilder` is the modern API;
  the v7 `IAsyncPolicy<T>` shape does not appear here. Don't mix them.
- **The router does not catch dispatcher exceptions.** The dispatcher itself
  catches and folds exceptions into a `CallbackExecutionDto`, but if you
  inject a non-default dispatcher that throws, the router will propagate.
- **`IDomainAuthorizationService.IsAuthorizedAsync` is open-by-default.**
  An empty allow-list returns `true`. If you want closed-by-default, register
  a custom implementation that returns `false` when no domains are configured.
- **`InMemoryCallbackConfigurationStore.FindBySubscribedEventAsync` filters
  on `c.Active`** — disabled configurations never receive events, even if
  their subscription list contains the type.

## Internals (for the curious)

The dispatcher uses Polly v8's `ResiliencePipelineBuilder<HttpResponseMessage>`
with strategy ordering `Retry → Timeout`. The order matters: each retry
attempt gets its own timeout window; if you put `Timeout` outside `Retry`
the entire retry sequence shares one timeout, which is almost never what
you want.

The HMAC implementation uses `HMACSHA256` from `System.Security.Cryptography`.
The output is hex-uppercase via `Convert.ToHexString`. Some downstream systems
expect lower-case; if you control both ends, this is fine, but if you don't,
you may need to wrap the dispatcher in a decorator that lower-cases the
signature.

The router maps DTO → entity inline rather than via an `IObjectMapper`.
That's fine because the mapping is only used to feed the dispatcher and
the lossy fields (`SubscribedEventTypesJson`, `CustomHeadersJson`) are
not needed at dispatch time. If you need the full mapper, write it in
your application code.

## Dependencies

| Reference                                | Used for                       |
|------------------------------------------|--------------------------------|
| `FireflyFramework.Callbacks.Models`      | Entity types for the dispatcher|
| `FireflyFramework.Callbacks.Interfaces`  | DTOs (transitive)              |
| `FireflyFramework.Eda`                   | Optional EDA-driven dispatch   |
| `Microsoft.Extensions.Http`              | `HttpClient` injection         |
| `Microsoft.Extensions.Logging.Abstractions` | Logging                     |
| `Polly`                                  | v8 resilience pipeline         |
| `Polly.Core`                             | v8 strategies (retry, timeout) |

## Java mapping

| .NET                                | Java                                  |
|-------------------------------------|---------------------------------------|
| `CallbackDispatcher`                | `CallbackDispatcherImpl`              |
| `ICallbackConfigurationStore`       | `CallbackConfigurationRepository`     |
| `CallbackRouter`                    | `CallbackRouterImpl`                  |
| `ICallbackExecutionStore`           | `CallbackExecutionRepository`         |
| `IDomainAuthorizationService`       | `DomainAuthorizationService`          |
| `IEventSubscriptionService`         | `EventSubscriptionServiceImpl`        |
| Polly v8 retry pipeline             | Resilience4j `RetryRegistry`          |
