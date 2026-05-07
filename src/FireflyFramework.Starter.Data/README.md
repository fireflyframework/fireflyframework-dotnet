# FireflyFramework.Starter.Data

Data-tier starter for ETL / batch services. Adds Polly resilience
helpers on top of `Starter.Core` and pulls in `FireflyFramework.Data`
for filter / pagination / repository contracts. The application is
expected to register its own `DbContext` with `AddDbContext<TDb>(...)`.

Mirrors `org.fireflyframework:firefly-starter-data`.

## Usage

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
```

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
