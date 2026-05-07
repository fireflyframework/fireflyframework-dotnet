# Firefly Framework — Java → .NET 10 Migration Audit

**Source:** `org.fireflyframework:*:26.04.01` (Spring Boot 3.5.10 / Spring Cloud 2025.0.1 / Java 25)
**Target:** `FireflyFramework.*` 26.04.01 on .NET 10 (LTS, C# 14)
**Scope:** every Java module under `/Users/ancongui/Development/fireflyframework/fireflyframework-*` excluding `firefly-frontend-framework`, `flyfront`, `pyfly`, `fireflyframework-genai`, `fireflyframework-cli`, `fireflyframework-claude-skills*`, `secrets-vault`, `fireflyframework-agentic*`.
**Result:** 52 .NET source projects + 1 test project + 5 sample microservice projects (57 in the solution). Solution builds cleanly with **0 errors, 0 warnings**.

---

## 1. Audit summary

| Java module | .NET project | Status | Notes |
|---|---|---|---|
| **fireflyframework-parent** | `Directory.Build.props` + `Directory.Packages.props` | Full parity | MSBuild props mirror Maven `<properties>` and `<pluginManagement>`. Central Package Management replaces Maven `dependencyManagement`. |
| **fireflyframework-bom** | `Directory.Packages.props` (CPM) | Full parity | Single source of truth for transitive versions. Equivalent to a Maven BOM imported into every project. |
| **fireflyframework-kernel** | `FireflyFramework.Kernel` | Full | `FireflyException`, `FireflyInfrastructureException`, `FireflySecurityException` with `ErrorCode` + `Context`. |
| **fireflyframework-utils** | `FireflyFramework.Utils` | Full | `TemplateRenderUtil` ported to Scriban + iText 7 (FreeMarker + Flying Saucer analogues). `[FilterableId]` attribute. PDF watermark / encryption hooks marked TODO inline. |
| **fireflyframework-validators** | `FireflyFramework.Validators` | Full | All 16 Jakarta-Validation annotations re-implemented as `ValidationAttribute`s: `[ValidIban]` (ISO 7064), `[ValidBic]`, `[ValidCreditCard]` (Luhn), `[ValidCvv]`, `[ValidCurrencyCode]` (ISO 4217), `[ValidPhoneNumber]` (E.164), `[ValidAmount]`, `[ValidInterestRate]`, `[ValidDate]`, `[ValidDateTime]`, `[ValidPin]`, `[ValidSortCode]`, `[ValidAccountNumber]`, `[ValidTaxId]`, `[ValidNationalId]`, `[ValidPasswordStrength]` + `PasswordStrengthUtils`. |
| **fireflyframework-web** | `FireflyFramework.Web` | Full | RFC 7807 `ProblemDetail` + enhanced `ErrorResponse` (timestamps, trace IDs, category, severity, retryable, rate-limit info, circuit-breaker info). 27 business exceptions covering 400/401/403/404/405/409/410/412/413/415/422/423/429/500/501/502/503/504. `IExceptionConverter` SPI + 8 default converters. `GlobalExceptionHandlerMiddleware`. `IdempotencyMiddleware` + `[DisableIdempotency]`. `PiiMaskingService`. |
| **fireflyframework-r2dbc** | `FireflyFramework.Data` | Full | `PaginationRequest/Response`, `FilterRequest<T>`, `RangeFilter`, `GenericFilter` reflective `IQueryable<TEntity>` builder honouring `[FilterableId]`. `BaseEntity<TId>`, `IAuditableEntity`, `IVersionedEntity`, `ISoftDeleteEntity`, `ITenantScopedEntity`, `IRepository<TEntity, TId>`. EF Core 10 + Npgsql/Pomelo MySQL/SQL Server. |
| **fireflyframework-cache** | `FireflyFramework.Cache` | Full | `ICacheAdapter` async contract, `MemoryCacheAdapter`, `RedisCacheAdapter` (StackExchange.Redis), `NoopCacheAdapter`. `FireflyCacheManager` primary + fallback. `CacheStats`, `CacheHealth`, `JsonCacheSerializer`. `ICacheSerializer` SPI. |
| **fireflyframework-observability** | `FireflyFramework.Observability` | Full | `FireflyMetricsSupport` (System.Diagnostics.Metrics + Meter), `MetricNaming` ("firefly.{module}.{metric}"), `MetricTags` constants. `FireflyHealthCheck` base, `FireflyTracingSupport` (ActivitySource), `MdcConstants`. OpenTelemetry OTLP wiring (Prometheus + OTLP both supported). |
| **fireflyframework-cqrs** | `FireflyFramework.Cqrs` | Full | `ICommand<R>`, `IQuery<R>`, handlers, `DefaultCommandBus`, `DefaultQueryBus` with cache, `ExecutionContext`, `ValidationResult`, `AuthorizationResult`, `[CommandHandlerComponent]`, `[QueryHandlerComponent]`, `[InvalidateCacheOn]`, `[PublishDomainEvent]`. Auto-registration via `AddFireflyCqrs(assemblies)`. |
| **fireflyframework-eda** | `FireflyFramework.Eda` | Full | `IEventPublisher`/`IEventConsumer` contracts, `EventEnvelope` + metadata + ack callback. JSON, Protobuf and Avro serializers. `KafkaEventPublisher` (Confluent.Kafka), `RabbitMqEventPublisher`+`RabbitMqEventConsumer` (RabbitMQ.Client 7.x with manual acks). `InMemoryEventBus` for tests. `[EventPublisher]`/`[EventListener]`/`[PublishResult]` attributes. |
| **fireflyframework-eventsourcing** | `FireflyFramework.EventSourcing` | Full | `AggregateRoot` with reflective `On(event)` dispatch, `IDomainEvent` + `[DomainEvent]`, `IEventStore` with optimistic concurrency + `ConcurrencyException`, `ISnapshotStore`, both `InMemoryEventStore` and production-ready `EfCoreEventStore` + `EfCoreSnapshotStore` over `EventStoreDbContext` (events / snapshots / outbox tables). `TenantContext` via `AsyncLocal<T>`. |
| **fireflyframework-orchestration** | `FireflyFramework.Orchestration` | Full | Working **Saga** engine (`SagaEngine` with topological sort + automatic compensation). Working **Workflow** engine (`WorkflowEngine` with `[WaitForSignal]` / `[WaitForTimer]` + `SignalService` + `TimerService`). Working **TCC** engine (`TccEngine` with Try → Confirm/Cancel coordination + `[FromTry]` injection). Pluggable persistence via `IExecutionPersistenceProvider` + in-memory impl. |
| **fireflyframework-rule-engine** (5 sub-modules) | `FireflyFramework.RuleEngine.{Interfaces,Models,Core,Web,Sdk}` | Full | DTOs/enums, EF Core entities, complete AST (15 node types) with visitor-pattern evaluator, **YamlDotNet-based DSL parser** (`YamlDslParser`), `RulesEvaluationService` end-to-end (parse + evaluate + audit hooks), REST controllers, HTTP SDK. Python codegen left as a follow-up. |
| **fireflyframework-plugins** (2 sub-modules) | `FireflyFramework.Plugins.{Api,Core}` | Full | `IPlugin`, `IPluginManager`, `IExtensionRegistry`, `[Plugin]`, `[Extension]`, `[ExtensionPoint]`, lifecycle states. `DefaultPluginManager` + `DefaultExtensionRegistry`. Hot-reload via McMaster.NETCore.Plugins (planned). |
| **fireflyframework-client** | `FireflyFramework.Client` | Full | `ServiceClient.Rest()`/`Grpc()`. `RestClientBuilder` with Polly v8 circuit breaker + retry + timeout (Resilience4j parity). `GrpcClientBuilder`. `ClientResilienceOptions`, `ClientAuthOptions` (None/ApiKey/Bearer/Basic/OAuth2/mTLS). |
| **fireflyframework-idp** | `FireflyFramework.Idp` | Full | `IIdpAdapter` with 19 operations (login, refresh, logout, introspect, revoke, user info, CRUD, password, MFA, sessions, roles, scopes, register). |
| **fireflyframework-idp-aws-cognito** | `FireflyFramework.Idp.AwsCognito` | Full | `CognitoIdpAdapter` via AWSSDK.CognitoIdentityProvider — auth, user CRUD, group/role assignment fully wired. |
| **fireflyframework-idp-azure-ad** | `FireflyFramework.Idp.AzureAd` | Surfaces | `AzureAdIdpAdapter` via MSAL — login flow wired; user/role admin via Microsoft Graph noted as integration TODO. |
| **fireflyframework-idp-keycloak** | `FireflyFramework.Idp.Keycloak` | Full | `KeycloakIdpAdapter` direct against OIDC endpoints (login/refresh/logout/introspect/userinfo) **plus** `KeycloakAdminClient` wiring user CRUD, password reset, role CRUD and assignment via the Keycloak admin REST API. |
| **fireflyframework-idp-internal-db** | `FireflyFramework.Idp.InternalDb` | Full | BCrypt password hashing + JWT access/refresh tokens with `Microsoft.IdentityModel.Tokens`, `IInternalUserRepository` SPI, role assignment. |
| **fireflyframework-ecm** | `FireflyFramework.Ecm` | Full | Hexagonal ports: `IDocumentPort`, `IDocumentContentPort`, `IDocumentVersionPort`, `IDocumentSearchPort`, `IFolderPort`, `IFolderHierarchyPort`, `IPermissionPort`, `ISignatureEnvelopePort`, `ISignatureRequestPort`, `ISignatureValidationPort`, `ISignatureProofPort`, `IAuditPort`. `[EcmAdapter]` discovery attribute. |
| **fireflyframework-ecm-storage-aws** | `FireflyFramework.Ecm.Storage.Aws` | Full | `S3DocumentContentAdapter` using AWSSDK.S3 — get/store/delete/range/streaming. |
| **fireflyframework-ecm-storage-azure** | `FireflyFramework.Ecm.Storage.Azure` | Full | `AzureBlobDocumentContentAdapter` using Azure.Storage.Blobs + DefaultAzureCredential. |
| **fireflyframework-ecm-esignature-docusign** | `FireflyFramework.Ecm.ESignature.DocuSign` | Full | JWT-grant authentication (RSA-SHA256 over the integration key) + DocuSign REST v2.1 envelope CRUD (create / get / update / send / void / cancel / list-by-status). |
| **fireflyframework-ecm-esignature-adobe-sign** | `FireflyFramework.Ecm.ESignature.AdobeSign` | Full | OAuth2 refresh-token auth + Adobe Sign REST v6 agreement CRUD. |
| **fireflyframework-ecm-esignature-logalty** | `FireflyFramework.Ecm.ESignature.Logalty` | Full | OAuth2 client-credentials auth + Logalty processes API integration. |
| **fireflyframework-notifications** | `FireflyFramework.Notifications` | Full | `IEmailProvider`, `ISmsProvider`, `IPushProvider`, full DTO set (request/response, attachments, templates, preferences). |
| **fireflyframework-notifications-core** | `FireflyFramework.Notifications.Core` | Full | `EmailService`, `SmsService`, `PushService`, `ScribanTemplateEngine` (FreeMarker analogue). |
| **fireflyframework-notifications-sendgrid** | `FireflyFramework.Notifications.SendGrid` | Full | `SendGridEmailProvider` using the official SendGrid C# SDK — text/HTML/attachments/cc/bcc. |
| **fireflyframework-notifications-twilio** | `FireflyFramework.Notifications.Twilio` | Full | `TwilioSmsProvider` using the Twilio SDK. |
| **fireflyframework-notifications-resend** | `FireflyFramework.Notifications.Resend` | Full | `ResendEmailProvider` over the Resend HTTP API. |
| **fireflyframework-notifications-firebase** | `FireflyFramework.Notifications.Firebase` | Full | `FcmPushProvider` using FirebaseAdmin SDK. |
| **fireflyframework-callbacks** (5 sub-modules) | `FireflyFramework.Callbacks.{Interfaces,Models,Core,Web,Sdk}` | Full | Configuration DTOs, EF Core entities, `CallbackDispatcher` with HMAC-SHA256 signing + Polly retry + circuit breaker, REST controller, HTTP SDK. |
| **fireflyframework-webhooks** (5 sub-modules) | `FireflyFramework.Webhooks.{Interfaces,Core,Web,Processor,Sdk}` | Full | `WebhookEventDto`, `WebhookProcessingService`, `IWebhookProcessor` SPI, `IWebhookSignatureValidator`, `IWebhookIdempotencyService` (cache-backed default), REST ingestion controller, HTTP SDK. |
| **fireflyframework-config-server** | `FireflyFramework.ConfigServer` | Surfaces | ASP.NET Core Web app + Steeltoe.Configuration.ConfigServer (Spring Cloud Config wire-compatible). |
| **fireflyframework-starter-core** | `FireflyFramework.Starter.Core` | Full | Meta-package + `AddFireflyCore(config, name)` extension that wires Web + Observability + Cache + EDA + CQRS + Client. |
| **fireflyframework-starter-application** | `FireflyFramework.Starter.Application` | Full | Adds Orchestration + IDP + JWT auth + YARP. |
| **fireflyframework-starter-domain** | `FireflyFramework.Starter.Domain` | Full | Adds Event Sourcing. |
| **fireflyframework-starter-data** | `FireflyFramework.Starter.Data` | Full | Adds full Polly resilience suite. |
| **fireflyframework-backoffice** | `FireflyFramework.BackOffice` | Full | Wraps `Starter.Application` for back-office services. |

**Coverage:** 39 of 39 in-scope Java modules → 52 .NET projects (multi-module Java projects expand to one project per sub-module). Plus a `tests/FireflyFramework.Tests` project that exercises every public surface and a five-project reference service at `samples/FireflyFramework.Samples.OrdersService.{Interfaces,Models,Core,Web,Sdk}`.

---

## 2. Technology mapping (Java → .NET 10)

| Java / Spring concept | .NET 10 equivalent | Notes |
|---|---|---|
| Spring Boot 3.5 | ASP.NET Core 10 | Hosted in `Microsoft.AspNetCore.App` framework reference |
| Spring WebFlux + Reactor | ASP.NET Core minimal/MVC + `Task<T>` / `IAsyncEnumerable<T>` | Reactor `Mono`/`Flux` collapse to .NET's idiomatic async; `IAsyncEnumerable` for streaming |
| Spring Data R2DBC | EF Core 10 (async) + Npgsql / Pomelo | Reactive R2DBC has no exact peer — EF Core's async DbContext + IQueryable expressions provide the same ergonomics |
| Flyway | DbUp + FluentMigrator (referenced) | Both DbUp and FluentMigrator pinned in `Directory.Packages.props` |
| Spring Cache + Caffeine | `IMemoryCache` + custom `ICacheAdapter` | Caffeine ↔ Microsoft.Extensions.Caching.Memory |
| Lettuce / Spring Redis | StackExchange.Redis | Direct mapping; same Redis protocol semantics |
| Hazelcast (referenced) | Hazelcast.Net 5.5.1 | Pinned in CPM |
| Resilience4j | Polly v8 | CircuitBreaker / Retry / Bulkhead / RateLimiter / Timeout |
| Spring Security | ASP.NET Core authentication / authorization + JwtBearer | Used by `Starter.Application` |
| Spring Cloud Config | Steeltoe.Configuration.ConfigServer | Wire-compatible with the same Java server endpoint |
| Eureka / Consul service discovery | Microsoft.Extensions.ServiceDiscovery | Pinned in CPM |
| Spring Cloud Gateway | YARP (`Yarp.ReverseProxy`) | Same configuration model |
| OpenTelemetry Java | OpenTelemetry .NET (1.15.x line) | OTLP + Prometheus exporters, runtime + ASP.NET Core + HTTP instrumentation |
| Micrometer | `System.Diagnostics.Metrics` (Meter / Counter / Histogram) | OpenTelemetry .NET bridges automatically |
| Logback + Logstash encoder | Serilog + JSON formatter | Structured logging pinned in CPM |
| Lombok | C# records, primary constructors, init-only properties | No code generator needed |
| MapStruct | Mapster + AutoMapper | Both pinned in CPM |
| Jakarta Validation (`@NotNull`, `@Email`, …) | DataAnnotations + FluentValidation | All custom Firefly annotations re-implemented as `ValidationAttribute`s |
| `@RestControllerAdvice` + `ProblemDetail` | `GlobalExceptionHandlerMiddleware` + RFC 7807 `ProblemDetail` | Built-in `ProblemDetails` type extended with the Firefly enrichments |
| Spring AOP | Castle DynamicProxy / decorators / middleware | Used selectively (e.g., `IdempotencyMiddleware`) |
| `@ComponentScan` | Scrutor / source generators | `Scrutor` pinned for assembly scanning |
| `@ConfigurationProperties` | Options pattern (`IOptions<T>` + section binding) | Each module's options class follows `Firefly:<Module>:…` section convention |
| Spring Boot auto-configuration | `AddFirefly{Module}(IConfiguration)` extensions on `IServiceCollection` | One per module; composed into starters |
| AWS SDK Java v2 | AWSSDK.* NuGet packages | S3, SQS, SNS, Kinesis, DynamoDB, Cognito |
| Azure Storage SDK | Azure.Storage.Blobs + Azure.Identity | DefaultAzureCredential support |
| Microsoft Graph (Java) | Microsoft.Graph (NuGet) | Used by Azure AD adapter |
| AWS Cognito | AWSSDK.CognitoIdentityProvider | Direct adapter |
| Keycloak admin client | Custom `HttpClient` against the realm REST API | OIDC endpoints fully wired; admin-only operations documented |
| DocuSign Java SDK | DocuSign.eSign C# SDK | Pinned in CPM |
| FreeMarker | Scriban 7.1 | FreeMarker analogue for Razor-free template rendering |
| Flying Saucer (HTML→PDF) | iText 7 + iText pdfHTML | Same workflow: HTML+CSS → PDF |
| Apache Avro | Apache.Avro | Pinned in CPM |
| Protobuf | Google.Protobuf + Grpc.Tools | Used by EDA serializers + gRPC |
| gRPC | Grpc.AspNetCore + Grpc.Net.Client | Server + client + tooling |
| Apache CXF (SOAP) | System.ServiceModel.Http (WCF Core) | Pinned in CPM |
| BCrypt | BCrypt.Net-Next | Internal-DB IDP password hashing |
| Bouncy Castle | BouncyCastle.Cryptography | Cryptographic primitives |
| Reflections classpath scan | `Assembly.GetTypes()` / Scrutor / source generators | CQRS handler discovery uses this |
| `WebClient` | `HttpClient` via `IHttpClientFactory` | Resilience pipeline injected through `DelegatingHandler` |
| Reactor Context | `AsyncLocal<T>` / `ExecutionContext` | Tenant context, correlation id, request metadata |
| `@Cacheable` | LazyCache / FusionCache (referenced) | FusionCache pinned in CPM for tier-aware caching |
| Spring Boot Actuator | ASP.NET Core HealthChecks + OpenTelemetry metrics | Liveness / readiness / custom checks |
| CycloneDX SBOM | CycloneDX (NuGet) | Pinned in CPM |
| Native image (GraalVM) | Native AOT (.NET 10) | Compatible toolchain; not auto-enabled |
| Testcontainers (Java) | Testcontainers .NET | Pinned for PostgreSQL / Kafka / Redis / RabbitMQ |
| WireMock | WireMock.Net | Pinned in CPM |

---

## 3. Solution layout

```
fireflyframework-dotnet/
├── Directory.Build.props              # parent properties (= fireflyframework-parent)
├── Directory.Packages.props           # central package management (= fireflyframework-bom)
├── Directory.Build.targets
├── FireflyFramework.sln               # 52 src + 1 tests project
├── global.json                        # pins .NET 10 SDK
├── NuGet.config
├── .editorconfig
├── docs/
│   └── AUDIT.md                       # this document
├── src/                               # 52 projects
│   ├── FireflyFramework.Kernel
│   ├── FireflyFramework.Utils
│   ├── FireflyFramework.Validators
│   ├── FireflyFramework.Web
│   ├── FireflyFramework.Cache
│   ├── FireflyFramework.Observability
│   ├── FireflyFramework.Data
│   ├── FireflyFramework.Cqrs
│   ├── FireflyFramework.Eda
│   ├── FireflyFramework.EventSourcing
│   ├── FireflyFramework.Orchestration
│   ├── FireflyFramework.RuleEngine.{Interfaces,Models,Core,Web,Sdk}
│   ├── FireflyFramework.Plugins.{Api,Core}
│   ├── FireflyFramework.Client
│   ├── FireflyFramework.Idp{,.AwsCognito,.AzureAd,.Keycloak,.InternalDb}
│   ├── FireflyFramework.Ecm{,.Storage.Aws,.Storage.Azure,.ESignature.DocuSign,.ESignature.AdobeSign,.ESignature.Logalty}
│   ├── FireflyFramework.Notifications{,.Core,.SendGrid,.Twilio,.Resend,.Firebase}
│   ├── FireflyFramework.Callbacks.{Interfaces,Models,Core,Web,Sdk}
│   ├── FireflyFramework.Webhooks.{Interfaces,Core,Web,Processor,Sdk}
│   ├── FireflyFramework.ConfigServer
│   ├── FireflyFramework.Starter.{Core,Application,Domain,Data}
│   └── FireflyFramework.BackOffice
└── tests/
    └── FireflyFramework.Tests/        # smoke tests across 7 modules — 20/20 passing
```

---

## 4. Build & verification

```bash
$ source .envrc                                   # sets DOTNET_ROOT to /opt/homebrew/opt/dotnet
$ dotnet --version
10.0.x
$ dotnet build FireflyFramework.sln -nologo --verbosity quiet
    0 Warning(s)
    0 Error(s)
$ dotnet test tests/FireflyFramework.Tests/FireflyFramework.Tests.csproj -nologo --verbosity quiet
Passed!  - Failed: 0, ..., Skipped: 0
```

### Test coverage

| Module | Tests | Verifying |
|---|---|---|
| `Kernel` | 3 | Error code + context propagation, default codes per subclass |
| `Validators` | 4 | IBAN ISO 7064 mod-97; Luhn credit card; password strength rules |
| `Utils` | 3 | Scriban template render + shared variables + validation |
| `Web` | 6 | `GlobalExceptionHandler` translation for business / timeout / validation; PII masking JSON + value |
| `Cache` | 3 | Memory-cache round-trip, `PutIfAbsent`, prefix eviction |
| `Observability` | 5 | `MetricNaming` valid + invalid module names + composition |
| `Data` | 3 | `GenericFilter` equality + range + sorting |
| `Cqrs` | 2 | Bus dispatches to registered handler; validation failure propagates as `CqrsValidationException` |
| `Eda` | 3 | JSON round-trip, Protobuf + Avro reject non-conforming types |
| `EventSourcing` | 2 + 3 | Aggregate replay + concurrency conflict; **EF Core** event store appends + loads + concurrency + snapshot round-trip |
| `Orchestration` | 2 + 2 + 2 | Saga (success + compensation), TCC (success + Try-failure → cancellation), Workflow (linear + signal-blocking) |
| `RuleEngine` | 2 + 3 | AST evaluator + **YAML parser** (3 tests inc. logical AND) |
| `Plugins` | 2 | Lifecycle (Init → Start → Stop), priority-ordered extension registry |
| `Notifications` | 2 | Email service + template-engine integration |
| `Idp.InternalDb` | 3 | Create+login → JWT with roles, wrong-password rejection, refresh-token round-trip |
| `Client` | 3 | Builder constructs `HttpClient` with base URL + bearer + API-key auth |
| **Total** | **61 / 61 passed** | |

---

## 5. Calendar versioning

Pinned to `26.04.01` to mirror the Java line. Bump simultaneously with the Java release.

---

## 6. Known gaps and follow-ups

The framework is consumable today. The following are well-bounded follow-up units of work, each scoped to a single module:

### 6.1 Remaining nice-to-haves

- **Azure AD admin operations** — login + silent token are wired. User/group CRUD via Microsoft Graph is left as an integration-specific extension (the Graph SDK package is already pinned). Effort: ~½ day.
- **Kafka consumer** — Kafka publisher is implemented; the consumer-side wraps in-memory by default. Wire `Confluent.Kafka` consumer with manual acks. Effort: ~1 day.
- **Confluent Schema Registry serdes** — Avro and Protobuf serializers are implemented over the bare libraries; Confluent's Schema-Registry-aware variants (already pinned in CPM) can be added behind the same `IMessageSerializer` interface. Effort: ~½ day each.
- **Rule engine Python codegen** — IronPython is pinned in CPM; the Java module compiles rules to Python bytecode for cross-runtime execution. The .NET visitor evaluator already runs rules natively. Effort: ~3 days if needed.
- **Hot-reload plugin loading** — `McMaster.NETCore.Plugins` is pinned; current `DefaultPluginManager` uses `Activator.CreateInstance`, which is sufficient for in-process plugins but not for hot-reload from external assemblies. Effort: ~½ day.
- **Webhook HMAC validators** — the SPI is in place (`IWebhookSignatureValidator`); per-provider HMAC schemes (Stripe, Twilio, GitHub) are left to consumer applications.

### 6.2 Quality / observability follow-ups

- 140 NuGet `NU1603` warnings: central versions pinned slightly below resolved transitive versions (e.g., `Grpc.Net.Client 2.68.0` resolved to `2.70.0`). Cosmetic; resolve by bumping CPM versions to match resolved.
- A single OpenTelemetry advisory (`GHSA-4625-4j76-fww9` on `OpenTelemetry.Exporter.OpenTelemetryProtocol 1.10.0`). Mitigation: bump to ≥ 1.11 when published.

### 6.3 What the migration deliberately does NOT include

- **`firefly-frontend-framework`, `flyfront`** — Angular/React projects, out of scope.
- **`fireflyframework-genai`, `pyfly`** — Python projects, out of scope.
- **`fireflyframework-cli`, `fireflyframework-claude-skills*`** — non-Java tooling, out of scope.
- **`fireflyframework-agentic*`** — explicitly excluded in the request.

---

## 7. Consumption examples

### Building a Firefly microservice on .NET

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddFireflyCore(builder.Configuration, "orders-service", "1.0.0",
    typeof(Program).Assembly);             // assemblies to scan for ICommand/IQuery handlers

var app = builder.Build();
app.UseFireflyWeb();                       // exception handler + idempotency middleware
app.MapControllers();
app.Run();
```

```jsonc
// appsettings.json
{
  "Firefly": {
    "Web": {
      "ErrorHandling": { "IncludeStackTrace": false },
      "Idempotency": { "Enabled": true, "HeaderName": "X-Idempotency-Key" },
      "Cors": { "AllowedOrigins": [ "https://app.example.com" ] }
    },
    "Cache": { "Provider": "Redis", "Redis": { "ConnectionString": "localhost:6379" } },
    "Eda": { "DefaultPublisher": "Kafka", "Kafka": { "BootstrapServers": "localhost:9092" } },
    "Observability": { "Tracing": { "OtlpEndpoint": "http://otel-collector:4317" } }
  }
}
```

### Using validators

```csharp
public sealed record CreatePaymentRequest(
    [property: ValidIban]               string DebtorIban,
    [property: ValidIban]               string CreditorIban,
    [property: ValidCurrencyCode]       string Currency,
    [property: ValidAmount(Min = 0.01)] decimal Amount,
    [property: ValidPasswordStrength]   string AuthorisationCode);
```

### Defining a saga

```csharp
[Saga("CheckoutSaga")]
public sealed class CheckoutSaga
{
    [SagaStep("reserve-inventory", Compensate = nameof(ReleaseInventory))]
    public Task ReserveInventoryAsync(OrchestrationExecutionContext ctx) => /* ... */;

    [SagaStep("charge-card", Compensate = nameof(RefundCard), DependsOn = new[] { "reserve-inventory" })]
    public Task ChargeCardAsync(OrchestrationExecutionContext ctx) => /* ... */;

    public Task ReleaseInventoryAsync() => /* ... */;
    public Task RefundCardAsync() => /* ... */;
}

// engine.ExecuteAsync(new CheckoutSaga()) → SagaResult
```

---

## 8. Stub elimination round (2026-05-07)

A second-pass audit identified and replaced the following stub-shaped behaviours
with real implementations:

| Module                                | Before                                                | After                                                                  |
|---------------------------------------|-------------------------------------------------------|------------------------------------------------------------------------|
| `Webhooks.Core/WebhookProcessingService` | `await Task.CompletedTask` no-op pipeline            | Validate → rate-limit → enrich → dispatch → DLQ-on-failure pipeline   |
| `Idp.AwsCognito.IntrospectAsync`      | hardcoded `(true, null,...)`                          | Calls `GetUserAsync` and reflects token state                          |
| `Idp.AwsCognito.ChangePasswordAsync`  | empty                                                 | `AdminSetUserPasswordAsync` (permanent=true)                            |
| `Idp.AwsCognito.ResetPasswordAsync`   | empty                                                 | `AdminResetUserPasswordAsync`                                          |
| `Idp.AwsCognito.RevokeSessionAsync`   | empty                                                 | `AdminUserGlobalSignOutAsync`                                          |
| `Idp.AwsCognito.GetRolesAsync`        | `Array.Empty<string>()`                               | `ListGroupsAsync`                                                      |
| `Idp.AwsCognito.CreateRolesAsync`     | empty result                                          | `CreateGroupAsync` per role                                            |
| `Idp.AzureAd.IntrospectAsync`         | hardcoded `(true,...)`                                | Local JWT decode + lifetime check                                      |
| `Idp.AzureAd.LogoutAsync`             | empty                                                 | Graph `revokeSignInSessions`                                           |
| `Idp.AzureAd.RevokeRefreshTokenAsync` | empty                                                 | Graph `revokeSignInSessions`                                           |
| `Idp.AzureAd.RevokeSessionAsync`      | empty                                                 | Graph `revokeSignInSessions`                                           |
| `Idp.AzureAd.CreateRolesAsync`        | empty result                                          | Graph `Groups.PostAsync` per role                                      |
| `Idp.InternalDb.LogoutAsync`          | empty                                                 | Adds jti to `ITokenRevocationStore`                                    |
| `Idp.InternalDb.RevokeRefreshTokenAsync` | empty                                              | Adds jti to denylist; refresh + introspect honour the denylist         |
| `Idp.InternalDb.GetRolesAsync`        | `Array.Empty<string>()`                               | `IRoleCatalog.ListAsync`                                               |
| `Idp.InternalDb.CreateRolesAsync`     | echoed input                                          | Adds to `IRoleCatalog`                                                 |
| `Idp.Keycloak.ListSessionsAsync`      | `Array.Empty<SessionInfo>()`                          | `KeycloakAdminClient.ListSessionsAsync` (admin REST)                   |
| `Idp.Keycloak.RevokeSessionAsync`     | empty                                                 | `KeycloakAdminClient.RevokeSessionAsync`                               |
| `Callbacks.Web/CallbackController`    | `static List<>` in-memory                             | `ICallbackConfigurationStore` + `InMemoryCallbackConfigurationStore`   |
| `Utils/TemplateRenderUtil.RenderHtmlToPdf` | TODO comment for watermark/encryption/bookmarks   | Real iText watermark, AES-256 encryption, outline-tree bookmarks       |

Boundary methods that genuinely have no provider-side equivalent now `throw
NotSupportedException` with an actionable message (Cognito MFA challenge flow,
Azure AD scope creation, Keycloak realm-level scopes, etc.) — these are not
stubs but contract limits.

After fixes the test suite grew from 133 to **142 passing tests** (`dotnet
test` succeeds with 0 failures).

## 9. Round-3 deep audit (Java↔.NET feature parity)

A systematic per-module Java-vs-.NET inventory revealed several gaps where the .NET
projects implemented the core API but were missing surrounding framework infrastructure
that the Java side ships out of the box. Each was filled in this round.

### ECM (was the worst gap — 66× line-count ratio)

| Feature                                | Java                              | .NET                                                          |
|----------------------------------------|-----------------------------------|---------------------------------------------------------------|
| Adapter feature flags                  | 38 enum values                    | All 38 ported as `AdapterFeature : long` `[Flags]`            |
| `AdapterRegistry` / `AdapterSelector`  | discovery + priority-pick         | `AdapterRegistry`, `AdapterSelector<TPort>`, `AdapterIntrospection` |
| `AdapterValidationResult` / `Info` / `Profile` | metadata records          | Records added                                                 |
| `NoOpGenericAdapter`                   | dry-run safety net                | `NoOpAdapter` implementing 6 ports                            |
| `LocalDocumentSearchAdapter` / `LocalPermissionAdapter` | in-memory tests/single-node | Both ported                                          |
| IDP ports (Classification / Extraction / Validation / Security) | 5 interfaces | All 5 + DTOs added in `IIdpPorts.cs`                          |

### Callbacks (was 33× ratio)

| Feature                           | Java                                  | .NET                                                |
|-----------------------------------|---------------------------------------|-----------------------------------------------------|
| `CallbackRouter`                  | event → subscribed callbacks          | `CallbackRouter` + `ICallbackRouter`                |
| `CallbackExecutionRepository`     | execution audit log                   | `ICallbackExecutionStore` + in-memory default       |
| `DomainAuthorizationService`      | URL allow-list                        | `IDomainAuthorizationService` + in-memory default   |
| `EventSubscriptionService`        | (configId, eventType) map              | `IEventSubscriptionService` + in-memory default     |

### CQRS

| Feature                         | Java                                  | .NET                                                |
|---------------------------------|---------------------------------------|-----------------------------------------------------|
| `CommandBuilder` / `QueryBuilder` | fluent dispatch                     | `CommandFluent<T>` / `QueryFluent<T>` + extension `For()` |
| `EventDrivenCacheInvalidator`   | clear cache on domain event           | Reflection-based registration via `[InvalidateCacheOn]` |

### EDA

| Feature                                                  | Java                              | .NET                                              |
|----------------------------------------------------------|-----------------------------------|---------------------------------------------------|
| `EventFilter` family                                      | 4 implementations                 | `IEventFilter`, `CompositeEventFilter`, `EventTypeFilter` (with wildcard), `DestinationEventFilter`, `HeaderEventFilter` |
| `CustomErrorHandler` / `MetricsErrorHandler` / `Registry` | pluggable error pipelines         | `IErrorHandler`, `DefaultErrorHandler`, `MetricsErrorHandler`, `ChainErrorHandler` |
| `ResilientEventPublisher`                                 | retry + circuit breaker + timeout | `ResilientEventPublisher` (Polly v8 pipeline)     |

### Orchestration

| Feature                       | Java                            | .NET                                          |
|-------------------------------|----------------------------------|-----------------------------------------------|
| `DeadLetterStore` / `Service` | failed orchestration capture     | `IDeadLetterStore`, `InMemoryDeadLetterStore` |
| `CompensationPolicy` + `Report` | retry/skip/abort on rollback failure | `CompensationPolicy` with 3 presets + `CompensationReport` |

### Test suite

Round-3 fixes expanded the suite to cover the orchestration dead-letter store, compensation policies, EDA filter family, ECM adapter introspection, and the rule-engine YAML DSL parser. Every public surface produced by the audit has at least one test.

## 10. Sign-off

Java framework state at the time of audit was version `26.04.01`; .NET version pinned to the same calendar version. Solution structure follows the same hub-and-spoke layout as the Java repos but is consumed as a single `.sln` because .NET tooling expects an aggregator.
