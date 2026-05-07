# Firefly Framework — .NET 9

A complete .NET 9 port of the Java/Spring Boot Firefly Framework
(`org.fireflyframework:*:26.04.01`). Same contracts, same starter pattern,
same calendar version — re-implemented with idiomatic .NET tooling.

The repository ships **52 NuGet-publishable projects** organised into four
tiers (foundational, platform, adapters, starters), backed by **157 xUnit
tests** covering every public surface. There are no stub implementations —
every method either runs real code or throws `NotSupportedException` with
an actionable message documenting why the underlying provider does not
support the operation.

## Why this exists

Most teams running the Java Firefly Framework eventually need to build
auxiliary services in .NET — a Windows-only integration, an Azure Function,
a desktop sidecar, a SaaS connector that uses a vendor's .NET SDK. Without
this port, those services either re-invent the framework's conventions
(error envelope, idempotency, correlation propagation, CQRS, EDA, event
sourcing, callbacks, webhooks, IDP, ECM) or ship inconsistent contracts
that break end-to-end traceability. With it, a .NET service is wired the
same way as its Java siblings and produces the same wire format.

## Requirements

- .NET 9 SDK (`9.0.100` or later). Verified against `9.0.115`.
- Apache-2.0 licence.

```bash
brew install dotnet@9          # macOS — or any official .NET 9 installer
source .envrc                  # exports DOTNET_ROOT and prepends dotnet@9 to PATH
dotnet --version               # expect 9.0.x
```

## Build, test, run

```bash
dotnet build  FireflyFramework.sln                                 # 0 errors
dotnet test   tests/FireflyFramework.Tests/                        # 157 passing
dotnet run --project samples/FireflyFramework.Samples.OrdersService/
```

## Repository layout

```
fireflyframework-dotnet/
├── docs/                              Long-form documentation
│   ├── ARCHITECTURE.md                Tier diagram, dependency graph
│   ├── MIGRATION-GUIDE.md             Java to .NET cookbook
│   ├── CONFIGURATION.md               Every Firefly:* options section
│   ├── MODULES.md                     One-line description per project
│   └── AUDIT.md                       Java vs .NET feature parity audit
├── src/                               52 framework projects
├── tests/FireflyFramework.Tests/      xUnit suite
├── samples/                           Runnable reference services
│   └── FireflyFramework.Samples.OrdersService/
├── Directory.Build.props              Parent build properties
├── Directory.Build.targets            Test-project package wiring
├── Directory.Packages.props           Central Package Management
├── FireflyFramework.sln               Solution file (53 projects, 7 folders)
├── NuGet.config                       Pins nuget.org as the only source
├── global.json                        Pins .NET SDK 9.0
├── .envrc                             Sources dotnet@9 into PATH
└── LICENSE                            Apache-2.0
```

## Module catalogue

### Foundational tier

| Project                          | Purpose                                                                                |
|----------------------------------|----------------------------------------------------------------------------------------|
| `FireflyFramework.Kernel`        | RFC 7807 `ProblemDetail`, `OperationResult<T>`, `IClock`, `FireflyException` base type |
| `FireflyFramework.Utils`         | `Try.Of`, `RetryUtils`, `TemplateRenderUtil` (Scriban + iText 7 PDF), `PdfOptions`     |
| `FireflyFramework.Validators`    | 16 validators (IBAN, BIC, Luhn, VAT, phone, password strength, etc.)                   |
| `FireflyFramework.Web`           | RFC 7807 middleware, idempotency, correlation, PII masking, 27 typed exceptions        |

### Platform tier

| Project                                                       | Purpose                                                                |
|---------------------------------------------------------------|------------------------------------------------------------------------|
| `FireflyFramework.Cache`                                      | `ICacheAdapter` port, Memory/Redis/Noop adapters, primary/fallback     |
| `FireflyFramework.Observability`                              | OpenTelemetry .NET (traces/metrics/logs) + Serilog                     |
| `FireflyFramework.Data`                                       | EF Core 9, filter DSL, pagination, soft-delete contract                |
| `FireflyFramework.Cqrs`                                       | Command/query buses, fluent dispatch, query cache, event-driven invalidation |
| `FireflyFramework.Eda`                                        | Kafka + RabbitMQ + InMemory, Schema Registry, filters, error handlers, resilient publisher |
| `FireflyFramework.EventSourcing`                              | Aggregates, snapshots, outbox, projections, upcasters                  |
| `FireflyFramework.Orchestration`                              | Saga, Workflow, TCC engines, dead-letter, compensation policies        |
| `FireflyFramework.RuleEngine.{Interfaces,Models,Core,Web,Sdk}` | YAML DSL parser, AST evaluator, REST controllers                      |
| `FireflyFramework.Plugins.{Api,Core}`                         | Lifecycle SPI, McMaster hot-reload assembly loader                     |

### Adapter tier

