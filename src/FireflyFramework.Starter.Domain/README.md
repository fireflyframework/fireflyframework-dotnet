# FireflyFramework.Starter.Domain

Domain-tier starter for event-sourced services. Adds an in-memory
`IEventStore` on top of `Starter.Core`.

Mirrors `org.fireflyframework:firefly-starter-domain`.

## Usage

```csharp
using FireflyFramework.Starter.Domain;

builder.Services.AddFireflyDomain(
    builder.Configuration,
    serviceName:    "orders-service",
    serviceVersion: "1.0.0",
    cqrsAssemblies: new[] { typeof(Program).Assembly });
```

## What it adds on top of `AddFireflyCore`

- `IEventStore` — singleton, default `InMemoryEventStore`

Replace with the EF Core implementation for persistent storage:

```csharp
builder.Services.AddDbContextFactory<EventStoreDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration["Firefly:Data:ConnectionString"]));
builder.Services.AddSingleton<IEventStore>(sp => new EfCoreEventStore(
    sp.GetRequiredService<IDbContextFactory<EventStoreDbContext>>(),
    knownEventTypes: new[] { typeof(OrderPlaced) }));
```

## Dependencies

| Reference                                | Pulled in transitively  |
|------------------------------------------|-------------------------|
| `FireflyFramework.Starter.Core`          | always                  |
| `FireflyFramework.EventSourcing`         | always                  |

## Java mapping

| .NET                       | Java                                     |
|----------------------------|------------------------------------------|
| `AddFireflyDomain`         | `fireflyframework-starter-domain`        |
