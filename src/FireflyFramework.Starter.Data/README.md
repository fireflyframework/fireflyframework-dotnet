# FireflyFramework.Starter.Data

## Overview

`FireflyFramework.Starter.Data` is the **data-tier starter** for ETL,
batch, and worker services that own their own EF Core `DbContext`. It
adds Polly resilience helpers on top of `Starter.Core` and pulls in
`FireflyFramework.Data` for filter / pagination / repository
contracts.

The application is expected to register its own DbContext —
`Starter.Data` doesn't impose a schema, a provider, or migrations.

Mirrors `org.fireflyframework:firefly-starter-data`.

## Why a separate starter?

The starter family covers four use cases:

| Starter                         | Composition                                              |
|---------------------------------|----------------------------------------------------------|
| `Starter.Core`                  | Web + Cache + Observability + EDA + CQRS + Client + Validators |
| `Starter.Application`           | `Starter.Core` + Plugins + IDP + orchestration + rule engine        |
| `Starter.Domain`                | `Starter.Core` + Event Sourcing                          |
| `Starter.Data`                  | `Starter.Core` + Polly resilience + Data primitives      |

`Starter.Data` is for services where the dominant concern is
relational persistence with bounded retry semantics — the canonical
fit is an ETL job that reads from one database, transforms, and
writes to another. It deliberately omits orchestration, event
sourcing, plugins, and IDP because those don't apply to most
data-pipeline services.

## What it adds beyond Starter.Core

| Component                                | What it gives you                                       |
|------------------------------------------|---------------------------------------------------------|
| `FireflyFramework.Data` types             | `BaseEntity<TId>`, `IRepository<,>`, `FilterRequest<T>`, `PaginationRequest`/`Response`, `GenericFilter<,,>` |
| Polly v8 (`Polly`, `Polly.RateLimiting`) | Retry, circuit-breaker, bulkhead, rate-limit pipelines  |

## Mental model

```
       ┌────────────────────────────────────────┐
       │        AddFireflyData(...)             │
       └────────────────┬───────────────────────┘
                        │  composes
                        ▼
       ┌────────────────────────────────────────┐
       │  Starter.Core wiring                   │
       │   - RFC 7807, idempotency, correlation │
       │   - cache, observability, EDA, CQRS    │
       │   - client + validators                │
       └────────────┬───────────────────────────┘
                    │
                    │  +
                    ▼
       ┌────────────────────────────────────────┐
       │  FireflyFramework.Data                 │
       │   - BaseEntity / IRepository / DSL     │
       └────────────────────────────────────────┘
                    │
                    │  +
                    ▼
       ┌────────────────────────────────────────┐
       │  Polly v8                              │
       │   - retry, breaker, bulkhead, rate-lim │
       └────────────────────────────────────────┘
```

The application registers its own `DbContext` separately —
`Starter.Data` does not call `AddDbContext` for you.

## Quick start

```csharp
using FireflyFramework.Starter.Data;
using Microsoft.EntityFrameworkCore;

builder.Services.AddFireflyData(
    builder.Configuration,
    serviceName:    "etl-service",
    serviceVersion: "1.0.0",
    cqrsAssemblies: new[] { typeof(Program).Assembly });

builder.Services.AddDbContext<MyDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration["Firefly:Data:ConnectionString"]));

var app = builder.Build();
app.UseFireflyWeb();   // RFC 7807 + idempotency + correlation
app.MapControllers();
await app.RunAsync();
```

## Common patterns

### Wiring a Polly retry pipeline

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 5,
        Delay            = TimeSpan.FromSeconds(2),
        BackoffType      = DelayBackoffType.Exponential,
    })
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        FailureRatio    = 0.5,
        MinimumThroughput = 10,
        BreakDuration   = TimeSpan.FromSeconds(30),
    })
    .Build();

await pipeline.ExecuteAsync(async ct => await UploadAsync(record, ct), ct);
```

### Bulk update with EF Core 10

```csharp
var rowsUpdated = await db.Records
    .Where(r => r.Status == RecordStatus.Pending && r.CreatedAt < cutoff)
    .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RecordStatus.Stale), ct);
```

EF Core 10's `ExecuteUpdateAsync` translates to a single SQL update —
much faster than load-mutate-save for bulk batch jobs. Note that it
bypasses change tracking and interceptors.

### Pagination loop with cursor

```csharp
DateTimeOffset? cursor = null;
while (true)
{
    var page = await repo.FindAllAsync(new FilterRequest<Record>
    {
        RangeFilters = new RangeFilter
        {
            Ranges = new() { ["CreatedAt"] = new(From: cursor, To: null) },
        },
        Pagination = new PaginationRequest { PageNumber = 0, PageSize = 1000, SortBy = "CreatedAt" },
    }, ct);

    if (page.Content.Count == 0) break;
    foreach (var rec in page.Content) await ProcessAsync(rec, ct);
    cursor = page.Content[^1].CreatedAt.AddTicks(1);
}
```

For high-volume tables, cursor pagination is dramatically faster
than offset pagination past a few thousand rows.

## Pitfalls and gotchas

- **No `AddDbContext` is implied.** You must register your own.
  This is by design — different services need different providers
  and lifetimes.
- **Polly v8 is the supported version.** Older Polly v7 APIs
  (`Policy.Handle<...>().Retry(...)`) are deprecated. Use
  `ResiliencePipelineBuilder` exclusively.
- **`ExecuteUpdateAsync` and `ExecuteDeleteAsync` skip interceptors.**
  Audit fields, soft-delete filters, and domain events triggered by
  `SaveChangesAsync` won't fire. If you depend on them, use the
  entity path.
- **`Starter.Data` does not include orchestration.** Long-running
  workflows belong in `Starter.Application`. If your ETL needs saga
  semantics, use that starter instead.

## Internals (for the curious)

- `AddFireflyData` calls `AddFireflyCore` internally and then
  layers `Polly` registrations. The CQRS bus is wired so command /
  query handlers in your service are auto-discovered.
- The starter does not bind any configuration sections of its own —
  it relies on the sections owned by the underlying modules
  (`Firefly:Cache`, `Firefly:Eda`, `Firefly:Observability`, etc.).

## Dependencies

| Reference                                | Pulled in transitively  |
|------------------------------------------|-------------------------|
| `FireflyFramework.Starter.Core`          | always                  |
| `FireflyFramework.Data`                  | always                  |
| `Polly` / `Polly.RateLimiting`           | always                  |

## Java mapping

| .NET                  | Java                                     |
|-----------------------|------------------------------------------|
| `AddFireflyData`      | `fireflyframework-starter-data`          |