| Project                                                         | Purpose                                                       |
|-----------------------------------------------------------------|---------------------------------------------------------------|
| `FireflyFramework.Client`                                       | REST / SOAP / WebSocket / gRPC builders with Polly resilience |
| `FireflyFramework.Idp`                                          | `IIdpAdapter` contract                                        |
| `FireflyFramework.Idp.Keycloak`                                 | Token endpoint + admin REST API                               |
| `FireflyFramework.Idp.AzureAd`                                  | MSAL + Microsoft Graph                                        |
| `FireflyFramework.Idp.AwsCognito`                               | AWSSDK.CognitoIdentityProvider with full admin surface        |
| `FireflyFramework.Idp.InternalDb`                               | BCrypt + JWT, revocation store, role catalog                  |
| `FireflyFramework.Ecm`                                          | Adapter framework, 14 ports, NoOp + Local adapters            |
| `FireflyFramework.Ecm.Storage.{Aws,Azure}`                      | S3 + Azure Blob document content adapters                     |
| `FireflyFramework.Ecm.ESignature.{DocuSign,AdobeSign,Logalty}`  | Three e-signature provider adapters                           |
| `FireflyFramework.Notifications{,.Core}`                        | Email / SMS / Push contracts + dispatcher with preferences    |
| `FireflyFramework.Notifications.{SendGrid,Twilio,Resend,Firebase}` | Channel adapters                                           |
| `FireflyFramework.Callbacks.{Interfaces,Models,Core,Sdk,Web}`   | Outbound callback subsystem                                   |
| `FireflyFramework.Webhooks.{Interfaces,Core,Processor,Sdk,Web}` | Inbound webhook subsystem (Stripe / GitHub / Twilio sigs)     |
| `FireflyFramework.ConfigServer`                                 | Spring-Cloud-Config-compatible REST endpoints                 |

### Starter tier

| Project                                | Composes                                                       |
|----------------------------------------|----------------------------------------------------------------|
| `FireflyFramework.Starter.Core`        | Web + Cache + Observability + EDA + CQRS                       |
| `FireflyFramework.Starter.Application` | Core + Plugins (IDP / orchestration registered per service)    |
| `FireflyFramework.Starter.Domain`      | Core + EventSourcing (in-memory store by default)              |
| `FireflyFramework.Starter.Data`        | Core (consumer supplies its own `DbContext`)                   |
| `FireflyFramework.BackOffice`          | Application + back-office context resolver and middleware      |

## A complete service end-to-end

The runnable example at `samples/FireflyFramework.Samples.OrdersService/Program.cs`
demonstrates the recommended composition. The shape is:

```csharp
using FireflyFramework.Cqrs.Buses;
using FireflyFramework.Starter.Core;
using FireflyFramework.Web.DependencyInjection;
using ExecutionContext = FireflyFramework.Cqrs.Context.ExecutionContext;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFireflyCore(
    builder.Configuration,
    serviceName:    "orders-service",
    serviceVersion: "1.0.0",
    cqrsAssemblies: new[] { typeof(Program).Assembly });

var app = builder.Build();
app.UseFireflyWeb();   // adds GlobalExceptionHandlerMiddleware + IdempotencyMiddleware

app.MapPost("/api/orders", async (PlaceOrderRequest req, ICommandBus bus, CancellationToken ct) =>
{
    var ctx = new ExecutionContext { UserId = "demo-user", TenantId = "demo-tenant" };
    var orderId = await bus.SendAsync(new PlaceOrderCommand(req.Sku, req.Quantity, req.UnitPrice), ctx, ct);
    return Results.Created($"/api/orders/{orderId}", new { orderId });
});

await app.RunAsync();
```

`AddFireflyCore` wires Web + Cache + Observability + EDA + CQRS in one
call. `UseFireflyWeb` registers `GlobalExceptionHandlerMiddleware` (every
unhandled exception becomes RFC 7807 `application/problem+json`) and
`IdempotencyMiddleware` (any request carrying an `Idempotency-Key` header
returns the cached response on retry).

## Configuration

Every option binds under the `Firefly:*` namespace in `appsettings.json`
(or the matching environment variables — `Firefly__Web__Idempotency__Enabled`
and so on). The full schema, with example values for every section, lives
in [`docs/CONFIGURATION.md`](docs/CONFIGURATION.md).

```json
{
  "Firefly": {
    "Web":           { "Idempotency": { "Enabled": true, "TtlSeconds": 600 } },
    "Cache":         { "DefaultProvider": "Memory" },
    "Observability": { "Otel": { "OtlpEndpoint": "http://otel-collector:4317" } },
    "Eda":           { "Provider": "InMemory" }
  }
}
```

## Documentation

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — Tier-by-tier reference,
  dependency-direction graph, process model, versioning policy.
- [`docs/MIGRATION-GUIDE.md`](docs/MIGRATION-GUIDE.md) — Java to .NET
  cookbook covering Reactor types, Spring DI, configuration, web layer,
  persistence, CQRS, EDA, resilience, observability, validation, testing.
- [`docs/CONFIGURATION.md`](docs/CONFIGURATION.md) — Every `Firefly:*`
  configuration section with example values.
- [`docs/MODULES.md`](docs/MODULES.md) — One-line description of every
  project plus its Java original.
- [`docs/AUDIT.md`](docs/AUDIT.md) — Java vs .NET feature parity audit
  (three rounds of systematic review, including stub elimination, ECM
  adapter framework completion, EDA filter family, CQRS fluent builders,
  orchestration dead-letter and compensation policies).

Each project under `src/` has its own `README.md` describing its public
surface, options class, and usage examples.

## Versioning

The .NET line uses the same calendar version as the Java line
(`26.04.01` = April 1st, 2026). When the Java side ships a new release,
`Directory.Build.props`'s `<Version>` is bumped in lockstep so a service
running version *X* on either platform consumes the same contract.

`Directory.Packages.props` pins every NuGet to a known-good version.
When a transitive dependency forces a newer version (for example,
Steeltoe forcing `System.Text.Json 9.0.8`), the pinned version is bumped
rather than allowing a floating range.

## Continuous integration

`.github/workflows/ci.yml` runs `dotnet restore` → `dotnet build -c Release`
→ `dotnet test` (with TRX logger and artefact upload) on every push and
pull request to `main`, plus a `dotnet pack` job that publishes `.nupkg`
artefacts on `main` only.

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md) for build prerequisites, the
"adding a project" recipe, and the .NET conventions the codebase follows
(file-scoped namespaces, naming, sub-module pattern, idiomatic async
shapes).

## License

Apache License 2.0. See [`LICENSE`](LICENSE).
