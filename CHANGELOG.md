# Changelog

All notable changes to FireflyFramework.NET.

## [26.04.01] — 2026-05-07

### Added — initial .NET 10 port from `org.fireflyframework:*:26.04.01`

#### Foundational layer
- **Kernel** — `OperationResult<T>`, `ProblemDetail`, `IClock`, `FireflyException`
- **Utils** — Slug / Crypto / IO helpers, `Try.Of`, `RetryUtils`, Scriban template engine + iText 7 PDF rendering with watermark, AES-256 encryption and bookmark support
- **Validators** — IBAN, BIC, Luhn, VAT, phone (E.164), e-mail, password strength, sort code, account number, CVV, PIN, credit-card, currency code, national ID, tax ID, date / datetime / amount / interest-rate validators (16 total)
- **Web** — RFC 7807 problem-details middleware, correlation-id propagation, idempotency middleware, PII masking service, exception converter pipeline, 27 business exception types

#### Platform
- **Cache** — `ICacheAdapter` port + Memory / Redis / Noop adapters + `FireflyCacheManager` (primary + fallback)
- **Observability** — OpenTelemetry .NET (tracing / metrics / logs), Serilog enrichers
- **Data** — EF Core 10 with InMemory + Postgres + SqlServer providers, generic filter DSL, pagination types, base-entity / soft-delete contracts
- **CQRS** — Command + Query buses with handler discovery, fluent `For()` API, validation, authorization, query result caching, `EventDrivenCacheInvalidator`
- **EDA** — Kafka publisher / consumer (with manual offset commit + Schema Registry Avro & Protobuf), RabbitMQ publisher / consumer, in-memory bus, event filter family (composite / type / destination / header), pluggable error handlers, `ResilientEventPublisher` with Polly pipeline
- **EventSourcing** — `AggregateRoot` with optimistic concurrency, `IEventStore` (in-memory + EF Core), snapshots, transactional outbox + `EventOutboxProcessor`, projections + `ProjectionRunner`, event upcasting
- **Orchestration** — Saga (DAG + compensation), Workflow (signals + timers), TCC (Try / Confirm / Cancel) engines, dead-letter store, compensation policies (Abort / Skip / Retry / DLQ)
- **RuleEngine** — Full AST + visitor evaluator + YAML DSL parser, web admin
- **Plugins** — `IPlugin` SPI, `DefaultPluginManager`, `IExtensionRegistry`, McMaster-based hot-reload `AssemblyPluginLoader`

#### Adapters
- **Client** — REST builder (`HttpRestClient`), SOAP `ChannelFactory<T>`, WebSocket helper, gRPC channel builder; Polly v8 resilience pipelines
- **IDP** — Keycloak (admin REST), Azure AD (Microsoft Graph + MSAL), AWS Cognito (full admin surface), InternalDb (BCrypt + stateless JWT + revocation store + role catalog)
- **ECM** — Adapter framework (`AdapterRegistry`, `AdapterSelector<TPort>`, `AdapterIntrospection`, 38 `AdapterFeature` flags), document / folder / version / search / signature / IDP ports, NoOp + Local adapters; storage on S3 / Azure Blob; e-signature on DocuSign (JWT grant) / Adobe Sign (OAuth2 refresh) / Logalty (OAuth2 client-credentials)
- **Notifications** — Dispatcher with per-user channel preferences; SendGrid (e-mail), Twilio (SMS), Resend (e-mail), Firebase (push)
- **Callbacks** — Configuration store, dispatcher (HMAC + Polly retry), router, execution audit log, domain authorization, event subscription service, REST controller
- **Webhooks** — Stripe / GitHub / Twilio / generic-HMAC signature validators, processing pipeline (validate → rate-limit → enrich → dispatch → DLQ), DLQ + redrive, compression, batching, metadata enrichment
- **ConfigServer** — Spring-Cloud-Config-compatible REST API with file-system property sources

#### Starters
- `Starter.Core` (Web + Cache + Observability + EDA + CQRS)
- `Starter.Application` (+ Plugins + IDP + Orchestration)
- `Starter.Domain` (+ EventSourcing + in-memory event store)
- `Starter.Data` (+ EF Core + Polly)
- `BackOffice` (Application + context resolver / middleware / security context)

#### Tooling
- `Directory.Packages.props` — Central Package Management (Maven BoM analogue) pinning every NuGet
- `Directory.Build.props` — Parent properties (`net10.0`, calendar version `26.04.01`)
- xUnit test project with **157 passing tests** across every tier
- Sample microservice in the canonical five-project layout
  (`samples/FireflyFramework.Samples.OrdersService.{Interfaces,Models,Core,Web,Sdk}`)
  mirroring the multi-module Maven structure used by every Java service
  in the Firefly platform
- Apache-2.0 LICENSE, comprehensive `.gitattributes` and `.gitignore`

#### Documentation
- `docs/ARCHITECTURE.md` — tier-by-tier reference + dependency graph
- `docs/SERVICE-SCAFFOLDING.md` — canonical five-project service layout
- `docs/MIGRATION-GUIDE.md` — Java → .NET cookbook
- `docs/CONFIGURATION.md` — every `Firefly:*` settings section
- `docs/MODULES.md` — per-project description
- `docs/AUDIT.md` — Java↔.NET feature parity audit (3 rounds)
- Per-module README files (52 framework + 5 sample modules)

### Notes
- Calendar version pinned to the Java release line (`26.04.01`).
- Targets `net10.0` (LTS). Built and tested with .NET SDK `10.0.107`.
  Language version `latest` (C# 14).
- `System.Net.Http.Json` is provided by the .NET 10 framework reference — no
  package import needed.
- No stubs: every public method either has a real implementation or throws
  `NotSupportedException` with an actionable message documenting why the
  underlying provider does not support it.
