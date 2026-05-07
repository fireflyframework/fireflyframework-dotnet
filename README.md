```
  _____.__                _____.__
_/ ____\__|______   _____/ ____\  | ___.__.
\   __\|  \_  __ \_/ __ \   __\|  |<   |  |
 |  |  |  ||  | \/\  ___/|  |  |  |_\___  |
 |__|  |__||__|    \___  >__|  |____/ ____|
                       \/           \/
```

# Firefly Framework for .NET

**A production-grade platform for building reactive, event-driven, resilient microservices on .NET 10.**

The Firefly Framework provides the cross-cutting machinery that every
non-trivial business service needs — error envelopes, idempotency,
correlation propagation, CQRS, event-driven messaging, event sourcing,
sagas, configuration servers, identity adapters, document management,
notifications, callbacks, webhooks — behind a single, opinionated
composition pattern.

This repository is the official .NET port of the Java/Spring Boot
[`org.fireflyframework`](https://fireflyframework.org) platform. It
preserves every public contract, configuration key, and wire format from
the Java release line, re-implemented with idiomatic .NET 10 tooling
(C# 14, ASP.NET Core 10, EF Core 10, OpenTelemetry, Polly v8). A service
running version *X* on either platform consumes the same contracts and
emits the same wire format.

---

## Why this framework

Modern back-office systems aren't bottlenecked by writing the next
controller. They're bottlenecked by getting **the same** controller,
**the same** error response, **the same** correlation id, **the same**
saga compensation, **the same** observability story across every service
in the platform. Every team that re-invents these picks slightly
different conventions and the platform fragments.

Firefly Framework treats those concerns as solved problems. A service
written on top is:

- **Composed, not constructed.** A single `AddFireflyCore` /
  `AddFireflyApplication` / `AddFireflyDomain` / `AddFireflyData` call
  wires the whole infrastructure tier. Authors write commands, queries,
  handlers, and endpoints — nothing more.
- **Symmetric across runtimes.** The wire contract, the
  `application/problem+json` shape, the idempotency-key semantics, the
  saga step definitions, the event envelopes — all identical to the
  Java side.
- **Pluggable at the adapter layer.** Each integration point (IDP, ECM,
  storage, e-signature, notification channel, message broker) is a port
  with multiple adapter implementations selected at registration time.
- **Observable by default.** OpenTelemetry traces and metrics, Serilog
  structured logging, RFC 7807 error envelopes, and a startup banner
  that names the application, version, and runtime are all on out of
  the box.
- **Honest about boundaries.** Every public method either runs real
  code or throws `NotSupportedException` with an actionable message
  documenting why the underlying provider does not support the
  operation. There are no silent stubs.

---

## Architecture at a glance

The framework is organised into four strictly-layered tiers, with a
left-to-right dependency direction:

```
┌────────────────┐   ┌──────────────────┐   ┌───────────────┐   ┌──────────────────┐
│  FOUNDATIONAL  │ → │     PLATFORM     │ → │   ADAPTERS    │ → │     STARTERS     │
│                │   │                  │   │               │   │                  │
│  Kernel        │   │  Cache           │   │  Client       │   │  Starter.Core    │
│  Utils         │   │  Observability   │   │  Idp.*        │   │  Starter.App.    │
│  Validators    │   │  Data            │   │  Ecm.*        │   │  Starter.Domain  │
│  Web           │   │  Cqrs            │   │  Notifications│   │  Starter.Data    │
│                │   │  Eda             │   │  Callbacks.*  │   │  BackOffice      │
│                │   │  EventSourcing   │   │  Webhooks.*   │   │                  │
│                │   │  Orchestration   │   │  ConfigServer │   │                  │
│                │   │  RuleEngine.*    │   │               │   │                  │
│                │   │  Plugins.*       │   │               │   │                  │
└────────────────┘   └──────────────────┘   └───────────────┘   └──────────────────┘
```

Each tier may depend on the tiers to its left, never to its right.
The compiler enforces the layering — there is no project reference
that violates it.

### Foundational tier — primitives every service uses

| Project                          | What it provides                                                                       |
|----------------------------------|----------------------------------------------------------------------------------------|
| `FireflyFramework.Kernel`        | RFC 7807 `ProblemDetail`, `OperationResult<T>`, `IClock`, `FireflyException` hierarchy |
| `FireflyFramework.Utils`         | `Try.Of`, `RetryUtils`, `TemplateRenderUtil` (Scriban + iText 7 PDF), AES-256 helpers  |
| `FireflyFramework.Validators`    | IBAN, BIC, Luhn, VAT, phone (E.164), e-mail, password strength, sort code, IDs (16)    |
| `FireflyFramework.Web`           | RFC 7807 middleware, idempotency, correlation IDs, PII masking, typed exception family |

### Platform tier — the infrastructure layer

| Project                                                       | What it provides                                                                |
|---------------------------------------------------------------|---------------------------------------------------------------------------------|
| `FireflyFramework.Cache`                                      | `ICacheAdapter` port with Memory, Redis, and NoOp adapters; primary + fallback  |
| `FireflyFramework.Observability`                              | OpenTelemetry traces, metrics, logs; Serilog enrichers; health-check primitives |
| `FireflyFramework.Data`                                       | EF Core 10 with InMemory / Postgres / SQL Server providers, generic filter DSL  |
| `FireflyFramework.Cqrs`                                       | Command + query buses, fluent dispatch, validation, query caching, invalidation |
| `FireflyFramework.Eda`                                        | Kafka + RabbitMQ + InMemory; Schema Registry; resilient publisher; filter family |
| `FireflyFramework.EventSourcing`                              | `AggregateRoot`, snapshots, transactional outbox, projection runner, upcasting  |
| `FireflyFramework.Orchestration`                              | Saga (DAG + compensation), Workflow, TCC engines; dead-letter; compensation policy |
| `FireflyFramework.RuleEngine.{Interfaces,Models,Core,Web,Sdk}` | YAML DSL, AST + visitor evaluator, REST admin, typed SDK                       |
| `FireflyFramework.Plugins.{Api,Core}`                         | Lifecycle SPI, McMaster-based hot-reload assembly loader                        |
| `FireflyFramework.Resilience`                                 | Resilience4j-style attributes + Polly v8 named pipelines (CB / retry / RL / bulkhead / timeout) |
| `FireflyFramework.Security`                                   | Spring Security port: `SecurityContext`, `[PreAuthorize]`, JWT, BCrypt          |
| `FireflyFramework.Aop`                                        | `[Aspect]` + before/after/around advice + AspectJ-style pointcuts               |
| `FireflyFramework.Scheduling`                                 | `[Scheduled]` cron / fixed-rate / fixed-delay; Cronos parser                    |
| `FireflyFramework.Messaging`                                  | Lightweight `IMessageBroker` for in-process pub/sub                             |
| `FireflyFramework.Actuator`                                   | Spring Actuator endpoints: `/info`, `/env`, `/beans`, `/metrics`, `/loggers`, `/threaddump`, `/mappings` |
| `FireflyFramework.Admin`                                      | Spring Boot Admin Server-style instance registry + heartbeat client             |
| `FireflyFramework.I18n`                                       | `IMessageSource` + `ILocaleResolver`, JSON resource bundles, parent-culture fallback |
| `FireflyFramework.Session`                                    | `IFireflySession` distributed session abstraction (in-memory + Redis)           |
| `FireflyFramework.WebSocket`                                  | Server-side `[WebSocketMapping]`, lifecycle hooks, group broadcast              |
| `FireflyFramework.Shell`                                      | `[ShellComponent/Method]`, `CommandLineRunner`, interactive shell               |
| `FireflyFramework.Testing`                                    | `FireflyTestBase`, `EventCapturePublisher`, slice attributes                    |
| `FireflyFramework.Cli`                                        | `firefly` dotnet tool: scaffold services, handlers, sagas, migrations           |
| `FireflyFramework.Agentic`                                    | LLM agent loop, tools, memory; provider-agnostic chat / embedding ports         |
| `FireflyFramework.AgenticBridge`                              | REST/SSE client for Python-hosted agents                                        |

### Adapter tier — pluggable integrations

| Project                                                            | What it provides                                                  |
|--------------------------------------------------------------------|-------------------------------------------------------------------|
| `FireflyFramework.Client`                                          | REST / SOAP / WebSocket / gRPC builders with Polly v8 resilience  |
| `FireflyFramework.Idp.{Keycloak,AzureAd,AwsCognito,InternalDb}`    | Token, admin, and user-management surfaces per provider           |
| `FireflyFramework.Ecm`                                             | Adapter framework with 38 feature flags; document/folder/version/search/signature ports |
| `FireflyFramework.Ecm.Storage.{Aws,Azure}`                         | S3 + Azure Blob document content adapters                         |
| `FireflyFramework.Ecm.ESignature.{DocuSign,AdobeSign,Logalty}`     | E-signature provider adapters (JWT grant / OAuth2)                |
| `FireflyFramework.Notifications{,.Core,.SendGrid,.Twilio,.Resend,.Firebase,.Smtp}` | Dispatcher with per-user channel preferences + five channel adapters (incl. plain SMTP) |
| `FireflyFramework.Callbacks.{Interfaces,Models,Core,Sdk,Web}`      | Outbound callback subsystem (HMAC + Polly retry, audit log)       |
| `FireflyFramework.Webhooks.{Interfaces,Core,Processor,Sdk,Web}`    | Inbound webhook subsystem (Stripe / GitHub / Twilio / generic HMAC) |
| `FireflyFramework.ConfigServer`                                    | Spring-Cloud-Config-compatible REST endpoints                     |

### Starter tier — one-call composition

| Starter                                | Composition                                                                                |
|----------------------------------------|--------------------------------------------------------------------------------------------|
| `FireflyFramework.Starter.Core`        | Web + Cache + Observability + EDA + CQRS + Resilience + Messaging                          |
| `FireflyFramework.Starter.Application` | Core + Plugins + Resilience + Security + Actuator + Scheduling + Session + I18n + Aop + WebSocket |
| `FireflyFramework.Starter.Domain`      | Core + Event Sourcing + Aop                                                                |
| `FireflyFramework.Starter.Data`        | Core (consumer supplies its own `DbContext`)                                               |
| `FireflyFramework.BackOffice`          | Application + back-office context resolver and middleware                                  |

Each starter ships an embedded `banner.txt` printed at startup, naming
the active starter, the application name and version, and the resolved
.NET runtime — mirroring the Spring Boot banner-on-start behaviour.

---

## Service shape — the canonical five-project layout

Every microservice built on Firefly follows the same scaffolding,
mirroring the multi-module Maven layout used by Java services across
the Firefly platform:

```
your-service/
├── YourCompany.Domain.YourService.Interfaces/   # public DTOs, enums, V1-namespaced wire contract
├── YourCompany.Domain.YourService.Models/       # persistence entities + repository contracts
├── YourCompany.Domain.YourService.Core/         # commands, queries, handlers, mappers
├── YourCompany.Domain.YourService.Web/          # runnable ASP.NET Core 10 host
└── YourCompany.Domain.YourService.Sdk/          # typed HttpClient consuming only Interfaces
```

The dependency graph is strictly layered:

```
Interfaces ◄── Models ◄── Core ◄── Web
   ▲
   └────── Sdk
```

`Sdk` references **only** `Interfaces`, so cross-service callers pull in
your wire contract and nothing else — no persistence types, no business
logic. The compiler enforces the layering.

A complete reference implementation lives at
[`samples/FireflyFramework.Samples.OrdersService.*`](samples/). The
pattern, naming conventions, and rationale are documented in
[`docs/SERVICE-SCAFFOLDING.md`](docs/SERVICE-SCAFFOLDING.md).

---

## Quickstart — run the Orders sample

The fastest way to see Firefly working is to run the reference sample
that ships in this repository. It exercises every cross-cutting concern
(idempotency, query caching, OpenAPI, the startup banner) end-to-end on
the in-memory infrastructure tier — no Kafka, Redis, or database
required.

```bash
brew install dotnet                        # macOS — or any official .NET 10 installer
source .envrc                              # exports DOTNET_ROOT and prepends dotnet to PATH
dotnet build  FireflyFramework.sln
dotnet run    --project samples/FireflyFramework.Samples.OrdersService.Web
```

The startup banner identifies the active starter, the application name
and version, and the resolved .NET runtime, then ASP.NET Core hosting
takes over.

Place an order:

```bash
curl -X POST http://localhost:5000/api/v1/orders \
  -H 'Content-Type: application/json' \
  -H 'X-Idempotency-Key: demo-1' \
  -d '{"sku":"SKU-1","quantity":2,"unitPrice":12.50}'
# → 201 Created  { "orderId": "..." }
```

Replay the same request — the idempotency middleware returns the
cached response without re-running the handler:

```bash
curl -X POST http://localhost:5000/api/v1/orders \
  -H 'Content-Type: application/json' \
  -H 'X-Idempotency-Key: demo-1' \
  -d '{"sku":"SKU-1","quantity":2,"unitPrice":12.50}'
# → 201 Created  (identical body, no duplicate order)
```

Read the order back; the second call hits the per-query cache
configured by `GetOrderQuery`:

```bash
curl http://localhost:5000/api/v1/orders/<id-from-previous-response>
```

The OpenAPI document is at `/openapi/v1.json`. The sample's full
five-project shape — `.Interfaces`, `.Models`, `.Core`, `.Web`,
`.Sdk` — is the same one you copy when starting a new service: see
[`docs/SERVICE-SCAFFOLDING.md`](docs/SERVICE-SCAFFOLDING.md) for the
walkthrough.

---

## Build your first Firefly service

Once you've seen the sample running, here is the end-to-end recipe for
a brand-new service. The result is a runnable ASP.NET Core 10 host that
emits the Firefly startup banner, serves RFC 7807 problem responses,
honours `X-Idempotency-Key`, and dispatches commands through the CQRS
bus — all from a single project, in roughly forty lines of code.

### Prerequisites

* **.NET 10 SDK** — verify with `dotnet --version` (any `10.0.*` is fine).
* `curl`, or any HTTP client, for the smoke test.

### 1. Create the project

```bash
dotnet new webapi -n HelloFirefly -o HelloFirefly
cd HelloFirefly
rm WeatherForecast.cs                      # the template noise we don't need
```

### 2. Install the framework

The Firefly packages live under the `FireflyFramework.*` prefix on
**two registries**, depending on the version you want:

| Version channel | Registry | Setup |
|---|---|---|
| Stable releases (`26.04.01`) | NuGet.org | None — works out of the box |
| Pre-release builds (`26.05.01-preview`, `-rc`, …) | GitHub Packages | One-time `nuget.config` + a GitHub PAT with `read:packages` |

The starter meta-package wires the entire infrastructure tier (Web,
Cache, Observability, EDA, CQRS) in one call, so you only need this
single `add package` to start:

```bash
# Stable release from NuGet.org
dotnet add package FireflyFramework.Starter.Core

# Or a pre-release from GitHub Packages (after one-time setup; see below)
dotnet add package FireflyFramework.Starter.Core --version 26.05.01-preview
```

For the GitHub Packages path, follow the one-time setup in
[`docs/INSTALL.md`](docs/INSTALL.md) — it walks through PAT creation,
the `nuget.config` template, and the CI-friendly auth flow using the
auto-provided `GITHUB_TOKEN`.

If you want the rule engine, orchestration, or one of the IDP
adapters, add the corresponding `FireflyFramework.<Module>` package on
top (every `src/<Module>/README.md` describes its own surface).

### 3. Replace `Program.cs`

```csharp
using FireflyFramework.Cqrs.Buses;
using FireflyFramework.Cqrs.Commands;
using FireflyFramework.Starter.Core;
using FireflyFramework.Web.DependencyInjection;
using ExecutionContext = FireflyFramework.Cqrs.Context.ExecutionContext;

var builder = WebApplication.CreateBuilder(args);

// One-line wiring of the Firefly infrastructure tier.
builder.Services.AddFireflyCore(
    builder.Configuration,
    serviceName: "hello-firefly",
    serviceVersion: "1.0.0",
    cqrsAssemblies: new[] { typeof(Program).Assembly });

var app = builder.Build();

// RFC 7807 errors, correlation IDs, idempotency, PII masking — all on by default.
app.UseFireflyWeb();

app.MapPost("/api/v1/greet", async (GreetRequest request, ICommandBus bus, CancellationToken ct) =>
{
    var ctx = new ExecutionContext { UserId = "anonymous" };
    var greeting = await bus.SendAsync(new GreetCommand(request.Name), ctx, ct);
    return Results.Ok(new { greeting });
});

app.Run();

public sealed record GreetRequest(string Name);
public sealed record GreetCommand(string Name) : ICommand<string>;

public sealed class GreetHandler : ICommandHandler<GreetCommand, string>
{
    public Task<string> HandleAsync(GreetCommand cmd, ExecutionContext ctx, CancellationToken ct) =>
        Task.FromResult($"Hello, {cmd.Name}!");
}

public partial class Program;
```

### 4. Configure the framework (`appsettings.json`)

```jsonc
{
  "Firefly": {
    "Web":   { "Idempotency": { "Enabled": true, "HeaderName": "X-Idempotency-Key" } },
    "Cache": { "Provider": "Memory" }
  }
}
```

The full schema for every `Firefly:*` section lives in
[`docs/CONFIGURATION.md`](docs/CONFIGURATION.md).

### 5. Run it

```bash
dotnet run
```

You should see the Firefly ASCII banner, the resolved application name
(`hello-firefly@1.0.0`), and the Kestrel hosting line.

### 6. Smoke-test

```bash
curl -X POST http://localhost:5000/api/v1/greet \
  -H 'Content-Type: application/json' \
  -H 'X-Idempotency-Key: demo-1' \
  -d '{"name":"Ada"}'
# → 200 OK  { "greeting": "Hello, Ada!" }

# Replay with the same idempotency key — the cached response is returned
# without invoking the handler again.
curl -X POST http://localhost:5000/api/v1/greet \
  -H 'Content-Type: application/json' \
  -H 'X-Idempotency-Key: demo-1' \
  -d '{"name":"Ada"}'
# → 200 OK  (identical body, handler not re-run)
```

That's it. From here, the natural next steps are:

1. **Add a query** with `IQuery<TResult>` + `IQueryHandler<>` and dispatch via `IQueryBus.AskAsync`.
2. **Promote to the canonical 5-project layout** by copying the structure of [`samples/FireflyFramework.Samples.OrdersService.*`](samples/) — see [`docs/SERVICE-SCAFFOLDING.md`](docs/SERVICE-SCAFFOLDING.md).
3. **Pick a richer starter** — `Starter.Application` (adds plugins / orchestration / IDP), `Starter.Domain` (adds event sourcing), or `Starter.Data` (you supply the `DbContext`).

---

## Configuration

Every option binds under the `Firefly:*` namespace in `appsettings.json`,
with the standard ASP.NET Core override precedence (environment variables
like `Firefly__Web__Idempotency__Enabled`, command-line, user secrets).
The full schema — verified against the actual `*Options` classes — is in
[`docs/CONFIGURATION.md`](docs/CONFIGURATION.md).

```json
{
  "Firefly": {
    "Web":           { "Idempotency": { "Enabled": true, "HeaderName": "X-Idempotency-Key", "Ttl": "01:00:00" } },
    "Cache":         { "Provider": "Memory" },
    "Observability": { "Tracing": { "Enabled": true, "OtlpEndpoint": "http://otel-collector:4317" } },
    "Eda":           { "DefaultPublisher": "InMemory", "DefaultConsumer": "InMemory" }
  }
}
```

---

## Repository layout

```
fireflyframework-dotnet/
├── docs/                              Long-form documentation (start with docs/README.md)
│   ├── README.md                      Index — recommended reading order
│   ├── ARCHITECTURE.md                Tier diagram, dependency graph, process model
│   ├── SERVICE-SCAFFOLDING.md         Canonical 5-project service layout
│   ├── MIGRATION-GUIDE.md             Java to .NET cookbook
│   ├── CONFIGURATION.md               Every Firefly:* options section
│   └── MODULES.md                     Per-project description with Java mapping
├── src/                               Framework projects, indexed by tier in src/README.md
├── samples/                           Reference services in canonical 5-project layout
│   ├── README.md
│   ├── FireflyFramework.Samples.OrdersService.Interfaces/
│   ├── FireflyFramework.Samples.OrdersService.Models/
│   ├── FireflyFramework.Samples.OrdersService.Core/
│   ├── FireflyFramework.Samples.OrdersService.Web/
│   └── FireflyFramework.Samples.OrdersService.Sdk/
├── tests/                             Cross-tier test suite (see tests/README.md)
│   └── FireflyFramework.Tests/
├── Directory.Build.props              Parent build properties (net10.0, version, metadata)
├── Directory.Build.targets            Cross-project test wiring
├── Directory.Packages.props           Central Package Management — every NuGet pinned
├── FireflyFramework.sln               Solution file
├── NuGet.config                       Pins nuget.org as the only source
├── global.json                        Pins .NET SDK 10.0
├── .envrc                             Sources dotnet (10.x) into PATH
└── LICENSE                            Apache-2.0
```

Each project under `src/` ships its own `README.md` describing its
public surface, options class, and usage examples.

---

## Documentation

[`docs/README.md`](docs/README.md) is the recommended starting point — it indexes the doc set and suggests a reading order.

| Document | Purpose |
|---|---|
| [`docs/INSTALL.md`](docs/INSTALL.md) | How to install packages from NuGet.org, GitHub Packages, or local project references; one-time `nuget.config` + PAT setup |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Tier-by-tier reference, dependency-direction graph, process model, versioning policy |
| [`docs/SERVICE-SCAFFOLDING.md`](docs/SERVICE-SCAFFOLDING.md) | The canonical 5-project layout, naming conventions, dependency graph, bootstrap recipe |
| [`docs/MIGRATION-GUIDE.md`](docs/MIGRATION-GUIDE.md) | Java → .NET cookbook covering Reactor, Spring DI, web layer, persistence, CQRS, EDA, resilience, observability |
| [`docs/CONFIGURATION.md`](docs/CONFIGURATION.md) | Every `Firefly:*` configuration section with example values |
| [`docs/MODULES.md`](docs/MODULES.md) | One-line description of every project plus its Java original |

Per-project READMEs live alongside each project under [`src/`](src) and [`samples/`](samples). [`src/README.md`](src/README.md) indexes them by tier; [`tests/README.md`](tests/README.md) describes the test suite layout.

---

## Versioning

The .NET line uses the same calendar version as the Java line (`26.04.01`
= year 26, month 04, patch 01). When the Java side ships a new release,
`Directory.Build.props`'s `<Version>` is bumped in lockstep so a service
running version *X* on either platform consumes identical contracts.

`Directory.Packages.props` pins every NuGet to a known-good version.
Transitive package floats are not allowed — when an upstream forces a
newer version, the central pin is bumped explicitly.

---

## Releases & publishing

Every `src/*` project is a publishable NuGet package
(`<IsPackable>true</IsPackable>` is the Directory.Build.props default;
samples and tests opt out individually).

### Continuous integration

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs on every
pull request and every push to `main`:

1. Restore + build the whole solution in **Release** configuration.
2. Run the test suite (`dotnet test`).
3. On `main` only, pack every `src/*` project and upload the resulting
   `.nupkg` files as a workflow artifact (preview only — *not* pushed
   to a registry).

### Releasing a version

[`.github/workflows/publish.yml`](.github/workflows/publish.yml)
fires on a published GitHub Release **or** by manual
`workflow_dispatch`. It rebuilds, re-tests, packs every `src/*`
project at the resolved version, and pushes to:

| Target          | Authentication                                     |
|-----------------|----------------------------------------------------|
| **NuGet.org**         | repo secret `NUGET_API_KEY` (skipped with a warning if unset) |
| **GitHub Packages**   | the workflow's `GITHUB_TOKEN` (always present)            |
| **GitHub Release**    | `.nupkg` + `.snupkg` attached as release assets           |

To cut a release:

```bash
# 1. Bump <Version> in Directory.Build.props.
# 2. Commit, tag, push.
git tag v26.04.01
git push origin v26.04.01

# 3. Publish a GitHub Release on the tag — the workflow takes over.
gh release create v26.04.01 --generate-notes
```

The `v` prefix is stripped before the version is fed to `dotnet pack`,
so a tag of `v26.04.01` produces packages versioned `26.04.01`. For
hotfixes or pre-release cuts, run the workflow manually:

```bash
gh workflow run publish.yml -f version=26.04.01-rc.1
```

Both targets use `--skip-duplicate`, so re-running the workflow on the
same version is safe.

---

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md) for build prerequisites, the
"adding a project" recipe, and the .NET conventions the codebase
follows (file-scoped namespaces, naming, sub-module pattern, idiomatic
async shapes).

## License

Apache License 2.0. See [`LICENSE`](LICENSE).
