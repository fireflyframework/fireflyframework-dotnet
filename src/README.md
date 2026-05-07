# Firefly Framework — `src/`

This folder holds the **52 publishable framework projects**, organised
into four strictly-layered tiers. Each tier may depend on tiers to its
left and never to its right; the compiler enforces the layering.

```
┌────────────────┐   ┌──────────────────┐   ┌───────────────┐   ┌──────────────────┐
│  FOUNDATIONAL  │ → │     PLATFORM     │ → │   ADAPTERS    │ → │     STARTERS     │
└────────────────┘   └──────────────────┘   └───────────────┘   └──────────────────┘
```

Every project ships its own `README.md` describing its public surface,
configuration class, and short usage examples — click the project name
to open it.

---

## Foundational tier — primitives every service needs

| Project | What it provides |
|---------|------------------|
| [`FireflyFramework.Kernel`](FireflyFramework.Kernel/README.md) | RFC 7807 `ProblemDetail`, `OperationResult<T>`, `IClock`, `FireflyException` hierarchy |
| [`FireflyFramework.Utils`](FireflyFramework.Utils/README.md) | `Try.Of`, `RetryUtils`, `TemplateRenderUtil` (Scriban + iText 7), AES-256 helpers |
| [`FireflyFramework.Validators`](FireflyFramework.Validators/README.md) | IBAN, BIC, Luhn, VAT, phone (E.164), e-mail, password strength, sort code, 16 ID types |
| [`FireflyFramework.Web`](FireflyFramework.Web/README.md) | RFC 7807 middleware, idempotency, correlation IDs, PII masking, typed exception family |

## Platform tier — the infrastructure layer

| Project | What it provides |
|---------|------------------|
| [`FireflyFramework.Cache`](FireflyFramework.Cache/README.md) | `ICacheAdapter` port with Memory, Redis, NoOp adapters; primary + fallback |
| [`FireflyFramework.Observability`](FireflyFramework.Observability/README.md) | OpenTelemetry traces, metrics, logs; Serilog enrichers; health-check primitives |
| [`FireflyFramework.Data`](FireflyFramework.Data/README.md) | EF Core 10 with InMemory / Postgres / MySQL / SQL Server providers, generic filter DSL |
| [`FireflyFramework.Cqrs`](FireflyFramework.Cqrs/README.md) | Command + query buses, fluent dispatch, validation, query caching, event-driven invalidation |
| [`FireflyFramework.Eda`](FireflyFramework.Eda/README.md) | Kafka + RabbitMQ + InMemory; Schema Registry; resilient publisher; filter family |
| [`FireflyFramework.EventSourcing`](FireflyFramework.EventSourcing/README.md) | `AggregateRoot`, snapshots, transactional outbox, projection runner, upcasting |
| [`FireflyFramework.Orchestration`](FireflyFramework.Orchestration/README.md) | Saga (DAG + compensation), Workflow, TCC engines; recovery; topology rendering; REST control plane |
| `FireflyFramework.RuleEngine.{Interfaces,Models,Core,Web,Sdk}` | YAML DSL, AST + visitor evaluator, REST admin, typed SDK |
| `FireflyFramework.Plugins.{Api,Core}` | Lifecycle SPI, McMaster-based hot-reload assembly loader |

## Adapter tier — pluggable integrations

### Service client

| Project | What it provides |
|---------|------------------|
| [`FireflyFramework.Client`](FireflyFramework.Client/README.md) | REST / SOAP / WebSocket / gRPC builders with Polly v8 resilience; service discovery (Static / Eureka / Consul / Kubernetes); load balancing; OAuth2 token cache; deduplication; chaos engineering; health rollup; GraphQL helper |

### Identity provider

| Project | Backend |
|---------|---------|
| [`FireflyFramework.Idp`](FireflyFramework.Idp/README.md) | `IIdpAdapter` port — 19 operations (login, refresh, logout, introspect, revoke, user info, CRUD, password, MFA, sessions, roles, scopes, register) |
| [`FireflyFramework.Idp.Keycloak`](FireflyFramework.Idp.Keycloak/README.md) | Keycloak (direct OIDC + admin REST) |
| [`FireflyFramework.Idp.AwsCognito`](FireflyFramework.Idp.AwsCognito/README.md) | AWS Cognito (`AWSSDK.CognitoIdentityProvider`) |
| [`FireflyFramework.Idp.AzureAd`](FireflyFramework.Idp.AzureAd/README.md) | Azure AD / Entra ID (MSAL + Microsoft Graph) |
| [`FireflyFramework.Idp.InternalDb`](FireflyFramework.Idp.InternalDb/README.md) | Self-hosted (BCrypt + JWT, repository SPI) |

### Enterprise content management

