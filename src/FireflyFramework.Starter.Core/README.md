# FireflyFramework.Starter.Core

One-call wiring of the Firefly infrastructure tier. Equivalent to
importing `fireflyframework-starter-core` on the Java side.

## Usage

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

## What it registers

`AddFireflyCore` calls:

- `AddFireflyWeb`           — RFC 7807 problem-details middleware,
                              idempotency, correlation, PII masking
- `AddFireflyObservability` — OpenTelemetry traces / metrics, Serilog
- `AddFireflyCache`         — `ICacheAdapter` (Memory or Redis based on configuration)
- `AddFireflyEda`           — `IEventPublisher` / `IEventConsumer`
- `AddFireflyCqrs(cqrsAssemblies)` — Command and query buses with
                              handler discovery

## Dependencies

| Reference                            | Pulled in transitively  |
|--------------------------------------|-------------------------|
| `FireflyFramework.Web`               | always                  |
| `FireflyFramework.Observability`     | always                  |
| `FireflyFramework.Cache`             | always                  |
| `FireflyFramework.Eda`               | always                  |
| `FireflyFramework.Cqrs`              | always                  |

## Java mapping

| .NET                                 | Java                              |
|--------------------------------------|-----------------------------------|
| `AddFireflyCore`                     | `fireflyframework-starter-core`   |
