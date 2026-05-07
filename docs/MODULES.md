# Module Catalogue

> One-line description of every project in `src/` plus its Java original.
> Cross-reference for [ARCHITECTURE.md](ARCHITECTURE.md) and
> [MIGRATION-GUIDE.md](MIGRATION-GUIDE.md).

## 01 — Foundational

| .NET project                                 | Java original                           | Purpose                                                                       |
|----------------------------------------------|-----------------------------------------|-------------------------------------------------------------------------------|
| `FireflyFramework.Kernel`                    | `firefly-common`                        | RFC 7807 `ProblemDetail`, `OperationResult<T>`, `IClock`, `FireflyException`  |
| `FireflyFramework.Utils`                     | `firefly-common-utils`                  | `Try.Of`, `RetryUtils`, Slug / Crypto / IO helpers, Scriban + iText 7 templating |
| `FireflyFramework.Validators`                | `firefly-common-validators`             | 16 `[Valid…]` attributes — IBAN, BIC, credit card, currency, phone, password strength, etc. |
| `FireflyFramework.Web`                       | `firefly-web` + `firefly-spring-utils`  | Problem-Details middleware, correlation-id, `IdempotencyMiddleware`, PII masking |

## 02 — Platform

| .NET project                                 | Java / Spring original                                   | Purpose                                                                |
|----------------------------------------------|----------------------------------------------------------|------------------------------------------------------------------------|
| `FireflyFramework.Cache`                     | `firefly-common-cache`                                   | `ICacheAdapter` port + Memory / Redis / NoOp adapters                  |
| `FireflyFramework.Observability`             | `firefly-otel-spring-boot-starter`                       | OpenTelemetry .NET tracing / metrics / logs configuration              |
| `FireflyFramework.Data`                      | `firefly-common-data` (R2DBC)                            | EF Core 10 + filter / pagination / repository helpers                  |
| `FireflyFramework.Cqrs`                      | `firefly-common-cqrs`                                    | Command / query bus + handler discovery + query caching                |
| `FireflyFramework.Eda`                       | `firefly-common-eda`                                     | Kafka + Schema Registry + RabbitMQ + in-memory bus                     |
| `FireflyFramework.EventSourcing`             | `firefly-event-sourcing-spring-boot-starter`             | `AggregateRoot`, snapshots, projections, transactional outbox, upcasters |
| `FireflyFramework.Orchestration`             | `firefly-common-domain` (orchestration)                  | Saga, Workflow, TCC engines                                            |
| `FireflyFramework.RuleEngine.{Interfaces,Models,Core,Web,Sdk}` | `firefly-common-rule-engine`            | YAML DSL → AST + visitor evaluator                                     |
| `FireflyFramework.Plugins.{Api,Core}`        | `firefly-platform-plugins`                               | Plugin lifecycle, hot-reload via McMaster.NETCore.Plugins              |
| `FireflyFramework.Resilience`                | Resilience4j (`@CircuitBreaker`, `@Retry`, etc.)         | Polly v8 pipelines: circuit-breaker, retry, rate-limit, bulkhead, time-limit |
| `FireflyFramework.Security`                  | Spring Security                                          | `SecurityContext`, `[PreAuthorize]`, JWT, BCrypt, ASP.NET Core auth wiring |
| `FireflyFramework.Aop`                       | Spring AOP                                               | `[Aspect]` + `[Before/After/Around/AfterReturning/AfterThrowing]` + pointcut DSL |
| `FireflyFramework.Scheduling`                | Spring `@Scheduled` + Quartz                             | `[Scheduled]` cron / fixed-rate / fixed-delay; Cronos-backed `ITaskScheduler` |
| `FireflyFramework.Messaging`                 | Spring Messaging                                         | Lightweight `IMessageBroker` send / subscribe (in-memory + adapters)   |
| `FireflyFramework.Actuator`                  | Spring Boot Actuator                                     | `/actuator/{info,env,beans,metrics,loggers,threaddump,mappings}`       |
| `FireflyFramework.Admin`                     | Spring Boot Admin                                        | Server registry + client heartbeat for dashboard-style introspection   |
| `FireflyFramework.I18n`                      | Spring `MessageSource` + `LocaleResolver`                | JSON resource bundles, fallback culture chain, Accept-Language / Cookie / Fixed resolvers |
| `FireflyFramework.Session`                   | Spring Session                                           | `IFireflySession` + `ISessionStore` (in-memory + Redis), middleware    |
| `FireflyFramework.WebSocket`                 | Spring WebSocket                                         | `[WebSocketMapping]`, lifecycle hooks, group broadcast                 |
| `FireflyFramework.Shell`                     | Spring Shell                                             | `[ShellComponent/Method/Argument/Option]`, `CommandLineRunner`, interactive shell |
| `FireflyFramework.Testing`                   | Spring Boot Test                                         | `FireflyTestBase`, `FireflyTestClient`, slice attributes, event-capture publisher |
| `FireflyFramework.Cli`                       | `fireflyframework-cli` (Go)                              | `firefly` dotnet tool: `new`, `handler`, `saga`, `migration`           |
| `FireflyFramework.Agentic`                   | `fireflyframework-agentic` (Python)                      | LLM agent loop, tools, memory, `IChatModel` / `IEmbeddingModel` ports  |
| `FireflyFramework.AgenticBridge`             | `fireflyframework-agentic-bridge`                        | REST / SSE client for Python-hosted agents                             |

