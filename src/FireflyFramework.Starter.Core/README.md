# FireflyFramework.Starter.Core

## Overview

`FireflyFramework.Starter.Core` is the **infrastructure-tier meta-package** of the
Firefly Framework for .NET. A single call to `services.AddFireflyCore(...)` wires
the seven primitives every Firefly service relies on — web, observability, cache,
event-driven architecture (EDA), CQRS, the typed-client builder, and validators —
in the order required for them to compose correctly. This is the smallest entry
point a Firefly service can adopt, and every other starter (`Starter.Application`,
`Starter.Domain`, `Starter.Data`, `BackOffice`) builds on top of it.

The problem this module solves is **boilerplate elimination at the infrastructure
boundary**. Without it, each service would copy ten to fifteen `services.Add*`
calls into `Program.cs`, with the constant risk of forgetting one (no
correlation IDs in production), wiring them in the wrong order (idempotency
middleware before exception handling), or pulling in inconsistent versions
across a fleet of services. The starter centralises the recipe so a service
team can focus on their domain instead of plumbing.

The Java equivalent is the Maven module `org.fireflyframework:firefly-starter-core`,
which collects the same set of `firefly-*-spring-boot-starter` artifacts and
relies on Spring Boot's auto-configuration to wire them. The .NET version
keeps the same assembly composition but uses explicit DI extension methods
(`AddFireflyWeb`, `AddFireflyCache`, etc.) instead of auto-configuration so
the registration order is unambiguous and traceable.

A separate module rather than direct package references keeps the consumer
surface tiny: a service references one project, not seven, and version
upgrades happen in one place.

## When to use this module

Reach for `Starter.Core` when:

- You are building a stateless or read-mostly microservice that does not
  need event sourcing or persistent storage. A service that consumes events
  from Kafka, runs CQRS handlers in memory, and emits HTTP responses fits
  here perfectly.
- You want full control over which IDP / orchestration / event-sourcing
  adapter the service picks. `Starter.Core` deliberately stops short of
  these choices because each service tends to pick exactly one.
- You are writing an internal worker or scheduled job that still wants the
  Firefly observability and CQRS stack but no inbound HTTP. The web
  middleware is registered but unused if you do not call `app.UseFireflyWeb()`.

Prefer a higher-tier starter when:

- The service exposes business plugins → `Starter.Application`.
- The service hosts event-sourced aggregates → `Starter.Domain`.
- The service is data-heavy and you want an EF Core `DbContext` wired in →
  `Starter.Data` plus your own `AddDbContext<TDb>` call.
- The service is a back-office / admin portal that needs a per-request
  user-impersonation context → `FireflyFramework.BackOffice`.

## Mental model

`Starter.Core` is a **composer**. It does not introduce new abstractions of
its own; everything in it lives in the seven referenced projects:

| Composed module                    | What it contributes                                               |
|------------------------------------|-------------------------------------------------------------------|
| `FireflyFramework.Web`             | RFC 7807 problem-details, idempotency, PII masking, CORS, errors  |
| `FireflyFramework.Observability`   | OpenTelemetry traces / metrics, OTLP exporter, runtime metrics    |
| `FireflyFramework.Cache`           | `ICacheAdapter` (Memory, Redis, NoOp) + `FireflyCacheManager`     |
| `FireflyFramework.Eda`             | `IEventPublisher` / `IEventConsumer` (in-memory, Kafka, RabbitMQ) |
| `FireflyFramework.Cqrs`            | `ICommandBus` / `IQueryBus` with attribute-driven handler scan    |
| `FireflyFramework.Client`          | Typed-client builder for resilient HTTP / gRPC / GraphQL          |
| `FireflyFramework.Validators`      | Attribute-based DTO validators                                    |

What is intentionally **not** here:

- No IDP adapter — pick one in your service composition root (Keycloak,
  Azure AD, AWS Cognito, internal DB).
- No event store / event sourcing — those move you to `Starter.Domain`.
- No DbContext — `Starter.Data` is the right place when persistence is the
  point of the service.
- No plugin manager — that lives in `Starter.Application`.

