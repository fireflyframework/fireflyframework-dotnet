# Firefly Framework — .NET 9

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![Version](https://img.shields.io/badge/version-26.04.01-green.svg)](#)

A complete .NET 9 port of the Java/Spring Boot **Firefly Framework**
(`org.fireflyframework:*:26.04.01`). Same contracts, same starter pattern, same
calendar version — re-implemented with idiomatic .NET tooling.

> **52 projects · 157 tests · 0 stubs · 0 build errors**

## Quick start

```bash
# 1. Install the .NET 9 SDK (once)
brew install dotnet@9          # or use any official .NET 9 SDK

# 2. Configure environment
source .envrc                  # exports DOTNET_ROOT and PATH

# 3. Build & test
dotnet build FireflyFramework.sln
dotnet test tests/FireflyFramework.Tests/
```

## Repository layout

```
fireflyframework-dotnet/
├── docs/                              ← Architecture, migration guide, modules, audit
├── src/                               ← 52 projects
│   ├── FireflyFramework.Kernel/             (foundational)
│   ├── FireflyFramework.Utils/
│   ├── FireflyFramework.Validators/
│   ├── FireflyFramework.Web/
│   ├── FireflyFramework.Cache/              (platform)
│   ├── FireflyFramework.Observability/
│   ├── FireflyFramework.Data/
│   ├── FireflyFramework.Cqrs/
│   ├── FireflyFramework.Eda/
│   ├── FireflyFramework.EventSourcing/
│   ├── FireflyFramework.Orchestration/
│   ├── FireflyFramework.RuleEngine.{Interfaces,Models,Core,Web,Sdk}/
│   ├── FireflyFramework.Plugins.{Api,Core}/
│   ├── FireflyFramework.Client/             (adapters)
│   ├── FireflyFramework.Idp{,.AzureAd,.AwsCognito,.Keycloak,.InternalDb}/
│   ├── FireflyFramework.Ecm/
│   ├── FireflyFramework.Ecm.Storage.{Aws,Azure}/
│   ├── FireflyFramework.Ecm.ESignature.{DocuSign,AdobeSign,Logalty}/
│   ├── FireflyFramework.Notifications{,.Core,.SendGrid,.Twilio,.Resend,.Firebase}/
│   ├── FireflyFramework.Callbacks.{Interfaces,Models,Core,Sdk,Web}/
│   ├── FireflyFramework.Webhooks.{Interfaces,Core,Processor,Sdk,Web}/
│   ├── FireflyFramework.ConfigServer/
│   ├── FireflyFramework.Starter.{Core,Application,Domain,Data}/
│   └── FireflyFramework.BackOffice/         (starters)
├── tests/FireflyFramework.Tests/      ← xUnit suite (157 tests)
├── samples/FireflyFramework.Samples.OrdersService/
├── Directory.Build.props              ← parent props (TargetFramework, Version)
├── Directory.Packages.props           ← Central Package Management (BoM analogue)
├── FireflyFramework.sln
├── LICENSE                            ← Apache-2.0
└── .envrc                             ← `source` to wire up dotnet@9
```

## What you get

| Tier              | Coverage                                                                           |
|-------------------|------------------------------------------------------------------------------------|
| **Foundational**  | RFC 7807 ProblemDetails, OperationResult&lt;T&gt;, IBAN/BIC/VAT/Phone validators, Scriban templates + iText PDF (watermark, AES-256, bookmarks) |
| **Platform**      | StackExchange.Redis cache, OpenTelemetry .NET, EF Core 9 with filter/pagination DSL, CQRS bus + handler discovery, Kafka/RabbitMQ EDA with Avro/Protobuf serdes, event sourcing with snapshots/upcasters/projections/outbox, Saga/Workflow/TCC engines, YAML rule engine, McMaster plugin loader |
| **Adapters**      | REST/SOAP/WebSocket/gRPC client builder, IDP × 4 (Keycloak/Azure AD/Cognito/InternalDB) with full admin surfaces, ECM × 5 storage + e-signature, Notifications × 5 (SendGrid/Twilio/Resend/FCM/Slack), Callbacks subsystem (router + DLQ + audit), Webhooks subsystem (Stripe/GitHub/Twilio signature validation) |
| **Starters**      | One-call wiring: `Core`, `Application`, `Domain`, `Data`, `BackOffice`              |

## Documentation

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — Tier-by-tier reference, dependency graph, process model
- [`docs/MIGRATION-GUIDE.md`](docs/MIGRATION-GUIDE.md) — Java → .NET cookbook (DI, reactive types, Spring annotations, etc.)
- [`docs/CONFIGURATION.md`](docs/CONFIGURATION.md) — Every `Firefly:*` settings section
- [`docs/MODULES.md`](docs/MODULES.md) — One-line description of each project
- [`docs/AUDIT.md`](docs/AUDIT.md) — Java-vs-.NET feature parity audit (3 rounds)

## Composition example

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddFireflyCore(builder.Configuration, "orders", "1.0.0", typeof(Program).Assembly)
    .AddDbContext<OrdersDbContext>(o => o.UseNpgsql(builder.Configuration["Firefly:Data:ConnectionString"]));

var app = builder.Build();
app.UseFireflyMiddleware();        // problem-details + correlation-id + idempotency
app.MapOrdersEndpoints();
await app.RunAsync();
```

That replaces the Java `@SpringBootApplication` + `@EnableFireflyXxx` set.

## Versioning

The .NET line uses the same calendar version as Java (`26.04.01`). When the Java
side ships a new release, `Directory.Build.props`’s `<Version>` is bumped in
lockstep.

## License

[Apache-2.0](LICENSE) © 2026 Firefly Software Foundation