## 03 — Adapters

### IDP

| .NET project                          | Java original                       | Backing tech                            |
|---------------------------------------|-------------------------------------|-----------------------------------------|
| `FireflyFramework.Idp`                | `firefly-idp`                       | Common `IIdpAdapter` port               |
| `FireflyFramework.Idp.InternalDb`     | `firefly-idp-internal-db`           | EF Core users + BCrypt + JWT            |
| `FireflyFramework.Idp.Keycloak`       | `firefly-idp-keycloak`              | OIDC endpoints + Keycloak admin REST API |
| `FireflyFramework.Idp.AzureAd`        | `firefly-idp-azure-ad`              | MSAL + Microsoft.Graph                  |
| `FireflyFramework.Idp.AwsCognito`     | `firefly-idp-aws-cognito`           | `AWSSDK.CognitoIdentityProvider`        |

### ECM (storage + e-signature)

| .NET project                                  | Java original                              | Purpose                                  |
|-----------------------------------------------|--------------------------------------------|------------------------------------------|
| `FireflyFramework.Ecm`                        | `firefly-ecm`                              | Adapter framework, ports, NoOp + Local   |
| `FireflyFramework.Ecm.Storage.Aws`            | `firefly-ecm-storage-aws`                  | S3 document content adapter              |
| `FireflyFramework.Ecm.Storage.Azure`          | `firefly-ecm-storage-azure`                | Azure Blob document content adapter      |
| `FireflyFramework.Ecm.ESignature.DocuSign`    | `firefly-ecm-esignature-docusign`          | DocuSign envelope CRUD (JWT grant)       |
| `FireflyFramework.Ecm.ESignature.AdobeSign`   | `firefly-ecm-esignature-adobe-sign`        | Adobe Sign agreement CRUD (OAuth2)       |
| `FireflyFramework.Ecm.ESignature.Logalty`     | `firefly-ecm-esignature-logalty`           | Logalty processes (OAuth2)               |

### Notifications

| .NET project                              | Java original                          | Channel       |
|-------------------------------------------|----------------------------------------|---------------|
| `FireflyFramework.Notifications`          | `firefly-notifications`                | Contract DTOs |
| `FireflyFramework.Notifications.Core`     | `firefly-notifications` (services)     | Dispatcher + template engine |
| `FireflyFramework.Notifications.SendGrid` | `firefly-notifications-sendgrid`       | Email         |
| `FireflyFramework.Notifications.Resend`   | `firefly-notifications-resend`         | Email         |
| `FireflyFramework.Notifications.Twilio`   | `firefly-notifications-twilio`         | SMS           |
| `FireflyFramework.Notifications.Firebase` | `firefly-notifications-firebase`       | Push          |
| `FireflyFramework.Notifications.Smtp`     | pyfly `notifications/smtp`             | Email via plain SMTP relay (System.Net.Mail) |