| Project | Backend |
|---------|---------|
| [`FireflyFramework.Ecm`](FireflyFramework.Ecm/README.md) | Adapter framework — `IDocumentPort`, `IDocumentContentPort`, `IFolderPort`, `ISignatureEnvelopePort`, `[EcmAdapter]` discovery |
| [`FireflyFramework.Ecm.Storage.Aws`](FireflyFramework.Ecm.Storage.Aws/README.md) | Amazon S3 |
| [`FireflyFramework.Ecm.Storage.Azure`](FireflyFramework.Ecm.Storage.Azure/README.md) | Azure Blob Storage |
| [`FireflyFramework.Ecm.ESignature.DocuSign`](FireflyFramework.Ecm.ESignature.DocuSign/README.md) | DocuSign (JWT-Bearer + REST v2.1) |
| [`FireflyFramework.Ecm.ESignature.AdobeSign`](FireflyFramework.Ecm.ESignature.AdobeSign/README.md) | Adobe Sign (OAuth2 refresh token + REST v6) |
| [`FireflyFramework.Ecm.ESignature.Logalty`](FireflyFramework.Ecm.ESignature.Logalty/README.md) | Logalty (EU qualified e-signature, OAuth2 client_credentials) |

### Notifications

| Project | Channel |
|---------|---------|
| [`FireflyFramework.Notifications`](FireflyFramework.Notifications/README.md) | Ports — `IEmailProvider`, `ISmsProvider`, `IPushProvider`; DTO set |
| [`FireflyFramework.Notifications.Core`](FireflyFramework.Notifications.Core/README.md) | Dispatcher + `ScribanTemplateEngine`; per-user channel preferences |
| [`FireflyFramework.Notifications.SendGrid`](FireflyFramework.Notifications.SendGrid/README.md) | SendGrid email |
| [`FireflyFramework.Notifications.Twilio`](FireflyFramework.Notifications.Twilio/README.md) | Twilio SMS |
| [`FireflyFramework.Notifications.Resend`](FireflyFramework.Notifications.Resend/README.md) | Resend email (HTTP) |
| [`FireflyFramework.Notifications.Firebase`](FireflyFramework.Notifications.Firebase/README.md) | Firebase Cloud Messaging push |

### Asynchronous & integration subsystems

| Project | What it provides |
|---------|------------------|
| `FireflyFramework.Callbacks.{Interfaces,Models,Core,Sdk,Web}` | Outbound callbacks (HMAC + Polly retry, audit log, REST controller) |
| `FireflyFramework.Webhooks.{Interfaces,Core,Processor,Sdk,Web}` | Inbound webhooks (Stripe / GitHub / Twilio / generic HMAC verifiers) |
| [`FireflyFramework.ConfigServer`](FireflyFramework.ConfigServer/README.md) | Spring-Cloud-Config-compatible REST endpoints (executable host) |

## Starter tier — one-call composition

| Starter | Composition |
|---------|-------------|
| [`FireflyFramework.Starter.Core`](FireflyFramework.Starter.Core/README.md) | Web + Cache + Observability + EDA + CQRS + Client + Validators |
| [`FireflyFramework.Starter.Application`](FireflyFramework.Starter.Application/README.md) | Core + Plugins (IDP / orchestration / rule-engine registered per service) |
| [`FireflyFramework.Starter.Domain`](FireflyFramework.Starter.Domain/README.md) | Core + Event Sourcing |
| [`FireflyFramework.Starter.Data`](FireflyFramework.Starter.Data/README.md) | Core (consumer supplies its own `DbContext`) |
| [`FireflyFramework.BackOffice`](FireflyFramework.BackOffice/README.md) | Application + back-office context resolver and middleware |

---

## Conventions every project follows

* **Apache 2.0 header** on every `.cs` file — the same Firefly Software Foundation copyright that appears in the Java sources.
* **File-scoped namespaces** matching the `<RootNamespace>` set in the csproj.
* **`<IsPackable>true</IsPackable>`** by default (set centrally in `Directory.Build.props`); the only opt-out is `FireflyFramework.ConfigServer`, which is an executable host.
* **`*Options` companion class** for every module that takes runtime configuration, bound under the `Firefly:<Module>` section.
* **`AddFirefly<Module>(IConfiguration)`** extension on `IServiceCollection` for DI registration; **`UseFirefly<Module>()`** on `IApplicationBuilder` where middleware is involved.
* **Documented upstream limits** — every public method either runs real code or throws `NotSupportedException` with an actionable message. There are no silent stubs.

## Where to look next

* [`docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md) — the layering model and dependency-direction rules in detail.
* [`docs/MODULES.md`](../docs/MODULES.md) — the same project list, organised one paragraph per project, with the matching Java module.
* [`docs/SERVICE-SCAFFOLDING.md`](../docs/SERVICE-SCAFFOLDING.md) — how to compose these projects into a runnable service.
