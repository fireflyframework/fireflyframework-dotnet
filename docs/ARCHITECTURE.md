# Firefly Framework .NET — Architecture

> Version 26.04.01 — full port of the Java/Spring Boot Firefly Framework
> to .NET 10 with idiomatic, modern .NET technologies.

## 1. Overview

Firefly Framework is a **batteries-included platform** for building
event-driven, resilient microservices. The .NET port keeps the same
logical boundaries as the Java original (foundational → platform →
adapters → starters) so a developer fluent in either stack can navigate
both with no surprises.

```
┌──────────────────────────────────────────────────────────────┐
│                  04 — Starters (BoM-style)                   │
│   Core / Application / Domain / Data / BackOffice            │
└──────────────────────────────────────────────────────────────┘
           ▲                                       ▲
           │                                       │
┌──────────────────────────────────────────────────────────────┐
│                       03 — Adapters                          │
│  IDP (4)  ·  ECM (storage 2 + e-signature 3)                 │
│  Notifications (4 channels)  ·  Callbacks (5 sub-modules)    │
│  Webhooks (5 sub-modules)  ·  ConfigServer  ·  Client        │
└──────────────────────────────────────────────────────────────┘
           ▲                                       ▲
           │                                       │
┌──────────────────────────────────────────────────────────────┐
│                       02 — Platform                          │
│  Cache · Observability · Data · CQRS · EDA · EventSourcing   │
│  Orchestration (Saga/Workflow/TCC) · RuleEngine · Plugins    │
└──────────────────────────────────────────────────────────────┘
           ▲                                       ▲
           │                                       │
┌──────────────────────────────────────────────────────────────┐
│                     01 — Foundational                        │
│         Kernel · Utils · Validators · Web                    │
└──────────────────────────────────────────────────────────────┘
```

Arrows point **upward** (depend on): higher tiers consume lower tiers
through abstractions only. Adapters bind a port (interface defined in
the platform layer) to a concrete technology (Keycloak, S3, SendGrid,
Stripe, etc.).

## 2. Tier-by-tier reference

### 2.1 Foundational (`01-Foundational/`)

| Module                       | Responsibility                                                                       | Java analogue                              |
|------------------------------|--------------------------------------------------------------------------------------|--------------------------------------------|
| `FireflyFramework.Kernel`    | RFC 7807 `ProblemDetail`, `OperationResult<T>`, `IClock`, `FireflyException` family  | `firefly-common`                           |
| `FireflyFramework.Utils`     | `Try.Of`, `RetryUtils`, Slug / Crypto / IO helpers, Scriban + iText 7 templating     | `firefly-common-utils`                     |
| `FireflyFramework.Validators`| 16 financial / identity `[Valid…]` `ValidationAttribute`s — IBAN, BIC, credit card, currency, phone, password strength, etc. | `firefly-common-validators` |
| `FireflyFramework.Web`       | RFC 7807 middleware, correlation-ID, `IdempotencyMiddleware` + `[DisableIdempotency]`, PII masking, 27 typed HTTP exceptions | `firefly-web` + `firefly-spring-utils` |

### 2.2 Platform (`02-Platform/`)

| Module                                | Backing technology                       | Java analogue                                |
|---------------------------------------|------------------------------------------|----------------------------------------------|
| `FireflyFramework.Cache`              | StackExchange.Redis + `IMemoryCache`     | `firefly-common-cache`                       |
| `FireflyFramework.Observability`      | OpenTelemetry .NET (traces / metrics / logs) + Serilog | `firefly-otel-spring-boot-starter` |
| `FireflyFramework.Data`               | EF Core 10 (InMemory + Npgsql + SQL Server) | `firefly-common-data` (R2DBC)             |
| `FireflyFramework.Cqrs`               | Command / query bus + handler discovery + query cache + event-driven invalidation | `firefly-common-cqrs` |
| `FireflyFramework.Eda`                | Confluent.Kafka + RabbitMQ.Client + in-memory bus + Schema Registry (Avro / Protobuf) | `firefly-common-eda` |
| `FireflyFramework.EventSourcing`      | `AggregateRoot`, snapshots, projections, transactional outbox, event upcasters | `firefly-event-sourcing-spring-boot-starter` |
| `FireflyFramework.Orchestration`      | Saga (DAG + compensation) / Workflow (signals + timers) / TCC engines | `firefly-common-domain` orchestration |
| `FireflyFramework.RuleEngine.*`       | YAML DSL → AST + visitor evaluator + REST admin | `firefly-common-rule-engine`           |
| `FireflyFramework.Plugins.*`          | `IPlugin` lifecycle + extension registry; McMaster.NETCore.Plugins for hot-reload | `firefly-platform-plugins` |

### 2.3 Adapters (`03-Adapters/`)

Every adapter implements a *port* defined in the platform / kernel
layer. Pick exactly one adapter per port at registration time.

| Port                                                       | Adapters in this repo                              |
|------------------------------------------------------------|----------------------------------------------------|
| `IIdpAdapter`                                              | `Idp.Keycloak`, `Idp.AzureAd`, `Idp.AwsCognito`, `Idp.InternalDb` |
| ECM `IDocumentContentPort`                                 | `Ecm.Storage.Aws` (S3), `Ecm.Storage.Azure` (Azure Blob) |
| ECM `ISignatureEnvelopePort`                               | `Ecm.ESignature.DocuSign`, `Ecm.ESignature.AdobeSign`, `Ecm.ESignature.Logalty` |
| `IEmailProvider` / `ISmsProvider` / `IPushProvider`        | `Notifications.SendGrid`, `Notifications.Resend`, `Notifications.Twilio`, `Notifications.Firebase` |
| `IWebhookSignatureValidator`                               | Stripe / GitHub / Twilio / generic-HMAC validators in `Webhooks.Core` |