### Callbacks (outbound webhook dispatch)

| .NET project                            | Java original              | Purpose                                   |
|-----------------------------------------|----------------------------|-------------------------------------------|
| `FireflyFramework.Callbacks.Interfaces` | `firefly-callbacks`        | DTOs / enums                              |
| `FireflyFramework.Callbacks.Models`     | `firefly-callbacks`        | EF Core entities                          |
| `FireflyFramework.Callbacks.Core`       | `firefly-callbacks`        | Configuration store + dispatcher (HMAC + Polly) + audit |
| `FireflyFramework.Callbacks.Web`        | `firefly-callbacks`        | REST admin controller                     |
| `FireflyFramework.Callbacks.Sdk`        | `firefly-callbacks`        | Typed `HttpClient` for the admin API      |

### Webhooks (inbound webhook ingestion)

| .NET project                                 | Purpose                                                          |
|----------------------------------------------|------------------------------------------------------------------|
| `FireflyFramework.Webhooks.Interfaces`       | DTOs / contract                                                  |
| `FireflyFramework.Webhooks.Core`             | Signature validators (Stripe / GitHub / Twilio / generic HMAC), processing pipeline (validate → rate-limit → enrich → dispatch → DLQ), batching + compression |
| `FireflyFramework.Webhooks.Processor`        | `IWebhookProcessor` SPI for per-event downstream handlers         |
| `FireflyFramework.Webhooks.Web`              | `POST /api/webhooks/{provider}` ingestion endpoint                |
| `FireflyFramework.Webhooks.Sdk`              | Typed `HttpClient` for forwarding events to the ingestion endpoint |

### Cross-protocol clients

| .NET project                          | Java original                 | Coverage                                |
|---------------------------------------|-------------------------------|-----------------------------------------|
| `FireflyFramework.Client`             | `firefly-service-client`      | REST + SOAP + WebSocket + gRPC builders |
| `FireflyFramework.ConfigServer`       | `firefly-config-server`       | Spring-Cloud-Config-compatible endpoint |

## 04 — Starters

| .NET project                                  | Bundles                                                                                |
|-----------------------------------------------|----------------------------------------------------------------------------------------|
| `FireflyFramework.Starter.Core`               | Web + Observability + Cache + EDA + CQRS + Resilience + Messaging                      |
| `FireflyFramework.Starter.Application`        | Core + Plugins + Resilience + Security + Actuator + Scheduling + Session + I18n + AOP + WebSocket |
| `FireflyFramework.Starter.Domain`             | Core + EventSourcing + AOP                                                             |
| `FireflyFramework.Starter.Data`               | Core (consumer supplies the `DbContext`) + Polly                                       |
| `FireflyFramework.BackOffice`                 | Application + back-office context resolver / middleware                                |

## 05 — Tests

`tests/FireflyFramework.Tests/` is a single xUnit project covering every
public surface of the framework — kernel utilities, validators, web
middleware, cache adapters, observability, EF Core repositories, CQRS,
EDA, event sourcing, orchestration engines, rule engine, plugins,
notifications, IDP, ECM, callbacks, webhooks, the config server, the
back-office context, the SDK extensions, and the startup banner.

## 06 — Samples

`samples/FireflyFramework.Samples.OrdersService.*` — five-project
reference service (`.Interfaces`, `.Models`, `.Core`, `.Web`, `.Sdk`)
demonstrating the canonical scaffolding documented in
[SERVICE-SCAFFOLDING.md](SERVICE-SCAFFOLDING.md). Mirrors the
multi-module Maven layout used by every Java service in the Firefly
platform.
