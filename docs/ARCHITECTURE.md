# Firefly Framework .NET — Architecture

> Version 26.04.01 — full port of the Java/Spring Boot Firefly Framework
> to .NET 10 with idiomatic, modern .NET technologies.

## 1. Overview

Firefly Framework is a **batteries-included platform** for building reactive,
event-driven, resilient microservices. The .NET port keeps the same logical
boundaries as the Java original (foundational → platform → adapters →
starters) so a developer fluent in either stack can navigate both with no
surprises.

```
┌──────────────────────────────────────────────────────────────┐
│                      04 — Starters (BoM-style)               │
│  Core / Application / Domain / Data / BackOffice             │
└──────────────────────────────────────────────────────────────┘
           ▲                                       ▲
           │                                       │
┌──────────────────────────────────────────────────────────────┐
│                       03 — Adapters                          │
│  IDP (4) · ECM (5) · Notifications (5) · Callbacks (5) ·     │
│  Webhooks (5) · ConfigServer · Client (REST/SOAP/WS)         │
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

Arrows point **downward** (depend on): higher tiers consume lower tiers
through abstractions only. Adapters bind a port (interface in `Platform`)
to a concrete technology (Keycloak, Azure AD, S3, Stripe, etc.).

## 2. Tier-by-tier reference

### 2.1 Foundational (`01-Foundational/`)

| Module                       | Responsibility                                                            | Java analogue                              |
|------------------------------|---------------------------------------------------------------------------|--------------------------------------------|
| `FireflyFramework.Kernel`    | RFC 7807 `ProblemDetails`, `OperationResult<T>`, `IClock`, exit codes      | `firefly-common`                           |
| `FireflyFramework.Utils`     | Slug/Crypto/IO helpers, `Try.Of`, `RetryUtils`                            | `firefly-common-utils`                     |
| `FireflyFramework.Validators`| Country / IBAN / VAT / Phone / E-mail validators                          | `firefly-common-validators`                |
| `FireflyFramework.Web`       | RFC 7807 middleware, correlation-ID, MDC scope, `[Idempotent]`            | `firefly-web` + `firefly-spring-utils`     |

### 2.2 Platform (`02-Platform/`)

| Module                                | Tech                                  | Java analogue                                |
|---------------------------------------|---------------------------------------|----------------------------------------------|
| `FireflyFramework.Cache`              | StackExchange.Redis + IMemoryCache    | `firefly-common-cache`                       |
| `FireflyFramework.Observability`      | OpenTelemetry .NET                    | `firefly-otel-spring-boot-starter`           |
| `FireflyFramework.Data`               | EF Core 10 (InMemory + Npgsql)         | `firefly-common-data` (R2DBC)                |
| `FireflyFramework.Cqrs`               | Command/Query bus + handler discovery | `firefly-common-cqrs`                        |
| `FireflyFramework.Eda`                | Confluent.Kafka 2.x · RabbitMQ.Client | `firefly-common-eda`                         |
| `FireflyFramework.EventSourcing`      | Aggregates, snapshots, projections, outbox, upcasters | `firefly-event-sourcing-spring-boot-starter` |
| `FireflyFramework.Orchestration`      | Saga / Workflow / TCC                 | `firefly-common-domain` orchestration        |
| `FireflyFramework.RuleEngine.*`       | YAML DSL → in-memory rule graph       | `firefly-common-rule-engine`                 |
| `FireflyFramework.Plugins.*`          | McMaster.NETCore.Plugins hot-reload   | `firefly-platform-plugins`                   |

### 2.3 Adapters (`03-Adapters/`)

Every adapter implements a *port* defined in the platform layer.

```
IIdpAdapter        →  Keycloak | Auth0 | AzureAd | Cognito
IEcmAdapter        →  Sharepoint | OneDrive | Box | GoogleDrive | Drupal
INotificationAdapter→  Email | SMS | Push | Slack | Webhook
ICallbackAdapter   →  DocuSign | Adobe Sign | Twilio Voice | Vonage Voice | Calendar
IWebhookProcessor  →  Stripe | GitHub | Twilio | Generic HMAC
```

Plus: `FireflyFramework.Client` (REST, SOAP, WebSocket builders), and
`FireflyFramework.ConfigServer` (Steeltoe Spring Cloud Config client).

### 2.4 Starters (`04-Starters/`)

These are *meta-packages* (no code, only `<PackageReference>`s) — analogous
to the Spring Boot starters. They guarantee the canonical set of versions.

| Starter                              | Includes                                                       |
|--------------------------------------|----------------------------------------------------------------|
| `FireflyFramework.Starter.Core`      | Kernel + Utils + Validators + Web + Cache + Observability      |
| `FireflyFramework.Starter.Application` | + CQRS + EDA + RuleEngine + Plugins                          |
| `FireflyFramework.Starter.Domain`    | + Orchestration + EventSourcing                                |
| `FireflyFramework.Starter.Data`      | + Data (EF Core)                                               |
| `FireflyFramework.BackOffice`        | Admin UI + REST controllers (Razor Pages)                      |

### 2.5 Tests / Samples (`05-Tests/`, `06-Samples/`)

* `FireflyFramework.Tests` — xUnit suite covering 100% of platform tiers.
* `FireflyFramework.Samples.OrdersService.{Interfaces,Models,Core,Web,Sdk}`
  — five-project reference service showing the canonical scaffolding.
  See [SERVICE-SCAFFOLDING.md](SERVICE-SCAFFOLDING.md).

## 3. Cross-cutting concerns

### 3.1 Configuration (`Firefly:*` namespace)

All starters bind under the `Firefly` root in `appsettings.json` /
environment variables. See [CONFIGURATION.md](CONFIGURATION.md) for the
complete map.

### 3.2 Error handling

Every public surface returns `OperationResult<T>` (success / failure +
RFC 7807). Web controllers translate failures into a Problem+JSON
response via `ProblemDetailsMiddleware`. There is **no** mixing of
exceptions and result types across boundaries.

### 3.3 Logging / tracing

* `Microsoft.Extensions.Logging` everywhere.
* `OpenTelemetry.Trace` propagates W3C `traceparent` between services.
* Correlation ID middleware copies `X-Correlation-Id` (or generates one)
  into both the log scope and the outbound `HttpClient` headers.

### 3.4 Resilience

`Polly v8` pipelines provide retry / circuit-breaker / timeout /
rate-limiter behaviours. The `Microsoft.Extensions.Http.Resilience`
package wires these into every named `HttpClient`.

### 3.5 Async model

* `IAsyncEnumerable<T>` everywhere we previously had Reactor `Flux`.
* `Task<T>` everywhere we had `Mono`.
* `ValueTask` for hot paths (caches, schedulers).

## 4. Dependency direction (concrete graph)

```
Kernel ─────┐
Utils ──────┤
Validators ─┤
Web ────────┤── Cache ──┐
            └─ Observability ─┐
                              ├── Data ─┐
                              │         ├── CQRS ─────┐
                              │         │             ├── Orchestration ─┐
                              │         │             │                  ├── EventSourcing ─┐
                              │         │             │                  │                  ├── Adapters
                              │         │             │                  │                  └── Starters
                              │         │             ├── EDA           ──┘
                              │         └── Plugins(.Api/.Core)         ──┘
                              └── RuleEngine(.Interfaces/.Models/.Core/.Web/.Sdk)
```

* No circular references — verified by `dotnet build` graph.
* No reverse references from a tier to a higher tier.

## 5. Process model

A typical deployable assembled with the Firefly starters is:

```
                ┌──── ASP.NET Core 10 host ────┐
                │                             │
   Inbound ───▶│  Web (Problem+JSON, JWT,   │──▶ Domain (CQRS handlers)
   HTTP/gRPC   │   correlation-id)           │       │
                │                             │       ▼
                │   ┌────── Polly v8 ──────┐ │   EventSourcing
                │   │ retry / cb / timeout  │ │       │ (snapshot/outbox)
                │   └───────────────────────┘ │       ▼
                └─────────────────────────────┘   EDA (Kafka/Rabbit)
                                                     │
                                                     ▼
                                                 Adapters
```

## 6. Versioning policy

* All projects share the version **26.04.01**, set centrally in
  `Directory.Build.props`.
* `Directory.Packages.props` pins every NuGet to a known-good version
  (Maven BoM equivalent).
* When a transitive dependency requires a newer version (e.g. Steeltoe
  forcing `System.Text.Json 9.0.8`), we bump the pinned version rather
  than allow a floating range.

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
.NET equivalent in `src/`.