Plus the cross-cutting transports:

- `FireflyFramework.Client` — REST / SOAP / WebSocket / gRPC builders
  with Polly v8 resilience.
- `FireflyFramework.Callbacks.*` — outbound webhook dispatch
  (configuration store, HMAC-SHA256 signing, Polly retry, audit log,
  REST admin, typed HTTP SDK).
- `FireflyFramework.Webhooks.*` — inbound webhook ingestion
  (signature validation, rate-limit, enrichment, dispatch, DLQ, REST
  ingestion endpoint, typed HTTP SDK).
- `FireflyFramework.ConfigServer` — Spring-Cloud-Config-compatible REST
  endpoint backed by file-system property sources.

### 2.4 Starters (`04-Starters/`)

These are *meta-packages* that compose a curated set of registrations.
Each one is invoked from the host's composition root with one call.

| Starter                                | What `AddFirefly{X}` does                                                          |
|----------------------------------------|------------------------------------------------------------------------------------|
| `FireflyFramework.Starter.Core`        | Web + Observability + Cache + EDA + CQRS                                           |
| `FireflyFramework.Starter.Application` | Core + plugin extension registry + plugin manager (IDP / orchestration adapters are service-specific — pick one and register it) |
| `FireflyFramework.Starter.Domain`      | Core + in-memory `IEventStore` (replace with the EF Core implementation for production) |
| `FireflyFramework.Starter.Data`        | Core (consumer registers their own `DbContext` via EF Core)                        |
| `FireflyFramework.BackOffice`          | Application + the back-office context resolver and middleware                      |

Each starter ships an embedded `Resources/banner.txt` that
`AddFirefly{X}` prints once at startup with ANSI colours, the active
service name, version, and the resolved .NET runtime.

### 2.5 Tests / Samples (`05-Tests/`, `06-Samples/`)

* `FireflyFramework.Tests` — single xUnit project covering every
  public surface across all four tiers.
* `FireflyFramework.Samples.OrdersService.{Interfaces,Models,Core,Web,Sdk}`
  — five-project reference service that demonstrates the canonical
  scaffolding documented in [SERVICE-SCAFFOLDING.md](SERVICE-SCAFFOLDING.md).

## 3. Cross-cutting concerns

### 3.1 Configuration (`Firefly:*` namespace)

All starters bind under the `Firefly` root in `appsettings.json` and the
matching environment variables. See [CONFIGURATION.md](CONFIGURATION.md)
for the complete map of every recognised section.

### 3.2 Error handling

Public surfaces either return `OperationResult<T>` (in domain code) or
throw a typed `FireflyException` subclass (in HTTP-facing code). The
`GlobalExceptionHandlerMiddleware` in `FireflyFramework.Web` converts
every unhandled exception into an RFC 7807 `application/problem+json`
response, attaching correlation id, error code, and the appropriate
HTTP status.

### 3.3 Logging / tracing

* `Microsoft.Extensions.Logging` everywhere; structured logging via
  Serilog enrichers configured in `FireflyFramework.Observability`.
* OpenTelemetry traces propagate W3C `traceparent` between services.
* The correlation-ID middleware copies `X-Correlation-Id` (or generates
  a fresh GUID) into the log scope and the outbound `HttpClient`
  headers.

### 3.4 Resilience

`Polly v8` pipelines provide retry / circuit-breaker / timeout /
rate-limiter behaviours. `Microsoft.Extensions.Http.Resilience` wires
them into every typed `HttpClient` registered through `Client` and the
SDK packages.

### 3.5 Async model

* `Task<T>` for async operations.
* `IAsyncEnumerable<T>` where the Java side returned a `Flux<T>` stream.
* `ValueTask<T>` is reserved for hot paths (cache `Get`, scheduler ticks).

## 4. Dependency direction

The strict layering is enforced by the `<ProjectReference>` graph — the
compiler refuses to let a lower tier reference a higher one. There are
no cycles: `dotnet build` topologically orders 52 source projects + 1
test project + 5 sample projects without warnings.

## 5. Process model

A typical deployable assembled with the Firefly starters is:

```
                ┌──── ASP.NET Core 10 host ─────┐
                │                               │
   Inbound ────▶│  Web (Problem+JSON, JWT,      │──▶ CQRS handlers
   HTTP / gRPC  │   correlation, idempotency)   │       │
                │                               │       ▼
                │  ┌────── Polly v8 ──────┐     │   EventSourcing
                │  │ retry / cb / timeout │     │       │ (snapshot / outbox)
                │  └──────────────────────┘     │       ▼
                └───────────────────────────────┘   EDA (Kafka / RabbitMQ)
                                                       │
                                                       ▼
                                                   Adapters
```

## 6. Versioning policy

* Every project shares the version **26.04.01**, set centrally in
  `Directory.Build.props`.
* `Directory.Packages.props` pins every NuGet to a known-good version
  (the .NET equivalent of a Maven BoM).
* When a transitive dependency forces a newer version, the central pin
  is bumped explicitly rather than allowing a floating range.

## 7. What is *not* in scope

The .NET port covers everything in the Java Firefly Framework **except**
projects explicitly outside the framework charter:

* `firefly-frontend-framework`, `flyfront`        — web UI
* `pyfly`                                          — Python sidecar
* `fireflyframework-genai`                         — LLM helpers
* `fireflyframework-cli`                           — CLI tooling
* `fireflyframework-claude-skills*`                — agent skills
* `secrets-vault`                                  — separate product
* `fireflyframework-agentic*`                      — agent runtime

Everything else listed in `fireflyframework-*/pom.xml` has a one-to-one
.NET equivalent under `src/`.