The starter prints an embedded `Resources/banner.txt` to `stdout` exactly once
per process. The print latch (`FireflyBanner._printed`) is interlocked, so
calling `AddFireflyCore` twice in the same process — or transitively from a
higher-tier starter — produces only one banner. This mirrors Spring Boot's
single-banner behaviour and gives a recognisable visual anchor for log
streams in Kubernetes pods, ECS tasks, and local dev terminals.

## Quick start

```csharp
using FireflyFramework.Starter.Core;
using FireflyFramework.Web.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFireflyCore(
    builder.Configuration,
    serviceName:    "orders-service",
    serviceVersion: "1.0.0",
    cqrsAssemblies: new[] { typeof(Program).Assembly });

var app = builder.Build();
app.UseFireflyWeb();
app.MapControllers();
await app.RunAsync();
```

That is it. The service now has structured logs, traces, metrics, idempotent
POSTs, RFC 7807 error responses, an in-memory cache, an in-memory event bus,
and CQRS bus instances ready to dispatch any `ICommandHandler<,>` /
`IQueryHandler<,>` discovered in the supplied assembly.

## Public surface

The module exposes a single static class with one extension method.

```csharp
namespace FireflyFramework.Starter.Core;

public static class FireflyCoreExtensions
{
    public static IServiceCollection AddFireflyCore(
        this IServiceCollection services,
        IConfiguration config,
        string serviceName,
        string serviceVersion = "1.0.0",
        params System.Reflection.Assembly[] cqrsAssemblies);
}
```

| Parameter         | Required | Purpose                                                                                          |
|-------------------|----------|--------------------------------------------------------------------------------------------------|
| `services`        | yes      | The DI container being configured. The standard ASP.NET Core `IServiceCollection`.               |
| `config`          | yes      | The `IConfiguration` from which every `Firefly:*` section is bound.                              |
| `serviceName`     | yes      | The OpenTelemetry `service.name` resource attribute. Used by the observability stack and banner. |
| `serviceVersion`  | no       | The OpenTelemetry `service.version` attribute. Defaults to `"1.0.0"`.                            |
| `cqrsAssemblies`  | no       | One or more assemblies scanned for `ICommandHandler<,>` / `IQueryHandler<,>` implementations.    |

If `cqrsAssemblies` is empty, the buses are still registered but no handlers
are discovered — useful when the service only publishes events or hosts a
background worker.

## Configuration

Every `Firefly:*` section transitively bound by the starter:

```jsonc
{
  "Firefly": {
    "Web": {
      "ErrorHandling": {
        "IncludeStackTrace": false,
        "IncludeDebugInfo":  false,
        "ProblemTypeBaseUri": "https://errors.fireflyframework.org/",
        "MaskPii":           true
      },
      "Idempotency": {
        "Enabled":      true,
        "HeaderName":   "X-Idempotency-Key",
        "Ttl":          "01:00:00",
        "MaxKeyLength": 256,
        "Methods":      [ "POST", "PATCH", "PUT", "DELETE" ]
      },
      "PiiMasking": { "Enabled": true },
      "Cors":       { /* see FireflyCorsOptions */ }
    },
    "Cache": {
      "Provider":  "Memory",          // Memory | Redis | NoOp | Auto
      "Name":      "default",
      "KeyPrefix": "firefly:cache:",
      "Redis":     { "ConnectionString": "localhost:6379" },
      "Memory":    { "SizeLimit": null }
    },
    "Eda": {
      "DefaultPublisher": "InMemory", // InMemory | Kafka | RabbitMq | Auto
      "DefaultConsumer":  "InMemory",
      "Kafka":            { "BootstrapServers": "localhost:9092" },
      "RabbitMq":         { "Hostname": "localhost", "Port": 5672 }
    },
    "Observability": {
      "Metrics": { "Enabled": true,  "Exporter": "Prometheus" },
      "Tracing": { "Enabled": true,  "SamplingProbability": 0.1 }
    }
  }
}
```

The defaults are deliberately safe for local development — in-memory
everywhere. To run against real infrastructure, switch `Firefly:Cache:Provider`
to `Redis`, `Firefly:Eda:DefaultPublisher` to `Kafka`, and adjust the
provider-specific blocks.

## Common patterns

### 1. Minimal API service with idempotent writes

