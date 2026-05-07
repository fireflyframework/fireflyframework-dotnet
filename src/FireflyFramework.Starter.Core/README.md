# FireflyFramework.Starter.Core

Meta-package and `AddFireflyCore(...)` extension that wires the Firefly *infrastructure tier* in one call. Mirrors `fireflyframework-starter-core`.

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddFireflyCore(
    builder.Configuration,
    serviceName: "orders-service",
    serviceVersion: "1.0.0",
    cqrsAssemblies: new[] { typeof(Program).Assembly });

var app = builder.Build();
app.UseFireflyWeb();
app.MapControllers();
app.Run();
```

`AddFireflyCore` registers everything from:

- `FireflyFramework.Web` (RFC 7807 errors + idempotency + PII masking)
- `FireflyFramework.Observability` (OpenTelemetry metrics + tracing)
- `FireflyFramework.Cache` (Memory + Redis adapters)
- `FireflyFramework.Eda` (publishers, consumers, serializers)
- `FireflyFramework.Cqrs` (command + query buses)

For higher tiers, use `FireflyFramework.Starter.Application`, `FireflyFramework.Starter.Domain`, `FireflyFramework.Starter.Data` or `FireflyFramework.BackOffice`.