```csharp
builder.Services.AddFireflyCore(builder.Configuration, "orders-service", "1.0.0",
    new[] { typeof(PlaceOrderCommand).Assembly });

var app = builder.Build();
app.UseFireflyWeb();   // mounts exception + idempotency middleware
app.MapPost("/api/v1/orders", async (PlaceOrderRequest req, ICommandBus bus, CancellationToken ct) =>
{
    var ctx = new ExecutionContext { UserId = "demo-user" };
    var id  = await bus.SendAsync(new PlaceOrderCommand(req.Sku, req.Quantity, req.UnitPrice), ctx, ct);
    return Results.Created($"/api/v1/orders/{id}", new { id });
});
```

### 2. Worker service without HTTP

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddFireflyCore(builder.Configuration, "orders-projector", "1.0.0",
    new[] { typeof(OrderProjector).Assembly });
builder.Services.AddHostedService<OrderProjector>();
await builder.Build().RunAsync();
```

### 3. Override the default cache provider

```jsonc
{
  "Firefly": {
    "Cache": { "Provider": "Redis", "Redis": { "ConnectionString": "redis-prod:6379" } }
  }
}
```

The `ICacheAdapter` resolved from DI now points at `RedisCacheAdapter` —
no code change required.

### 4. Switch the event publisher to Kafka

```jsonc
{
  "Firefly": { "Eda": { "DefaultPublisher": "Kafka", "Kafka": { "BootstrapServers": "kafka-1:9092,kafka-2:9092" } } }
}
```

`IEventPublisher` is now `KafkaEventPublisher`; `InMemoryEventPublisher`
remains registered as a concrete class so unit tests can resolve it directly.

### 5. Multi-assembly CQRS scan

When commands and handlers live across several projects:

```csharp
builder.Services.AddFireflyCore(
    builder.Configuration, "orders", "1.0.0",
    new[]
    {
        typeof(PlaceOrderCommand).Assembly,    // Core
        typeof(OrderProjection).Assembly,      // Projections
    });
```

The CQRS scanner walks each assembly looking for non-abstract classes that
implement `ICommandHandler<,>` or `IQueryHandler<,>` and registers them
against their closed generic interfaces.

## Pitfalls and gotchas

- **Order matters.** `AddFireflyCore` must run before any service-specific
  registration that depends on an `IOptions<Firefly*Options>` snapshot at
  construction time. Calling it last produces "options not bound" errors
  at first request.
- **`UseFireflyWeb` must be in the pipeline.** Calling `AddFireflyCore`
  registers the middleware types but does not mount them; for HTTP services
  you must add `app.UseFireflyWeb()` after `app.UseRouting()`. A worker
  service can skip this.
- **The banner only prints once per process.** Tests that spin up multiple
  hosts will see a banner from the first host and silence from the rest.
  `FireflyBanner.ResetForTests()` is an internal hook used by
  `FireflyBannerTests`; it is not part of the public API.
- **`Firefly:*` sections are case-insensitive but section names are not.**
  Spelling `Firefly:cache:Provider` works; spelling
  `Firefly:Caching:Provider` silently falls through to defaults.
- **CQRS handlers are scanned, not auto-discovered.** A handler in an
  assembly you forgot to pass to `cqrsAssemblies` simply will not be
  registered. The bus will throw `InvalidOperationException` on dispatch.
- **`AddDistributedMemoryCache` is registered by `AddFireflyWeb`** so
  idempotency works without a Redis dependency. Replace with
  `AddStackExchangeRedisCache` in production for cross-instance idempotency.
- **Calling `AddFireflyCore` twice is safe but not idempotent for
  singletons added via `AddSingleton<T>(implementation)`.** The framework
  uses `TryAdd*` for canonical types, but a few defaults are added with
  `AddSingleton` (e.g. exception converters), so the second call appends
  duplicates. In normal application code, call it exactly once.

## Internals (for the curious)

The body of `AddFireflyCore` is six lines, in this strict order:

1. **`FireflyBanner.Print(...)`** — Resolves the embedded `Resources/banner.txt`
   on the starter assembly via `assembly.GetManifestResourceNames()`,
   substitutes `${application.name}`, `${application.version}`,
   `${dotnet.version}`, `${AnsiColor.*}`, `${AnsiStyle.*}` placeholders,
   and writes once to `Console.Out`. The latch
   (`Interlocked.Exchange(ref _printed, 1)`) guarantees a single emission
   per process. ANSI emission honours `NO_COLOR`, `TERM=dumb`, and
   `Console.IsOutputRedirected`.

2. **`AddFireflyWeb(config)`** — Binds four options sections
   (`Firefly:Web:ErrorHandling`, `:Idempotency`, `:PiiMasking`, `:Cors`),
   registers the eight default `IExceptionConverter`s, the
   `PiiMaskingService`, the `ExceptionConverterRegistry`, and adds
   `AddDistributedMemoryCache()` so idempotency has a backing store.

3. **`AddFireflyObservability(config, serviceName, serviceVersion)`** —
   Configures OpenTelemetry: meters under `firefly.*`, traces under
   `firefly.*`, OTLP exporter, runtime instrumentation, and a service
   resource of `(serviceName, serviceVersion)` annotated with
   `framework=fireflyframework-dotnet`.

4. **`AddFireflyCache(config)`** — Binds `Firefly:Cache`, registers
   `JsonCacheSerializer` and an `IMemoryCache` (defaults), and resolves
   `ICacheAdapter` lazily via the configured `CacheType` (Auto, Memory,
   Redis, NoOp). For Redis, it opens a single `ConnectionMultiplexer` per
   process.

5. **`AddFireflyEda(config)`** — Binds `Firefly:Eda`, registers an
   `InMemoryEventBus`, and resolves `IEventPublisher` based on
   `DefaultPublisher`. The Kafka publisher is registered as a concrete class
   even when in-memory is the default, so it can be resolved on demand.

6. **`AddFireflyCqrs(cqrsAssemblies)`** — Registers `DefaultCommandBus` and
   `DefaultQueryBus` as singletons, then walks each assembly's exported
   types looking for `ICommandHandler<,>` / `IQueryHandler<,>` closures
   and registers each as `AddTransient(closedInterface, type)`.

The `Client` and `Validators` modules are pulled in transitively via
project reference but expose no `AddFirefly*` extension — they are pure
type libraries.

### What if I call AddFirefly* more than once?

Most registrations use `TryAddSingleton` and are idempotent. A few
(`AddSingleton<IExceptionConverter, ...>`) are append-only by design so
applications can stack their own converters; calling the starter twice
produces duplicate converters and a confused exception pipeline. The
framework rule is: call any single `AddFirefly*` extension exactly once
per `IServiceCollection`.

## Dependencies

| Reference                                   | Why                                                                  |
|---------------------------------------------|----------------------------------------------------------------------|
| `FireflyFramework.Web`                      | HTTP middleware (errors, idempotency, CORS, PII masking)             |
| `FireflyFramework.Observability`            | OpenTelemetry traces, metrics, runtime instrumentation               |
| `FireflyFramework.Cache`                    | Memory / Redis / NoOp cache adapters                                 |
| `FireflyFramework.Eda`                      | In-memory / Kafka / RabbitMQ publishers and consumers                |
| `FireflyFramework.Cqrs`                     | Command and query bus                                                |
| `FireflyFramework.Client`                   | Typed-client builder used by the SDK projects                        |
| `FireflyFramework.Validators`               | Attribute-based validators                                           |
| `Microsoft.Extensions.ServiceDiscovery`     | Logical service-name resolution                                      |
| `Polly`, `Polly.RateLimiting`               | Resilience primitives surfaced via `FireflyFramework.Client`         |

The package embeds `Resources/banner.txt` so `FireflyBanner.Print` can
find a manifest resource ending in `.banner.txt` regardless of the
starter that called it.

## Java mapping

| .NET                                  | Java                                                  |
|---------------------------------------|-------------------------------------------------------|
| `AddFireflyCore`                      | `org.fireflyframework:firefly-starter-core`           |
| `Resources/banner.txt`                | `src/main/resources/banner.txt` (Spring Boot)         |
| `FireflyBanner.Print`                 | Spring Boot's automatic banner printer                |
| Explicit `Add*` calls                 | Spring Boot auto-configuration via `META-INF/spring`  |
